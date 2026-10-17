using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Runtime.Abstractions;

namespace CanDoItAll.AgentFramework.Core;

public sealed partial class AgentToolAdmissionJournal {
    private const string SavedEffectDecisionSource = "saved-effect-decision";

    public async Task<AgentToolRunLease> AcquireSavedEffectDispatchAsync(AgentSavedEffectContinuationRequest request,
        Func<AgentToolSessionObservation, CancellationToken, ValueTask<bool>> currentRead, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(currentRead);
        var original = await RequireSavedRequestAsync(request.ExecutionRunId, request.ChatSessionId,
            request.ExpectedRunRevision, request.ExpectedJournalRevision, cancellationToken);
        await RequireSavedReadAsync(original, currentRead, cancellationToken);
        var handle = await leases.AcquireToolDispatchLeaseAsync(request.ExecutionRunId, cancellationToken);
        try {
            var lease = new AgentToolRunLease(this, original.ToolAdmission!.Session.Reference, Guid.NewGuid(), handle,
                AgentToolRunLeaseMode.SavedEffectDispatch);
            var saved = await writer.UpdateExecutionRunDetailAsync(request.ExecutionRunId, current => {
                var journal = RequireSavedOriginal(original, current, compareRevision: true);
                RequireCurrent(journal.Session, new(current.Run, current.ChatSession), AgentToolRunLeaseMode.SavedEffectDispatch);
                var selected = AgentToolJournalTransitions.RequireProposal(journal, request.Binding);
                RequirePreparedOwnerEffect(selected);
                var changed = AgentToolJournalTransitions.ApplyDecisions(journal, current.Run.PendingApprovals,
                    request.Decisions, automatic: false);
                if (changed.Batches.SelectMany(batch => batch.Proposals).Any(item => item.ApprovalStatus == ExecutionApprovalStatus.Pending)) {
                    throw Failure("Every original pending decision must be resolved before a saved effect can be selected.");
                }
                selected = AgentToolJournalTransitions.RequireProposal(changed, request.Binding);
                if (selected.State != AgentToolProposalState.Rejected) {
                    RequireSelectedPrepared(changed, request.Binding);
                }
                var now = clock.GetUtcNow();
                var approvals = ExecutionRunStateTransitions.ApplyApprovalDecision(current.Approvals, current.Run,
                    request.Decisions, now, SavedEffectDecisionSource, current.Run.Id.ToString("D")).RunApprovals;
                changed = changed with { Revision = checked(changed.Revision + 1), ActiveDispatchLeaseId = lease.Id };
                AgentToolJournalTransitions.ValidatePersistence(journal, changed);
                return current with {
                    Run = current.Run with {
                        ToolAdmission = changed, PendingApprovals = [], State = ExecutionState.WaitingOnTool,
                        Revision = checked(current.Run.Revision + 1), UpdatedAtUtc = now
                    },
                    Approvals = approvals
                };
            }, cancellationToken);
            lease.AdmitSavedEffect(this, new(saved.Run with { PendingApprovals = saved.Run.PendingApprovals.ToImmutableArray() },
                request.Binding, currentRead));
            return lease;
        } catch (Exception primary) {
            try {
                await handle.DisposeAsync();
            } catch (Exception cleanup) {
                throw new AgentEffectReconciliationLeaseException(primary, cleanup);
            }
            throw;
        }
    }

    public async Task ObserveSavedEffectReceiptAsync(AgentToolRunLease lease,
        IAgentToolReceiptReconciliationProvider provider, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(provider);
        var admitted = RequireBoundSavedEffect(lease);
        lease.BeginSavedReceipt(this);
        var snapshot = await ReadSavedEffectSnapshotAsync(lease, cancellationToken);
        var proposal = RequireSelectedPrepared(RequireJournal(snapshot.Run), admitted.Binding);
        if (!provider.Supports(proposal.Payload.ToolName)) {
            throw Failure("The selected receipt provider does not own this original tool.");
        }
        var claim = new AgentToolReceiptReconciliationClaim(this, lease, admitted.Binding,
            AgentToolReceiptReconciliationPurpose.PreparedSavedEffect);
        var observed = await provider.ReconcileAsync(claim, cancellationToken);
        await RequireSavedEffectReceiptAsync(claim, cancellationToken);
        if (observed.CommittedEffect is not null || observed.Receipt is not null) {
            throw new AgentToolAdmissionException("tool-admission.prepared-receipt-conflict",
                "An owner receipt contradicts this undispatched proposal. Dispatch is refused without inventing claim history.");
        }
        lease.ConfirmSavedReceiptAbsence(this);
    }

