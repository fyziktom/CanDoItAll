using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed partial class AgentToolAdmissionJournal {
    internal static readonly TimeSpan ReceiptSliceBudget = TimeSpan.FromSeconds(5);
    internal static readonly TimeSpan ReceiptCheckpointBudget = TimeSpan.FromSeconds(2);
    public Task<AgentToolJournalRecord> BeginEffectSegmentAsync(AgentToolRunLease lease, CancellationToken cancellationToken)
        => MutateAsync(lease, true, journal => {
            if (journal.SchemaVersion != AgentToolJournalRecord.EffectJournalSchemaVersion ||
                journal.Support != AgentToolAdmissionSupport.EffectJournalOnly) {
                throw Failure("An effects-only origin requires its retained schema-five admission.");
            }
            if (journal.Segments.Length != 0) {
                RequireLiveEffectOrigin(lease, journal);
                return journal;
            }
            var segment = new AgentToolInvocationSegment(Guid.NewGuid(), 0, 0, null, PendingApprovals: [],
                CheckpointKind: AgentToolCheckpointKind.EffectsOnly, OriginDispatchLeaseId: lease.Id);
            return journal with { Revision = checked(journal.Revision + 1), Segments = [segment] };
        }, cancellationToken);

    public async Task<IReadOnlyList<PendingToolApprovalRecord>> SaveEffectApprovalsAsync(AgentToolRunLease lease,
        Guid segmentId, IReadOnlyList<PendingToolApprovalRecord> approvals, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(approvals);
        lease.RequireOwner(this);
        RequireLeaseMode(lease, reconciliationOnly: false);
        if (approvals.Count is 0 or > AgentToolJournalRecord.MaximumCallsPerBatch) {
            throw Failure("An effects-only approval publication requires a bounded nonempty batch.");
        }
        var captured = approvals.ToImmutableArray();
        if (lease.Session.BackgroundSource is not null) {
            await RequireBackgroundSourceAsync((await RequireSnapshotAsync(lease.Session.ExecutionRunId, cancellationToken)).Run,
                readOnly: false, cancellationToken);
        }
        var saved = await writer.UpdateExecutionRunDetailAsync(lease.Session.ExecutionRunId, detail => {
            var journal = RequireJournal(detail.Run);
            RequireCurrent(journal.Session, new(detail.Run, detail.ChatSession));
            RequireLease(lease, journal);
            RequireLiveEffectOrigin(lease, journal);
            if (journal.Support != AgentToolAdmissionSupport.EffectJournalOnly || journal.Segments[0].Id != segmentId ||
                detail.Run.PendingApprovals.Count != 0 || journal.Segments[0].PendingApprovals.Any(approval =>
                    AgentToolJournalTransitions.RequireProposal(journal, approval.ToolAdmission!).ApprovalStatus == ExecutionApprovalStatus.Pending)) {
                throw Failure("The original live effects-only segment must resolve its prior decisions before another approval batch.");
            }
            var segment = journal.Segments[0];
            if (captured.Length > AgentToolJournalRecord.MaximumCallsPerBatch - segment.PendingApprovals.Length) {
                throw Failure("The retained effects-only approval sequence exceeds its bound.");
            }
            var proposals = journal.Batches.SelectMany(batch => batch.Proposals.Select(proposal => (Batch: batch.Id, Proposal: proposal)))
                .Where(item => item.Proposal.RequiresApproval && item.Proposal.ApprovalStatus == ExecutionApprovalStatus.Pending &&
                    item.Proposal.ApprovalId is null).ToArray();
            var pending = captured.Select(approval => {
                var match = proposals.SingleOrDefault(item => item.Proposal.CallId == approval.CallId &&
                    item.Proposal.Payload.ToolName == approval.ToolName && item.Proposal.Payload.ArgumentsJson == approval.ArgumentsJson);
                if (match.Proposal is null || approval.ToolAdmission is { } supplied && supplied != Binding(match.Batch, match.Proposal)) {
                    throw Failure("The effects-only approval does not match an original pending proposal.");
                }
                return approval with { ToolAdmission = Binding(match.Batch, match.Proposal) };
            }).ToImmutableArray();
            var changed = AgentToolJournalTransitions.BindApprovals(journal, pending);
            changed = changed with {
                Revision = checked(changed.Revision + 1),
                Segments = changed.Segments.SetItem(0, segment with { PendingApprovals = segment.PendingApprovals.AddRange(pending) })
            };
            AgentToolJournalTransitions.ValidatePersistence(journal, changed);
            return detail with { Run = detail.Run with {
                ToolAdmission = changed, PendingApprovals = pending, State = ExecutionState.WaitingOnTool,
                Revision = checked(detail.Run.Revision + 1), UpdatedAtUtc = clock.GetUtcNow()
            } };
        }, cancellationToken);
        return saved.Run.PendingApprovals;
    }

    private static void RequireLiveEffectOrigin(AgentToolRunLease lease, AgentToolJournalRecord journal) {
        if (journal.Support == AgentToolAdmissionSupport.Recoverable) {
            return;
        }
        if (journal.SchemaVersion != AgentToolJournalRecord.EffectJournalSchemaVersion ||
            journal.Support != AgentToolAdmissionSupport.EffectJournalOnly || journal.Segments.Length != 1 ||
            journal.Segments[0].CheckpointKind != AgentToolCheckpointKind.EffectsOnly ||
            journal.Segments[0].OriginDispatchLeaseId != lease.Id || lease.Mode != AgentToolRunLeaseMode.Dispatch) {
            throw Failure("Only the original live dispatch lease can append to an effects-only segment.");
        }
    }

    public async ValueTask<AgentToolRunLease> AcquireInterruptedReconciliationAsync(AgentToolSessionReference reference,
        CancellationToken cancellationToken) {
        var started = clock.GetTimestamp();
        using var admissionBudget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        admissionBudget.CancelAfter(ReceiptSliceBudget);
        var handle = await leases.AcquireToolDispatchLeaseAsync(reference.ExecutionRunId, admissionBudget.Token);
        try {
            var lease = new AgentToolRunLease(this, reference, Guid.NewGuid(), handle, AgentToolRunLeaseMode.InterruptedEffectReconciliation, started);
            var admitted = await writer.UpdateExecutionRunDetailAsync(reference.ExecutionRunId, detail => {
                var journal = RequireJournal(detail.Run);
                if (journal.SchemaVersion != AgentToolJournalRecord.EffectJournalSchemaVersion ||
                    journal.Support != AgentToolAdmissionSupport.EffectJournalOnly || journal.Session.Reference != reference) {
                    throw Failure("Interrupted reconciliation requires the exact retained effects-only session.");
                }

                RequireCurrent(journal.Session, new(detail.Run, detail.ChatSession), AgentToolRunLeaseMode.InterruptedEffectReconciliation);
                var changed = journal with { Revision = checked(journal.Revision + 1), ActiveDispatchLeaseId = lease.Id };
                AgentToolJournalTransitions.ValidatePersistence(journal, changed);
                return detail with { Run = detail.Run with {
                    ToolAdmission = changed, Revision = checked(detail.Run.Revision + 1), UpdatedAtUtc = clock.GetUtcNow()
                } };
            }, admissionBudget.Token);
            lease.Pin(this, admitted);
            return lease;
        } catch (Exception originalFailure) {
            try {
                await handle.DisposeAsync();
            } catch (Exception cleanupFailure) {
                throw new AgentEffectReconciliationLeaseException(originalFailure, cleanupFailure);
            }
            throw;
        }
    }

    public async Task<AgentEffectContinuation> ReadInterruptedContinuationAsync(ExecutionRunDetail detail,
        Func<AgentToolSessionObservation, CancellationToken, ValueTask<bool>> currentRead, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(detail);
        ArgumentNullException.ThrowIfNull(currentRead);
        var run = detail.Run;
        var journal = RequireJournal(run);
        if (journal.Support != AgentToolAdmissionSupport.EffectJournalOnly) {
            throw Failure("A continuation summary requires the original effects-only journal.");
        }
        var admission = new ExecutionRunAdmissionSnapshot(run, detail.ChatSession);
        RequireCurrent(journal.Session, admission, AgentToolRunLeaseMode.InterruptedEffectReconciliation);
        var observation = new AgentToolSessionObservation(journal.Session,
            AgentTurnContextMetadata.TryReadTurnContextReference(run.MetadataJson),
            AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(run.MetadataJson));
        await RequireEffectReadPolicyAsync(run, observation, currentRead, recheckAfterBackground: true, cancellationToken);
        var snapshot = ClassifyEffectSlice(journal);
        await RequireEffectReadPolicyAsync(run, observation, currentRead, recheckAfterBackground: true, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return new(run.Id, run.ChatSessionId, run.Revision, journal.Revision, Requirement(snapshot, snapshot.Counts),
            snapshot.Counts, 0, snapshot.Counts.EligibleOwnerEffects, AgentEffectReconciliationStop.SnapshotRead,
            AgentEffectCheckpointPersistence.Confirmed, []);
    }

    public async Task<AgentEffectContinuation> ReconcileInterruptedAsync(AgentToolRunLease lease,
        IReadOnlyList<IAgentToolReceiptReconciliationProvider> providers,
        Func<AgentToolSessionObservation, CancellationToken, ValueTask<bool>> currentRead,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(currentRead);
        var pinned = lease.RequirePinnedSnapshot(this);
        RequireInterruptedLease(lease, pinned);
        lease.BeginReconciliation(this);
        var original = pinned.Admission.Run;
        var journal = RequireJournal(original);
        var snapshot = ClassifyEffectSlice(journal);
        var observations = new List<EffectObservation>(AgentEffectContinuation.MaximumObservations);
        var stop = snapshot.Counts.EligibleOwnerEffects > snapshot.Candidates.Length
            ? AgentEffectReconciliationStop.LookupLimit : AgentEffectReconciliationStop.ExhaustedSnapshot;
        var remaining = ReceiptSliceBudget - clock.GetElapsedTime(lease.ReconciliationStartedTimestamp);
        using var lookupBudget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (remaining > TimeSpan.Zero) {
            lookupBudget.CancelAfter(remaining);
        } else {
            lookupBudget.Cancel();
        }
        foreach (var candidate in snapshot.Candidates) {
            if (lookupBudget.IsCancellationRequested) {
                stop = cancellationToken.IsCancellationRequested ? AgentEffectReconciliationStop.Cancelled : AgentEffectReconciliationStop.CooperativeBudget;
                break;
            }
            var claim = new AgentToolReceiptReconciliationClaim(this, lease, candidate.Binding,
                currentRead: currentRead, position: candidate.Position);
            try {
                await claim.RequireAsync(lookupBudget.Token);
            } catch (OperationCanceledException) when (lookupBudget.IsCancellationRequested) {
                stop = cancellationToken.IsCancellationRequested ? AgentEffectReconciliationStop.Cancelled : AgentEffectReconciliationStop.CooperativeBudget;
                break;
            } catch (Exception exception) when (exception is AgentToolReceiptAccessDeniedException or AgentToolReceiptReadUnavailableException) {
                throw;
            } catch (OperationCanceledException) {
                throw;
            } catch (Exception exception) when (exception is not OutOfMemoryException) {
                stop = AgentEffectReconciliationStop.ReaderFailure;
                break;
            }
            var observation = new EffectObservation(candidate);
            observations.Add(observation);
            try {
                IAgentToolReceiptReconciliationProvider? selected = null;
                foreach (var provider in providers) {
                    if (!provider.Supports(candidate.Proposal.Payload.ToolName)) {
                        continue;
                    }
                    if (selected is not null) {
                        throw new InvalidDataException("Multiple receipt readers claim the same original tool.");
                    }
                    selected = provider;
                }
                if (selected is null) {
                    continue;
                }
                var observed = await selected.ReconcileAsync(claim, lookupBudget.Token);
                if ((observed.CommittedEffect is null) != (observed.Receipt is null)) {
                    throw new InvalidDataException("Owner confirmation requires complete original effect and receipt evidence.");
                }
                if (observed.CommittedEffect is { } effect) {
                    if (string.IsNullOrEmpty(effect.SourceKind) || string.IsNullOrEmpty(effect.SourceId) ||
                        char.IsWhiteSpace(effect.SourceKind[0]) || char.IsWhiteSpace(effect.SourceKind[^1]) ||
                        char.IsWhiteSpace(effect.SourceId[0]) || char.IsWhiteSpace(effect.SourceId[^1])) {
                        throw new InvalidDataException("Owner confirmation requires a canonical effect identity.");
                    }
                    observation.Knowledge = AgentEffectReceiptKnowledge.ReceiptConfirmed;
                    if ((long)effect.SourceKind.Length + effect.SourceId.Length > AgentToolProtocolEnvelope.MaximumUtf8Bytes ||
                        (long)Encoding.UTF8.GetByteCount(effect.SourceKind) + Encoding.UTF8.GetByteCount(effect.SourceId) > AgentToolProtocolEnvelope.MaximumUtf8Bytes) {
                        throw new ReceiptEvidenceCapacityException();
                    }
                    var receipt = new AgentToolOwnerReceipt(effect, observed.Receipt!);
                    using var bound = new ReceiptEvidenceBound();
                    JsonSerializer.Serialize(bound, receipt);
                    observation.Receipt = receipt;
                } else {
                    observation.Knowledge = AgentEffectReceiptKnowledge.ReceiptNotObserved;
                }
                await claim.RequireAsync(lookupBudget.Token);
            } catch (Exception exception) when (exception is AgentToolReceiptAccessDeniedException or AgentToolReceiptReadUnavailableException) {
                throw;
            } catch (ReceiptEvidenceCapacityException) {
                stop = AgentEffectReconciliationStop.EvidenceCapacity;
                break;
            } catch (OperationCanceledException) when (lookupBudget.IsCancellationRequested) {
                stop = cancellationToken.IsCancellationRequested ? AgentEffectReconciliationStop.Cancelled : AgentEffectReconciliationStop.CooperativeBudget;
                break;
            } catch (OperationCanceledException) {
                throw;
            } catch (Exception exception) when (exception is not OutOfMemoryException) {
                stop = AgentEffectReconciliationStop.ReaderFailure;
                break;
            }
            if (lookupBudget.IsCancellationRequested) {
                stop = cancellationToken.IsCancellationRequested ? AgentEffectReconciliationStop.Cancelled : AgentEffectReconciliationStop.CooperativeBudget;
                break;
            }
        }
        using var checkpointBudget = new CancellationTokenSource(ReceiptCheckpointBudget);
        await RequireEffectReadAsync(lease, pinned, currentRead, checkpointBudget.Token);
        var persistence = AgentEffectCheckpointPersistence.NotAttempted;
        var counts = snapshot.Counts;
        var requirement = Requirement(snapshot, counts);
        var runRevision = original.Revision;
        var journalRevision = journal.Revision;
        if (stop != AgentEffectReconciliationStop.EvidenceCapacity) {
            AgentToolJournalRecord? changed = null;
            var confirmed = observations.Count(item => item.Receipt is not null);
            var targetCounts = counts with {
                KnownCommitted = checked(counts.KnownCommitted + confirmed),
                EligibleOwnerEffects = checked(counts.EligibleOwnerEffects - confirmed)
            };
            var targetRequirement = Requirement(snapshot, targetCounts);
            try {
                changed = ConfirmEffects(journal, observations) with { ToolContinuationRequirement = targetRequirement };
                AgentToolJournalTransitions.ValidatePersistence(journal, changed);
            } catch (InvalidDataException) {
                stop = AgentEffectReconciliationStop.EvidenceCapacity;
                changed = null;
            }
            if (changed is not null) {
                try {
                    var committed = await writer.UpdateExecutionRunDetailAsync(original.Id, current => {
                        RequireEffectCheckpoint(lease, pinned, current);
                        return current with { Run = current.Run with {
                            ToolAdmission = changed, State = ExecutionState.WaitingOnTool, Outcome = null,
                            Revision = checked(current.Run.Revision + 1), UpdatedAtUtc = clock.GetUtcNow(), CompletedAtUtc = null
                        } };
                    }, checkpointBudget.Token);
                    persistence = AgentEffectCheckpointPersistence.Confirmed;
                    counts = targetCounts;
                    requirement = targetRequirement;
                    runRevision = committed.Run.Revision;
                    journalRevision = committed.Run.ToolAdmission!.Revision;
                } catch (Exception exception) when (exception is EffectCheckpointConflictException or
                        SandboxWorkspacePublicationConflictException or SandboxWorkspaceConcurrencyException) {
                    stop = AgentEffectReconciliationStop.CheckpointConflict;
                } catch (Exception exception) when (exception is not (AgentToolReceiptAccessDeniedException or OutOfMemoryException)) {
                    stop = AgentEffectReconciliationStop.CheckpointFailure;
                    persistence = AgentEffectCheckpointPersistence.Unconfirmed;
                }
            }
        }
        await RequireEffectReadAsync(lease, pinned, currentRead, checkpointBudget.Token);
        return new(original.Id, original.ChatSessionId, runRevision, journalRevision, requirement, counts, observations.Count,
            checked(snapshot.Counts.EligibleOwnerEffects - observations.Count), stop, persistence,
            observations.Select(item => new AgentEffectReceiptSummary(item.Candidate.Binding, item.Knowledge,
                item.Receipt is not null && persistence == AgentEffectCheckpointPersistence.Confirmed)).ToImmutableArray());
    }

    private async ValueTask RequireEffectReadAsync(AgentToolRunLease lease, AgentToolPinnedRunSnapshot pinned,
        Func<AgentToolSessionObservation, CancellationToken, ValueTask<bool>> currentRead, CancellationToken cancellationToken) {
        RequireInterruptedLease(lease, pinned);
        var run = pinned.Admission.Run;
        var journal = RequireJournal(run);
        var observation = new AgentToolSessionObservation(journal.Session,
            AgentTurnContextMetadata.TryReadTurnContextReference(run.MetadataJson),
            AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(run.MetadataJson));
        await RequireEffectReadPolicyAsync(run, observation, currentRead, recheckAfterBackground: false, cancellationToken);
        RequireInterruptedLease(lease, pinned);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private async ValueTask RequireEffectReadPolicyAsync(ExecutionRunRecord run, AgentToolSessionObservation observation,
        Func<AgentToolSessionObservation, CancellationToken, ValueTask<bool>> currentRead, bool recheckAfterBackground,
        CancellationToken cancellationToken) {
        try {
            if (!await currentRead(observation, cancellationToken) || observation.Session.Reference.BackgroundSource is not null &&
                (!await CanReadBackgroundResultAsync(run, cancellationToken) ||
                    recheckAfterBackground && !await currentRead(observation, cancellationToken))) {
                throw new AgentToolReceiptAccessDeniedException();
            }
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch (Exception exception) when (exception is not (AgentToolReceiptAccessDeniedException or
                AgentToolReceiptReadUnavailableException or OutOfMemoryException)) {
            throw new AgentToolReceiptReadUnavailableException(exception);
        }
    }

    private void RequireInterruptedLease(AgentToolRunLease lease, AgentToolPinnedRunSnapshot pinned) {
        lease.RequireOwner(this);
        if (!ReferenceEquals(AgentToolRunLease.Current, lease) || !ReferenceEquals(lease.RequirePinnedSnapshot(this), pinned)) {
            throw Failure("Receipt reconciliation requires its bound owner-pinned lease.");
        }
        RequireCurrent(pinned.Admission.Run.ToolAdmission!.Session, pinned.Admission, AgentToolRunLeaseMode.InterruptedEffectReconciliation);
        RequireLease(lease, pinned.Admission.Run.ToolAdmission);
    }

    private void RequireEffectCheckpoint(AgentToolRunLease lease, AgentToolPinnedRunSnapshot pinned, ExecutionRunDetail current) {
        RequireInterruptedLease(lease, pinned);
        var previous = pinned.Admission.Run;
        var currentJournal = RequireJournal(current.Run);
        if (current.Run.Revision != previous.Revision || currentJournal.Revision != previous.ToolAdmission!.Revision ||
            currentJournal.Session != previous.ToolAdmission.Session || currentJournal.ActiveDispatchLeaseId != lease.Id ||
            current.Run.State != previous.State || current.Run.Outcome != previous.Outcome || current.Run.MetadataJson != previous.MetadataJson ||
            current.Run.CreatedAtUtc != previous.CreatedAtUtc || current.Run.AgentId != previous.AgentId ||
            current.Run.SourceKind != previous.SourceKind || current.Run.SourceId != previous.SourceId ||
            current.Run.ChatSessionId != previous.ChatSessionId || current.ChatSession?.Id != pinned.Admission.ChatSession?.Id ||
            current.ChatSession?.AgentId != pinned.Admission.ChatSession?.AgentId ||
            !current.Run.PendingApprovals.SequenceEqual(previous.PendingApprovals) || !current.Approvals.SequenceEqual(pinned.Approvals)) {
            throw new EffectCheckpointConflictException();
        }
        RequireCurrent(currentJournal.Session, new(current.Run, current.ChatSession), AgentToolRunLeaseMode.InterruptedEffectReconciliation);
        AgentToolJournalTransitions.ValidatePersistence(previous.ToolAdmission, currentJournal);
    }

    private static AgentToolJournalRecord ConfirmEffects(AgentToolJournalRecord journal, IReadOnlyList<EffectObservation> observations) {
        var batches = journal.Batches.ToBuilder();
        foreach (var group in observations.Where(item => item.Receipt is not null).GroupBy(item => item.Candidate.Position.BatchOrdinal)) {
            var batch = batches[group.Key];
            var proposals = batch.Proposals.ToBuilder();
            foreach (var item in group) {
                var proposal = proposals[item.Candidate.Position.ProposalOrdinal];
                proposals[proposal.Ordinal] = proposal with {
                    State = AgentToolProposalState.OwnerReceiptConfirmed, EffectState = AgentToolEffectState.Committed, OwnerReceipt = item.Receipt
                };
            }
            batches[group.Key] = batch with { Proposals = proposals.ToImmutable() };
        }
        return journal with {
            Revision = checked(journal.Revision + 1), Batches = batches.ToImmutable(),
            ReceiptReconciliationCursor = observations.LastOrDefault()?.Candidate.Position ?? journal.ReceiptReconciliationCursor
        };
    }

    private static EffectSliceSnapshot ClassifyEffectSlice(AgentToolJournalRecord journal) {
        var before = new List<EffectCandidate>(AgentEffectContinuation.MaximumObservations);
        var after = new List<EffectCandidate>(AgentEffectContinuation.MaximumObservations);
        var total = 0;
        var known = 0;
        var eligible = 0;
        var prepared = 0;
        var pending = 0;
        var other = 0;
        foreach (var batch in journal.Batches) {
            foreach (var proposal in batch.Proposals) {
                total = checked(total + 1);
                known += proposal.State == AgentToolProposalState.OwnerReceiptConfirmed ? 1 : 0;
                prepared += proposal.State == AgentToolProposalState.Prepared && proposal.Payload.Effect == AgentToolProposalEffect.Mutation ? 1 : 0;
                pending += proposal.ApprovalStatus == ExecutionApprovalStatus.Pending ? 1 : 0;
                var uncertain = proposal.State is AgentToolProposalState.Executing or AgentToolProposalState.ReconciliationRequired;
                if (!uncertain || proposal.Payload.Effect != AgentToolProposalEffect.Mutation ||
                    proposal.Payload.Recovery != AgentToolProposalRecovery.OwnerReceipt || proposal.ProviderCall is not null ||
                    proposal.DispatchClaimId is null || proposal.ApprovalStatus != ExecutionApprovalStatus.Approved ||
                    proposal.ApprovedDigest != proposal.Payload.Digest) {
                    other += uncertain ? 1 : 0;
                    continue;
                }
                eligible++;
                var cursor = journal.ReceiptReconciliationCursor;
                var following = cursor is null || batch.Ordinal > cursor.BatchOrdinal ||
                    batch.Ordinal == cursor.BatchOrdinal && proposal.Ordinal > cursor.ProposalOrdinal;
                var selected = following ? after : before;
                if (selected.Count < AgentEffectContinuation.MaximumObservations) {
                    selected.Add(new(Binding(batch.Id, proposal), new(batch.Id, proposal.IntentId, batch.Ordinal, proposal.Ordinal), proposal));
                }
            }
        }
        var native = journal.ProviderDispatches.IsDefault ? 0 : journal.ProviderDispatches.Count(item => item.State != AgentToolProviderDispatchState.ResponseAdmitted);
        return new(new(total, known, eligible, prepared, pending, other, native),
            after.Concat(before).Take(AgentEffectContinuation.MaximumObservations).ToImmutableArray(),
            journal.RecoveryInputContent is null && journal.RuntimeContext is null);
    }

    private static AgentToolContinuationRequirement Requirement(EffectSliceSnapshot snapshot, AgentEffectSnapshotCounts counts)
        => counts.EligibleOwnerEffects != 0 || counts.OtherUnresolvedProposals != 0 || counts.UncertainProviderDispatches != 0
            ? AgentToolContinuationRequirement.EffectReconciliationRequired
            : counts.PreparedEffects == 0 && counts.PendingApprovals == 0 && snapshot.MissingOriginalInput
                ? AgentToolContinuationRequirement.ProviderInputRequired : AgentToolContinuationRequirement.None;

    private sealed record EffectCandidate(AgentToolApprovalBinding Binding, AgentToolReceiptReconciliationCursor Position, AgentToolProposalRecord Proposal);
    private sealed record EffectSliceSnapshot(AgentEffectSnapshotCounts Counts, ImmutableArray<EffectCandidate> Candidates, bool MissingOriginalInput);
    private sealed class EffectObservation(EffectCandidate candidate) {
        internal EffectCandidate Candidate { get; } = candidate;
        internal AgentToolOwnerReceipt? Receipt { get; set; }
        internal AgentEffectReceiptKnowledge Knowledge { get; set; } = AgentEffectReceiptKnowledge.ReaderUnavailable;
    }
    private sealed class EffectCheckpointConflictException : InvalidOperationException {
    }
    private sealed class ReceiptEvidenceCapacityException : Exception {
    }
    private sealed class ReceiptEvidenceBound : Stream {
        private long bytes;
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => bytes;
        public override long Position {
            get => bytes;
            set => throw new NotSupportedException();
        }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));
        public override void Write(ReadOnlySpan<byte> buffer) {
            bytes = checked(bytes + buffer.Length);
            if (bytes > AgentToolProtocolEnvelope.MaximumUtf8Bytes) {
                throw new ReceiptEvidenceCapacityException();
            }
        }
    }

    private async ValueTask<AgentToolReceiptReconciliationAdmission> RequireInterruptedReceiptAsync(
        AgentToolReceiptReconciliationClaim claim, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        var pinned = claim.Lease.RequirePinnedSnapshot(this);
        var detail = pinned.Admission;
        var journal = RequireJournal(detail.Run);
        RequireCurrent(journal.Session, detail, AgentToolRunLeaseMode.InterruptedEffectReconciliation);
        RequireLease(claim.Lease, journal);
        var evidence = pinned.Evidence;
        if (evidence.DispatchLeaseId != claim.Lease.Id || evidence.RunRevision != detail.Run.Revision ||
            evidence.JournalRevision != journal.Revision || evidence.ExecutionCreatedAtUtc != detail.Run.CreatedAtUtc ||
            claim.Purpose != AgentToolReceiptReconciliationPurpose.UncertainEffect || claim.CurrentRead is not { } currentRead) {
            throw Failure("Interrupted receipt access requires the owner's pinned admission and a current read-policy callback.");
        }

        AgentToolProposalRecord proposal;
        if (claim.Position is { } position) {
            if (position.BatchOrdinal < 0 || position.BatchOrdinal >= journal.Batches.Length ||
                journal.Batches[position.BatchOrdinal].Id != position.BatchId || position.BatchId != claim.Binding.BatchId ||
                position.ProposalOrdinal < 0 || position.ProposalOrdinal >= journal.Batches[position.BatchOrdinal].Proposals.Length) {
                throw Failure("The pinned receipt claim no longer names its exact original position.");
            }
            proposal = journal.Batches[position.BatchOrdinal].Proposals[position.ProposalOrdinal];
            if (proposal.IntentId != position.IntentId || Binding(position.BatchId, proposal) != claim.Binding) {
                throw Failure("The pinned receipt claim no longer names its exact original payload.");
            }
        } else {
            proposal = AgentToolJournalTransitions.RequireProposal(journal, claim.Binding);
        }
        if (proposal.Payload.Effect != AgentToolProposalEffect.Mutation || proposal.Payload.Recovery != AgentToolProposalRecovery.OwnerReceipt ||
            proposal.ProviderCall is not null || proposal.DispatchClaimId is null ||
            proposal.ApprovalStatus != ExecutionApprovalStatus.Approved || proposal.ApprovedDigest != proposal.Payload.Digest ||
            proposal.State is not (AgentToolProposalState.Executing or AgentToolProposalState.ReconciliationRequired)) {
            throw Failure("The interrupted proposal has no exact originally approved dispatched owner-receipt intent.");
        }

        var observation = new AgentToolSessionObservation(journal.Session,
            AgentTurnContextMetadata.TryReadTurnContextReference(detail.Run.MetadataJson),
            AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(detail.Run.MetadataJson));
        await RequireEffectReadPolicyAsync(detail.Run, observation, currentRead, recheckAfterBackground: true, cancellationToken);
        claim.RequireOwner(this);
        cancellationToken.ThrowIfCancellationRequested();
        return new(journal.Session, claim.Binding, proposal.Payload) { PinnedEvidence = evidence };
    }
}