    public async Task<AgentToolInvocationClaim> ClaimSavedInvocationAsync(AgentToolRunLease lease,
        AgentToolApprovalBinding binding, AgentToolPreparedPayload restored, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(restored);
        var admitted = RequireBoundSavedEffect(lease);
        if (binding != admitted.Binding) {
            throw Failure("This saved lease cannot select another original effect.");
        }
        var observed = await ReadSavedEffectSnapshotAsync(lease, cancellationToken);
        RequireSavedMutationCeiling(observed.Run);
        var selected = RequireSelectedPrepared(RequireJournal(observed.Run), binding);
        if (selected.Payload != restored) {
            throw Failure("Restoration must preserve the entire original arguments and source preparation.");
        }
        lease.BeginSavedClaim(this);
        var claimId = Guid.NewGuid();
        var saved = await writer.UpdateExecutionRunDetailAsync(lease.Session.ExecutionRunId, current => {
            RequireBoundSavedEffect(lease);
            var journal = RequireSavedOriginal(admitted.Run, current, compareRevision: true);
            RequireCurrent(journal.Session, new(current.Run, current.ChatSession), AgentToolRunLeaseMode.SavedEffectDispatch);
            RequireSavedMutationCeiling(current.Run);
            RequireLease(lease, journal);
            var proposal = RequireSelectedPrepared(journal, binding);
            if (proposal.Payload != restored) {
                throw Failure("The current original effect no longer matches its restoration.");
            }
            var changed = AgentToolJournalTransitions.Replace(journal, binding, prior => prior with {
                State = AgentToolProposalState.Executing, DispatchClaimId = claimId
            });
            return current with { Run = current.Run with {
                ToolAdmission = changed, State = ExecutionState.Running,
                Revision = checked(current.Run.Revision + 1), UpdatedAtUtc = clock.GetUtcNow()
            } };
        }, cancellationToken);
        lease.SetSavedClaim(this, claimId);
        return new(this, lease, binding, selected, claimId, saved.Run.ToolAdmission!.Revision);
    }

    public async Task<AgentSavedEffectDispatchResult> CheckpointSavedEffectAsync(AgentToolRunLease lease,
        CancellationToken cancellationToken) {
        var admitted = RequireBoundSavedEffect(lease);
        var observed = await ReadSavedEffectSnapshotAsync(lease, cancellationToken);
        var selected = AgentToolJournalTransitions.RequireProposal(RequireJournal(observed.Run), admitted.Binding);
        if (selected.State is not (AgentToolProposalState.Completed or AgentToolProposalState.Rejected) ||
            selected.State == AgentToolProposalState.Completed && selected.EffectState == AgentToolEffectState.Unknown) {
            throw Failure("The selected effect has no acknowledged terminal result; receipt reconciliation is required.");
        }
        var saved = await writer.UpdateExecutionRunDetailAsync(lease.Session.ExecutionRunId, current => {
            RequireBoundSavedEffect(lease);
            var journal = RequireSavedOriginal(observed.Run, current, compareRevision: true);
            RequireCurrent(journal.Session, new(current.Run, current.ChatSession), AgentToolRunLeaseMode.SavedEffectDispatch);
            RequireLease(lease, journal);
            if (AgentToolJournalTransitions.RequireProposal(journal, admitted.Binding) != selected) {
                throw Failure("The selected outcome changed before its saved-effect checkpoint.");
            }
            var changed = journal with { Revision = checked(journal.Revision + 1) };
            AgentToolJournalTransitions.ValidatePersistence(journal, changed);
            return current with { Run = current.Run with {
                ToolAdmission = changed, State = ExecutionState.WaitingOnTool,
                Revision = checked(current.Run.Revision + 1), UpdatedAtUtc = clock.GetUtcNow()
            } };
        }, cancellationToken);
        return new(admitted.Binding, selected.State, selected.EffectState, saved.Run.Revision, saved.Run.ToolAdmission!.Revision);
    }

    public async Task<ExecutionRunDetail> CancelSavedEffectsAsync(AgentSavedEffectCancellationRequest request,
        Func<AgentToolSessionObservation, CancellationToken, ValueTask<bool>> currentRead, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(currentRead);
        var original = await RequireSavedRequestAsync(request.ExecutionRunId, request.ChatSessionId,
            request.ExpectedRunRevision, request.ExpectedJournalRevision, cancellationToken);
        await RequireSavedReadAsync(original, currentRead, cancellationToken);
        return await writer.UpdateExecutionRunDetailAsync(request.ExecutionRunId, current => {
            var journal = RequireSavedOriginal(original, current, compareRevision: true);
            RequireCurrent(journal.Session, new(current.Run, current.ChatSession), AgentToolRunLeaseMode.SavedEffectDispatch);
            var now = clock.GetUtcNow();
            var decisions = current.Run.PendingApprovals.Select(item => new PendingToolApprovalDecision(item.ApprovalId, false) {
                ToolAdmission = item.ToolAdmission
            }).ToImmutableArray();
            var approvals = ExecutionRunStateTransitions.ApplyApprovalDecision(current.Approvals, current.Run, decisions,
                now, AgentPendingApprovalCancellation.Phase, current.Run.Id.ToString("D")).RunApprovals;
            var changed = AgentToolJournalTransitions.RejectUndispatchedOnCancellation(journal);
            changed = changed with { Revision = checked(changed.Revision + 1) };
            AgentToolJournalTransitions.ValidatePersistence(journal, changed);
            var log = new ExecutionLogEntry(Guid.NewGuid(), current.Run.AgentId, current.Run.ChatSessionId, now,
                ExecutionState.Failed, AgentPendingApprovalCancellation.Phase, AgentPendingApprovalCancellation.Summary) {
                ExecutionRunId = current.Run.Id
            };
            return current with {
                Run = current.Run with {
                    ToolAdmission = changed, State = ExecutionState.Failed, Outcome = RunOutcome.Cancelled,
                    PendingApprovals = [], RuntimeSessionKey = string.Empty, SerializedSessionStateJson = null,
                    ResultSummary = AgentPendingApprovalCancellation.Summary, CompletedAtUtc = now,
                    Revision = checked(current.Run.Revision + 1), UpdatedAtUtc = now
                },
                Approvals = approvals,
                ChatSession = current.ChatSession! with { UpdatedAtUtc = now, Compatibility = null },
                ExecutionLog = [log, .. current.ExecutionLog]
            };
        }, cancellationToken);
    }

    private async Task<ExecutionRunRecord> RequireSavedRequestAsync(Guid runId, Guid? chatId, long runRevision,
        long journalRevision, CancellationToken cancellationToken) {
        var snapshot = await RequireSnapshotAsync(runId, cancellationToken);
        var run = snapshot.Run with { PendingApprovals = snapshot.Run.PendingApprovals.ToImmutableArray() };
        var journal = RequireJournal(run);
        RequireSavedJournal(run, journal);
        RequireCurrent(journal.Session, new(run, snapshot.ChatSession), AgentToolRunLeaseMode.SavedEffectDispatch);
        if (run.ChatSessionId != chatId || run.Revision != runRevision || journal.Revision != journalRevision) {
            throw Failure("The displayed saved-effect run or journal revision changed. Read the current state before another explicit action.");
        }
        return run;
    }

    private AgentSavedEffectAdmission RequireBoundSavedEffect(AgentToolRunLease lease) {
        var admitted = lease.RequireSavedEffect(this);
        if (!ReferenceEquals(AgentToolRunLease.Current, lease)) {
            throw Failure("The original saved-effect lease must be bound to this operation.");
        }
        return admitted;
    }

    private async Task<ExecutionRunAdmissionSnapshot> ReadSavedEffectSnapshotAsync(AgentToolRunLease lease,
        CancellationToken cancellationToken) {
        var admitted = RequireBoundSavedEffect(lease);
        var snapshot = await RequireSnapshotAsync(lease.Session.ExecutionRunId, cancellationToken);
        var journal = RequireSavedOriginal(admitted.Run, new(snapshot.Run, snapshot.ChatSession, [], []), compareRevision: false);
        RequireCurrent(journal.Session, snapshot, AgentToolRunLeaseMode.SavedEffectDispatch);
        RequireLease(lease, journal);
        await RequireSavedReadAsync(snapshot.Run, admitted.CurrentRead, cancellationToken);
        lease.RequireOwner(this);
        return snapshot;
    }

    private async Task RequireSavedReadAsync(ExecutionRunRecord run,
        Func<AgentToolSessionObservation, CancellationToken, ValueTask<bool>> currentRead, CancellationToken cancellationToken) {
        await RequireEffectReadPolicyAsync(run, new(run.ToolAdmission!.Session,
            AgentTurnContextMetadata.TryReadTurnContextReference(run.MetadataJson),
            AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(run.MetadataJson)), currentRead,
            recheckAfterBackground: false, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private async ValueTask<AgentToolReceiptReconciliationAdmission> RequireSavedEffectReceiptAsync(
        AgentToolReceiptReconciliationClaim claim, CancellationToken cancellationToken) {
        claim.RequireOwner(this);
        var admitted = RequireBoundSavedEffect(claim.Lease);
        if (claim.Purpose != AgentToolReceiptReconciliationPurpose.PreparedSavedEffect || claim.Binding != admitted.Binding) {
            throw Failure("The receipt preflight must name this lease's one original prepared effect.");
        }
        var snapshot = await ReadSavedEffectSnapshotAsync(claim.Lease, cancellationToken);
        var journal = RequireJournal(snapshot.Run);
        var proposal = RequireSelectedPrepared(journal, admitted.Binding);
        return new(journal.Session, admitted.Binding, proposal.Payload);
    }

    private async Task RecordSavedEffectOutcomeAsync(AgentToolInvocationClaim claim,
        Func<AgentToolProposalRecord, AgentToolProposalRecord> update, CancellationToken cancellationToken,
        bool preserveUncertainty = false) {
        claim.RequireOwner(this);
        var admitted = RequireBoundSavedEffect(claim.RunLease);
        await writer.UpdateExecutionRunDetailAsync(claim.RunLease.Session.ExecutionRunId, current => {
            claim.RequireOwner(this);
            var journal = RequireSavedOriginal(admitted.Run, current, compareRevision: false);
            RequireLease(claim.RunLease, journal);
            var proposal = AgentToolJournalTransitions.RequireProposal(journal, claim.Binding);
            if (preserveUncertainty && proposal.State == AgentToolProposalState.Completed && proposal.DispatchClaimId == claim.Id) {
                return current;
            }
            RequireClaim(claim, proposal);
            var changed = AgentToolJournalTransitions.Replace(journal, claim.Binding, update);
            return current with { Run = current.Run with {
                ToolAdmission = changed, Revision = checked(current.Run.Revision + 1), UpdatedAtUtc = clock.GetUtcNow()
            } };
        }, cancellationToken);
    }

    private static AgentToolJournalRecord RequireSavedOriginal(ExecutionRunRecord original, ExecutionRunDetail current,
        bool compareRevision) {
        var run = current.Run;
        var journal = RequireJournal(run);
        RequireSavedJournal(run, journal);
        if (run.Id != original.Id || run.AgentId != original.AgentId || run.ChatSessionId != original.ChatSessionId ||
            run.CreatedAtUtc != original.CreatedAtUtc || run.MetadataJson != original.MetadataJson ||
            run.SourceKind != original.SourceKind || run.SourceId != original.SourceId ||
            journal.Session != original.ToolAdmission!.Session ||
            compareRevision && (run.Revision != original.Revision || journal.Revision != original.ToolAdmission.Revision ||
                !run.PendingApprovals.SequenceEqual(original.PendingApprovals))) {
            throw Failure("The original saved-effect authority, decisions or exact checkpoint changed.");
        }
        return journal;
    }

    private static void RequireSavedJournal(ExecutionRunRecord run, AgentToolJournalRecord journal) {
        if (journal.SchemaVersion != AgentToolJournalRecord.EffectJournalSchemaVersion ||
            journal.Support != AgentToolAdmissionSupport.EffectJournalOnly || journal.Session.Reference.BackgroundSource is not null ||
            !string.IsNullOrEmpty(run.ProcessRunId) || journal.Segments.Length != 1 ||
            journal.Segments[0].CheckpointKind != AgentToolCheckpointKind.EffectsOnly) {
            throw Failure("Saved effect dispatch requires an original interactive schema-five effects-only admission.");
        }
    }

    private static void RequirePreparedOwnerEffect(AgentToolProposalRecord proposal) {
        if (proposal.State != AgentToolProposalState.Prepared || proposal.ProviderCall is not null ||
            proposal.Payload.Effect != AgentToolProposalEffect.Mutation || proposal.Payload.Recovery != AgentToolProposalRecovery.OwnerReceipt ||
            proposal.Payload.SourcePreparation is null || proposal.DispatchClaimId is not null) {
            throw Failure("Only an original prepared owner-receipted effect can acquire saved mutation authority; entered effects require receipt reconciliation.");
        }
    }

    private static void RequireSavedMutationCeiling(ExecutionRunRecord run) {
        if (AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(run.MetadataJson) is not { MutationAllowed: true }) {
            throw Failure("The original execution authority did not permit mutation.");
        }
    }

    private static AgentToolProposalRecord RequireSelectedPrepared(AgentToolJournalRecord journal, AgentToolApprovalBinding binding) {
        var selected = AgentToolJournalTransitions.RequireProposal(journal, binding);
        RequirePreparedOwnerEffect(selected);
        if (selected.ApprovalStatus != ExecutionApprovalStatus.Approved || selected.ApprovedDigest != selected.Payload.Digest) {
            throw Failure("The original selected effect has not been approved exactly.");
        }
        foreach (var batch in journal.Batches) {
            foreach (var proposal in batch.Proposals) {
                if (proposal.IntentId == selected.IntentId) {
                    return selected;
                }
                if (proposal.State is not (AgentToolProposalState.Completed or AgentToolProposalState.Rejected or
                    AgentToolProposalState.Cancelled or AgentToolProposalState.OwnerReceiptConfirmed)) {
                    throw Failure("The selected effect cannot bypass an earlier unresolved original proposal.");
                }
            }
        }
        throw Failure("The selected effect was not retained in the original ordered journal.");
    }
}
