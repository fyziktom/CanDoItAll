using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed partial class AgentToolAdmissionJournal : IAgentToolAdmissionVerifier {
    private readonly ISandboxWorkspaceExecutionRunStore reader;
    private readonly ISandboxWorkspaceExecutionRunMutationStore writer;
    private readonly ISandboxWorkspaceExecutionRunLeaseStore leases;
    private readonly AgentToolProfileBinding profile;
    private readonly TimeProvider clock;
    private readonly AgentChatContextAttachmentPersistence contextAttachments;

    public AgentToolAdmissionJournal(ISandboxWorkspaceStore store, AgentToolProfileBinding profile, TimeProvider? clock = null,
        IEnumerable<IAgentToolBackgroundSourcePolicy>? backgroundSources = null,
        IEnumerable<IAgentChatContextAttachmentCodec>? contextAttachmentCodecs = null) {
        reader = store as ISandboxWorkspaceExecutionRunStore
            ?? throw new InvalidOperationException("Tool admission requires the canonical execution-run reader.");
        writer = store as ISandboxWorkspaceExecutionRunMutationStore
            ?? throw new InvalidOperationException("Tool admission requires atomic current-state run mutations.");
        leases = store as ISandboxWorkspaceExecutionRunLeaseStore
            ?? throw new InvalidOperationException("Tool admission requires a real cross-instance run dispatch lease.");
        this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
        this.clock = clock ?? TimeProvider.System;
        contextAttachments = new(contextAttachmentCodecs);
        this.backgroundSources = (backgroundSources ?? []).ToDictionary(item => item.SourceKind, StringComparer.OrdinalIgnoreCase);
    }

    public bool CanRecoverRuntimeContext(AgentRuntimeTransientContext? context) => contextAttachments.CanCapture(context);

    internal TimeProvider Clock => clock;
    internal AgentToolProfileBinding Profile => profile;

    public AgentRuntimeTransientContext RestoreRuntimeContext(AgentToolAdmittedRuntimeContext context) => contextAttachments.Restore(context);

    public AgentToolJournalRecord CreateForNewRun(ExecutionRunRecord run, ChatSessionRecord chat,
        AgentToolAdmissionSupport support = AgentToolAdmissionSupport.Recoverable,
        AgentToolOriginalInput? originalInput = null, AgentRuntimeTransientContext? runtimeContext = null) {
        if (run.ToolAdmission is not null || run.PendingApprovals.Count != 0 || run.Revision != 1 ||
            run.State != ExecutionState.Preparing || run.ChatSessionId != chat.Id) {
            throw new AgentToolAdmissionException("tool-admission.legacy-run",
                "Tool admission must be created by the owner with a new execution run; existing runs cannot acquire fabricated effect identity.");
        }

        var governance = AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(run.MetadataJson)
            ?? throw Failure("The new run has no persisted execution authority.");
        var session = new AgentToolSessionAdmission(new(run.Id, chat.Id, governance.AuthorityId), run.AgentId,
            AgentRuntimeContextPurpose.InteractiveChat, profile);
        RequireCurrent(session, new(run, chat));
        if (originalInput is not null && !chat.Messages.Any(message => message.Id == originalInput.MessageId &&
                message.Role == ChatMessageRole.User && message.Content == originalInput.Content)) {
            throw Failure("The admitted input does not match the new run's persisted user message.");
        }

        if (runtimeContext is not null && (!contextAttachments.CanCapture(runtimeContext) ||
                AgentChatContextDigest.Compute(runtimeContext) != ExecutionInvocationMetadata.ResolveTransientContextDigest(run))) {
            throw Failure("The saved runtime context does not match its admitted digest or requires an unsupported typed attachment.");
        }

        var savedContext = runtimeContext is null ? null : contextAttachments.Capture(runtimeContext);
        var journal = new AgentToolJournalRecord(savedContext is { Attachments.IsDefaultOrEmpty: false }
                ? AgentToolJournalRecord.TypedContextSchemaVersion : AgentToolJournalRecord.CurrentSchemaVersion,
            1, session, [], [], Support: support, OriginalInput: originalInput, RuntimeContext: savedContext);
        journal.Validate();
        return journal;
    }

    public async ValueTask<AgentToolRunLease> AcquireRunAsync(AgentToolSessionReference reference, CancellationToken cancellationToken) {
        var handle = await leases.AcquireToolDispatchLeaseAsync(reference.ExecutionRunId, cancellationToken);
        try {
            var lease = new AgentToolRunLease(this, reference, Guid.NewGuid(), handle);
            await MutateAsync(lease, requireClaim: false, journal => journal with {
                Revision = checked(journal.Revision + 1), ActiveDispatchLeaseId = lease.Id
            }, cancellationToken, backgroundCheck: BackgroundCheck.Read);
            return lease;
        } catch {
            await handle.DisposeAsync();
            throw;
        }
    }

    public AgentToolJournalRecord CreateForNewPlanningRun(ExecutionRunRecord run, ChatSessionRecord chat,
        AgentToolOriginalInput originalInput, AgentToolProtocolEnvelope originalTarget, AgentPlanningPolicyProvenance provenance) {
        ArgumentNullException.ThrowIfNull(originalInput);
        var admitted = CreateForNewRun(run, chat, originalInput: originalInput);
        return CreatePlanningJournal(admitted, originalTarget, provenance);
    }

    public AgentToolJournalRecord CreateForNewPlanningRun(ExecutionRunRecord run, ChatSessionRecord chat,
        AgentToolOriginalInput originalInput, AgentPlanningRunAdmission preparation) {
        ArgumentNullException.ThrowIfNull(originalInput);
        ArgumentNullException.ThrowIfNull(preparation);
        var admitted = CreateForNewRun(run, chat, originalInput: originalInput);
        return CreatePlanningJournal(admitted, preparation.BindOriginalTarget(admitted.Session), preparation.Provenance);
    }

    private AgentToolJournalRecord CreatePlanningJournal(AgentToolJournalRecord admitted, AgentToolProtocolEnvelope originalTarget,
        AgentPlanningPolicyProvenance provenance) {
        var planning = admitted with {
            SchemaVersion = AgentToolJournalRecord.PlanningSchemaVersion,
            Support = AgentToolAdmissionSupport.EffectJournalOnly,
            PlanningDecision = AgentPlanningDecisionTransitions.Create(admitted.Session.Reference, originalTarget, provenance, clock.GetUtcNow())
        };
        planning.Validate();
        return planning;
    }

    internal async Task<AgentToolRunLease> AcquirePlanningStepAsync(AgentPlanningDecisionOwner owner,
        AgentPlanningDecisionExpectation expected, Func<AgentToolSessionObservation, CancellationToken, ValueTask<bool>> currentRead,
        CancellationToken cancellationToken, bool inherited = false) {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(currentRead);
        var snapshot = await RequireSnapshotAsync(expected.ExecutionRunId, cancellationToken);
        var original = snapshot.Run with { PendingApprovals = snapshot.Run.PendingApprovals.ToImmutableArray() };
        RequirePlanningExpectation(original, expected, inherited);
        RequirePlanningStart(original, inherited);
        RequireCurrent(original.ToolAdmission!.Session, snapshot);
        await RequireSavedReadAsync(original, currentRead, cancellationToken);
        if (inherited) {
            await RequireBackgroundDispatchAsync(original, cancellationToken);
        }
        var handle = await leases.AcquireToolDispatchLeaseAsync(original.Id, cancellationToken);
        try {
            var lease = new AgentToolRunLease(this, original.ToolAdmission.Session.Reference, Guid.NewGuid(), handle);
            long started = 0;
            var saved = await writer.UpdateExecutionRunDetailAsync(original.Id, current => {
                var journal = RequireOriginalRun(original, current, compareRevision: true);
                RequirePlanningExpectation(current.Run, expected, inherited);
                RequirePlanningStart(current.Run, inherited);
                RequireCurrent(journal.Session, new(current.Run, current.ChatSession));
                var now = clock.GetUtcNow();
                started = clock.GetTimestamp();
                var changed = journal with {
                    Revision = checked(journal.Revision + 1), ActiveDispatchLeaseId = lease.Id,
                    PlanningDecision = AgentPlanningDecisionTransitions.BeginStep(journal.PlanningDecision!, lease.Id, now)
                };
                AgentToolJournalTransitions.ValidatePersistence(journal, changed);
                return current with { Run = current.Run with {
                    ToolAdmission = changed, State = ExecutionState.Running,
                    Revision = checked(current.Run.Revision + 1), UpdatedAtUtc = now
                } };
            }, cancellationToken);
            lease.AdmitPlanningWork(this, new(owner, lease, saved.Run, started));
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

    private static void RequirePlanningStart(ExecutionRunRecord run, bool inherited) {
        var journal = RequireJournal(run);
        var decision = journal.PlanningDecision!;
        if (decision.Work.Open is not null) {
            throw new AgentPlanningAdmissionException(decision.DecisionId, decision.CurrentGeneration,
                AgentPlanningAdmissionRefusal.InterruptedWorkAccountingUnavailable);
        }
        var originMatches = inherited
            ? decision.InheritedOrigin is not null && journal.Session.Reference.BackgroundSource is not null &&
                journal.BackgroundInput is not null && journal.OriginalInput is null && run.ChatSessionId is null
            : decision.InheritedOrigin is null && journal.OriginalInput is not null &&
                journal.Session.Reference.BackgroundSource is null && string.IsNullOrEmpty(run.ProcessRunId);
        if (!originMatches || run.State != ExecutionState.Preparing || run.Outcome is not null || run.PendingApprovals.Count != 0 ||
            journal.Support != AgentToolAdmissionSupport.EffectJournalOnly || journal.HasUnresolvedEffects) {
            throw Failure("A new planning step requires its original admitted preparation without pending or uncertain effects.");
        }
    }

    internal async Task<ExecutionRunAdmissionSnapshot> ReadPlanningSnapshotAsync(AgentToolRunLease lease, CancellationToken cancellationToken) {
        RequireBoundPlanningLease(lease);
        var snapshot = await ReadVerifiedSnapshotAsync(lease, reconciliationOnly: false, cancellationToken);
        if (snapshot.Run.ToolAdmission?.PlanningDecision is null) {
            throw Failure("This run has no original planning decision.");
        }
        return snapshot;
    }

    internal async Task<ExecutionRunRecord> UpdatePlanningAsync(AgentToolRunLease lease, ExecutionRunRecord original,
        AgentPlanningDecisionExpectation? expected, bool outcomeOnly,
        Func<ExecutionRunRecord, ExecutionRunRecord> update, CancellationToken cancellationToken) {
        RequireBoundPlanningLease(lease);
        if (!outcomeOnly && original.ToolAdmission?.PlanningDecision?.InheritedOrigin is not null) {
            await RequireBackgroundDispatchAsync(original, cancellationToken);
        }
        var saved = await writer.UpdateExecutionRunDetailAsync(lease.Session.ExecutionRunId, current => {
            RequireBoundPlanningLease(lease);
            var run = current.Run;
            var journal = RequireJournal(run);
            if (journal.SchemaVersion is not (AgentToolJournalRecord.PlanningSchemaVersion or AgentToolJournalRecord.InheritedPlanningSchemaVersion) ||
                journal.SchemaVersion != original.ToolAdmission?.SchemaVersion ||
                journal.Session != original.ToolAdmission?.Session || journal.Session.Profile != profile ||
                run.Id != original.Id || run.AgentId != original.AgentId || run.ChatSessionId != original.ChatSessionId ||
                run.CreatedAtUtc != original.CreatedAtUtc || run.SourceKind != original.SourceKind ||
                run.SourceId != original.SourceId || run.MetadataJson != original.MetadataJson) {
                throw Failure("The original planning run, source or authority changed.");
            }
            if (!outcomeOnly) {
                RequireCurrent(journal.Session, new(run, current.ChatSession), lease.Mode);
            }
            RequireLease(lease, journal);
            if (expected is not null && expected != AgentPlanningDecisionExpectation.From(run)) {
                throw Failure("The displayed planning decision or revision changed. Reload before applying this action.");
            }
            var changed = update(run);
            AgentToolJournalTransitions.ValidatePersistence(journal, changed.ToolAdmission);
            var observed = clock.GetUtcNow();
            var updatedAt = outcomeOnly && (observed.Offset != TimeSpan.Zero || observed < run.UpdatedAtUtc)
                ? run.UpdatedAtUtc : observed;
            return current with { Run = changed with { Revision = checked(run.Revision + 1), UpdatedAtUtc = updatedAt } };
        }, cancellationToken);
        return saved.Run;
    }

    private void RequireBoundPlanningLease(AgentToolRunLease lease) {
        lease.RequireOwner(this);
        if (!ReferenceEquals(AgentToolRunLease.Current, lease) ||
            lease.Mode != AgentToolRunLeaseMode.Dispatch &&
                (lease.Mode != AgentToolRunLeaseMode.SavedEffectDispatch || lease.PlanningWork is null)) {
            throw Failure("Planning work requires the bound original live dispatch lease.");
        }
    }

    internal async Task<ExecutionRunDetail> ChangeClosedPlanningAsync(AgentPlanningDecisionExpectation expected,
        Func<AgentToolSessionObservation, CancellationToken, ValueTask<bool>> currentRead,
        Func<ExecutionRunDetail, ExecutionRunDetail> change, CancellationToken cancellationToken,
        AgentInheritedSavedEffectContinuation? parent = null) {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(currentRead);
        var snapshot = await RequireSnapshotAsync(expected.ExecutionRunId, cancellationToken);
        var original = snapshot.Run with { PendingApprovals = snapshot.Run.PendingApprovals.ToImmutableArray() };
        RequirePlanningExpectation(original, expected, inherited: parent is not null);
        RequireClosedPlanning(original);
        await RequireSavedReadAsync(original, currentRead, cancellationToken, parent);
        if (parent?.ReviewIntent is not null) {
            await parent.RequireCurrentManagerAsync(cancellationToken);
        }
        await using var handle = await leases.AcquireToolDispatchLeaseAsync(original.Id, cancellationToken);
        await RequireSavedReadAsync(original, currentRead, cancellationToken, parent);
        if (parent?.ReviewIntent is not null) {
            await parent.RequireCurrentManagerAsync(cancellationToken);
        }
        return parent?.ReviewIntent is null
            ? await writer.UpdateExecutionRunDetailAsync(original.Id, Change, cancellationToken)
            : await writer.UpdateExecutionRunDetailAsync(original.Id, Change, parent.RequireCurrentManagerAsync, cancellationToken);

        ExecutionRunDetail Change(ExecutionRunDetail current) {
            var journal = RequireSavedOriginal(original, current, compareRevision: true, parent);
            RequireCurrent(journal.Session, new(current.Run, current.ChatSession), AgentToolRunLeaseMode.SavedEffectDispatch);
            RequirePlanningExpectation(current.Run, expected, inherited: parent is not null);
            RequireClosedPlanning(current.Run);
            var changed = change(current);
            AgentToolJournalTransitions.ValidatePersistence(journal, changed.Run.ToolAdmission);
            return changed with { Run = changed.Run with {
                Revision = checked(current.Run.Revision + 1), UpdatedAtUtc = clock.GetUtcNow()
            } };
        }
    }

    private static void RequirePlanningExpectation(ExecutionRunRecord run, AgentPlanningDecisionExpectation expected, bool inherited = false) {
        if (run.ToolAdmission?.SchemaVersion != (inherited ? AgentToolJournalRecord.InheritedPlanningSchemaVersion : AgentToolJournalRecord.PlanningSchemaVersion) ||
            AgentPlanningDecisionExpectation.From(run) != expected) {
            throw Failure("The displayed planning decision or revision changed. Reload before applying this action.");
        }
    }

    private static void RequireClosedPlanning(ExecutionRunRecord run) {
        var decision = run.ToolAdmission!.PlanningDecision!;
        if (decision.Work.Open is not null) {
            throw new AgentPlanningAdmissionException(decision.DecisionId, decision.CurrentGeneration,
                AgentPlanningAdmissionRefusal.InterruptedWorkAccountingUnavailable);
        }
        if (decision.Current.State is not (AgentPlanningGenerationState.AwaitingApproval or AgentPlanningGenerationState.PausedBasisChanged)) {
            throw new AgentPlanningAdmissionException(decision.DecisionId, decision.CurrentGeneration,
                AgentPlanningAdmissionRefusal.DecisionInactive);
        }
    }

    public async Task<AgentToolJournalRecord> ReadAsync(AgentToolRunLease lease, CancellationToken cancellationToken) {
        return await ReadVerifiedAsync(lease, reconciliationOnly: false, cancellationToken);
    }

    private async Task<AgentToolJournalRecord> ReadVerifiedAsync(AgentToolRunLease lease, bool reconciliationOnly,
        CancellationToken cancellationToken)
        => RequireJournal((await ReadVerifiedSnapshotAsync(lease, reconciliationOnly, cancellationToken)).Run);

    private async Task<ExecutionRunAdmissionSnapshot> ReadVerifiedSnapshotAsync(AgentToolRunLease lease, bool reconciliationOnly,
        CancellationToken cancellationToken) {
        lease.RequireOwner(this);
        if (!reconciliationOnly && lease.Mode == AgentToolRunLeaseMode.SavedEffectDispatch) {
            return await ReadSavedEffectSnapshotAsync(lease, cancellationToken);
        }
        RequireLeaseMode(lease, reconciliationOnly);
        var detail = await RequireSnapshotAsync(lease.Session.ExecutionRunId, cancellationToken);
        var journal = RequireJournal(detail.Run);
        RequireCurrent(journal.Session, detail, reconciliationOnly);
        if (!reconciliationOnly) {
            await RequireBackgroundSourceAsync(detail.Run, readOnly: true, cancellationToken);
        }
        RequireLease(lease, journal);
        return detail;
    }

    public Task<AgentToolJournalRecord> BeginSegmentAsync(AgentToolRunLease lease,
        AgentToolProtocolEnvelope checkpoint, Guid? continuesSegmentId, CancellationToken cancellationToken)
        => MutateAsync(lease, true, journal => {
            RequireRecoverable(journal);
            var existing = journal.Segments.SingleOrDefault(segment => segment.ContinuesSegmentId == continuesSegmentId);
            if (existing is not null) {
                return journal;
            }

            if (journal.Segments.Length == 0 && continuesSegmentId is not null ||
                journal.Segments.Length != 0 && journal.Segments[^1].Id != continuesSegmentId) {
                throw Failure("An SDK continuation must follow the latest saved invocation segment.");
            }

            if (continuesSegmentId is not null) {
                var previous = journal.Segments[^1];
                if (previous.ApprovalCheckpoint is null || previous.PendingApprovals.Any(approval =>
                        AgentToolJournalTransitions.RequireProposal(journal, approval.ToolAdmission
                            ?? throw Failure("The saved SDK approval has no admitted proposal.")).ApprovalStatus == ExecutionApprovalStatus.Pending)) {
                    throw Failure("The exact saved approval decisions must commit before SDK continuation.");
                }
            }

            var segment = new AgentToolInvocationSegment(Guid.NewGuid(), journal.Segments.Length,
                journal.Batches.Length, checkpoint, continuesSegmentId, PendingApprovals: []);
            return journal with { Revision = checked(journal.Revision + 1), Segments = journal.Segments.Add(segment) };
        }, cancellationToken);

    public async Task<IReadOnlyList<PendingToolApprovalRecord>> SaveApprovalCheckpointAsync(AgentToolRunLease lease,
        Guid segmentId, AgentToolProtocolEnvelope checkpoint, string serializedSessionStateJson,
        IReadOnlyList<PendingToolApprovalRecord> approvals, CancellationToken cancellationToken) {
        lease.RequireOwner(this);
        RequireLeaseMode(lease, reconciliationOnly: false);
        IReadOnlyList<PendingToolApprovalRecord>? bound = null;
        if (lease.Session.BackgroundSource is not null) {
            await RequireBackgroundSourceAsync((await RequireSnapshotAsync(lease.Session.ExecutionRunId, cancellationToken)).Run,
                readOnly: false, cancellationToken);
        }
        await writer.UpdateExecutionRunDetailAsync(lease.Session.ExecutionRunId, detail => {
            var journal = RequireJournal(detail.Run);
            RequireCurrent(journal.Session, new(detail.Run, detail.ChatSession));
            RequireLease(lease, journal);
            var segment = journal.Segments.Single(item => item.Id == segmentId);
            if (segment.CheckpointKind != AgentToolCheckpointKind.ProviderRestart ||
                segment.Ordinal != journal.Segments.Length - 1 || approvals.Count == 0) {
                throw Failure("Only the current SDK invocation can publish pending approvals.");
            }

            var proposals = journal.Batches.Skip(segment.FirstBatchOrdinal).SelectMany(batch =>
                batch.Proposals.Select(proposal => (Batch: batch.Id, Proposal: proposal))).ToArray();
            bound = approvals.Select(approval => {
                var match = proposals.SingleOrDefault(item => item.Proposal.CallId == approval.CallId &&
                    item.Proposal.Payload.ToolName == approval.ToolName && item.Proposal.ApprovalStatus == ExecutionApprovalStatus.Pending);
                if (match.Proposal is null) {
                    throw Failure("The SDK approval is not an exact pending proposal in this invocation.");
                }

                return approval with { ToolAdmission = Binding(match.Batch, match.Proposal) };
            }).ToArray();
            var changed = AgentToolJournalTransitions.BindApprovals(journal, bound);
            changed = changed with {
                Revision = checked(changed.Revision + 1),
                Segments = changed.Segments.SetItem(segment.Ordinal, segment with {
                    ApprovalCheckpoint = checkpoint, PendingApprovals = bound.ToImmutableArray()
                })
            };
            AgentToolJournalTransitions.ValidatePersistence(journal, changed);
            return detail with { Run = detail.Run with {
                ToolAdmission = changed, PendingApprovals = bound, SerializedSessionStateJson = serializedSessionStateJson,
                State = ExecutionState.WaitingOnTool, Revision = checked(detail.Run.Revision + 1), UpdatedAtUtc = clock.GetUtcNow()
            } };
        }, cancellationToken);
        return bound ?? throw Failure("The SDK approval checkpoint was not saved.");
    }

    public Task<AgentToolJournalRecord> AdmitBatchAsync(AgentToolRunLease lease, AgentToolSemanticDigest requestDigest,
        AgentToolProtocolEnvelope response, IReadOnlyList<AgentToolPreparedCall> calls, CancellationToken cancellationToken)
        => MutateAsync(lease, true, journal => {
            RequireLiveEffectOrigin(lease, journal);
            if (journal.Segments.Length == 0 || journal.HasUnresolvedEffects) {
                throw Failure("Restore and drain the saved tool batch before admitting another provider proposal.");
            }

            var batch = CreateBatch(journal, requestDigest, response, calls);
            return journal with { Revision = checked(journal.Revision + 1), Batches = journal.Batches.Add(batch) };
        }, cancellationToken);

    public async Task<AgentToolInvocationClaim> ClaimInvocationAsync(AgentToolRunLease lease, AgentToolBatchId batchId,
        string callId, AgentToolPreparedPayload payload, CancellationToken cancellationToken) {
        var claimId = Guid.NewGuid();
        AgentToolProposalRecord? selected = null;
        var backgroundCheck = BackgroundCheck.Dispatch;
        if (lease.Session.BackgroundSource is not null) {
            var observed = await ReadAsync(lease, cancellationToken);
            var prior = observed.Batches.SingleOrDefault(item => item.Id == batchId)?.Proposals.SingleOrDefault(item => item.CallId == callId);
            if (prior?.State == AgentToolProposalState.Completed) {
                backgroundCheck = BackgroundCheck.Read;
            }
        }
        var journal = await MutateAsync(lease, true, current => {
            RequireLiveEffectOrigin(lease, current);
            var batch = current.Batches.SingleOrDefault(item => item.Id == batchId)
                ?? throw Failure("The current serial batch does not exist.");
            var proposal = batch.Proposals.SingleOrDefault(item => item.CallId == callId)
                ?? throw Failure("The protocol call is not part of the admitted batch.");
            if (proposal.ProviderCall is not null) {
                throw Failure("A native provider proposal cannot acquire a local function-dispatch claim.");
            }
            if (proposal.Payload != payload || proposal.ApprovalStatus != ExecutionApprovalStatus.Approved ||
                proposal.ApprovedDigest != payload.Digest) {
                throw Failure("The invocation does not match its exact approved payload.");
            }

            if (batch.Proposals.Take(proposal.Ordinal).Any(item =>
                    item.State is not (AgentToolProposalState.Completed or AgentToolProposalState.Rejected) &&
                    !(item.ProviderCall is not null && item.State == AgentToolProposalState.Prepared))) {
                throw Failure("Tool invocation cannot bypass an earlier unresolved serial proposal.");
            }

            selected = proposal;
            if (proposal.State is AgentToolProposalState.Rejected or AgentToolProposalState.Cancelled or AgentToolProposalState.OwnerReceiptConfirmed) {
                throw Failure("A rejected, cancelled or owner-confirmed proposal cannot execute.");
            }

            if (proposal.State == AgentToolProposalState.Completed) {
                return current;
            }

            if (proposal.State is AgentToolProposalState.Executing or AgentToolProposalState.ReconciliationRequired &&
                payload.Recovery == AgentToolProposalRecovery.ReconcileBeforeRetry) {
                throw new AgentToolAdmissionException("tool-admission.reconciliation-required",
                    "A previously dispatched non-idempotent effect is uncertain. Reconcile it before any retry or new provider proposal.");
            }

            return AgentToolJournalTransitions.Replace(current, Binding(batch.Id, proposal), prior => prior with {
                State = AgentToolProposalState.Executing, DispatchClaimId = claimId
            });
        }, cancellationToken, backgroundCheck: backgroundCheck);
        var admitted = selected ?? throw Failure("The current dispatch did not select a saved proposal.");
        return new(this, lease, Binding(batchId, admitted), admitted, claimId, journal.Revision);
    }

    public async Task CompleteInvocationAsync(AgentToolInvocationClaim claim, AgentToolProtocolEnvelope result,
        AgentToolEffectState effect, CancellationToken cancellationToken, bool requiresOwnerReconciliation = false,
        AgentToolProtocolEnvelope? disclosureEvidence = null) {
        claim.RequireOwner(this);
        if (claim.Proposal.State == AgentToolProposalState.Completed) {
            return;
        }

        if (claim.RunLease.Mode == AgentToolRunLeaseMode.SavedEffectDispatch) {
            await RecordSavedEffectOutcomeAsync(claim, proposal => proposal with {
                State = requiresOwnerReconciliation ? AgentToolProposalState.ReconciliationRequired : AgentToolProposalState.Completed,
                Result = result, EffectState = effect, DisclosureEvidence = disclosureEvidence
            }, cancellationToken);
            return;
        }

        await MutateAsync(claim.RunLease, true, journal => AgentToolJournalTransitions.Replace(journal, claim.Binding, proposal => {
            RequireClaim(claim, proposal);
            return proposal with {
                State = requiresOwnerReconciliation ? AgentToolProposalState.ReconciliationRequired : AgentToolProposalState.Completed,
                Result = result,
                EffectState = effect,
                DisclosureEvidence = disclosureEvidence
            };
        }), cancellationToken, backgroundCheck: BackgroundCheck.RecordOutcome);
    }

    public async Task PreserveUncertainInvocationAsync(AgentToolInvocationClaim claim, CancellationToken cancellationToken) {
        claim.RequireOwner(this);
        if (claim.Proposal.State == AgentToolProposalState.Completed) {
            return;
        }

        if (claim.RunLease.Mode == AgentToolRunLeaseMode.SavedEffectDispatch) {
            await RecordSavedEffectOutcomeAsync(claim, proposal => proposal with {
                State = AgentToolProposalState.ReconciliationRequired
            }, cancellationToken, preserveUncertainty: true);
            return;
        }

        await MutateAsync(claim.RunLease, true, journal => {
            var proposal = AgentToolJournalTransitions.RequireProposal(journal, claim.Binding);
            if (proposal.State == AgentToolProposalState.Completed && proposal.DispatchClaimId == claim.Id) {
                return journal;
            }
            RequireClaim(claim, proposal);
            return AgentToolJournalTransitions.Replace(journal, claim.Binding,
                current => current with { State = AgentToolProposalState.ReconciliationRequired });
        }, cancellationToken, backgroundCheck: BackgroundCheck.RecordOutcome);
    }

    public async ValueTask<AgentToolSessionAdmission> RequireSessionAsync(
        AgentToolSessionReference reference, CancellationToken cancellationToken = default) {
        var lease = AgentToolRunLease.Current ?? throw Failure("No active admitted runtime lease is held.");
        lease.RequireOwner(this);
        if (lease.Session != reference) {
            throw Failure("The requested session does not match the active admitted runtime.");
        }

        return (await ReadAsync(lease, cancellationToken)).Session;
    }

    public async ValueTask<AgentToolSessionObservation> RequireSessionObservationAsync(
        AgentToolSessionReference reference, CancellationToken cancellationToken = default) {
        var lease = AgentToolRunLease.Current ?? throw Failure("No active admitted runtime lease is held.");
        lease.RequireOwner(this);
        if (lease.Session != reference) {
            throw Failure("The requested session does not match the active admitted runtime.");
        }
        var detail = await ReadVerifiedSnapshotAsync(lease, reconciliationOnly: false, cancellationToken);
        return AgentToolSessionObservation.From(detail.Run);
    }

    public async ValueTask<AgentToolPriorInvocationObservation> RequirePriorInvocationsAsync(
        AgentToolSessionReference session, CancellationToken cancellationToken = default) {
        var claim = AgentToolInvocationClaim.Current ?? throw Failure("No current serial proposal is bound to this invocation.");
        claim.RequireOwner(this);
        if (claim.RunLease.Session != session) {
            throw Failure("The current serial proposal belongs to a different execution session.");
        }
        var snapshot = await ReadVerifiedSnapshotAsync(claim.RunLease, reconciliationOnly: false, cancellationToken);
        var journal = RequireJournal(snapshot.Run);
        var proposal = AgentToolJournalTransitions.RequireProposal(journal, claim.Binding);
        if (proposal.State is not (AgentToolProposalState.Executing or AgentToolProposalState.Completed) ||
            proposal.DispatchClaimId != claim.Id || proposal.Payload != claim.Proposal.Payload || proposal.IntentId != claim.Proposal.IntentId) {
            throw Failure("The current serial dispatch no longer owns this exact proposal.");
        }
        claim.RequireOwner(this);
        cancellationToken.ThrowIfCancellationRequested();
        return new(journal.Session, claim.Binding, snapshot.Run.Revision, journal.Revision,
            AgentToolPriorInvocationEvidence.Capture(journal, claim.Binding));
    }

    public async ValueTask<AgentToolPriorReceiptObservation> RequirePriorResultInvocationsAsync(AgentToolSessionReference session,
        AgentToolBusinessIntentId intent, AgentToolPreparedPayload payload, CancellationToken cancellationToken = default) {
        var lease = AgentToolRunLease.Current ?? throw Failure("No active admitted runtime lease is held.");
        lease.RequireOwner(this);
        if (lease.Session != session) {
            throw Failure("The result belongs to a different execution session.");
        }
        var snapshot = await ReadVerifiedSnapshotAsync(lease, reconciliationOnly: false, cancellationToken);
        var journal = RequireJournal(snapshot.Run);
        foreach (var batch in journal.Batches) {
            foreach (var proposal in batch.Proposals) {
                if (proposal.IntentId != intent) {
                    continue;
                }
                if (proposal.State != AgentToolProposalState.Completed || proposal.Payload != payload) {
                    throw Failure("The result does not match this exact completed proposal.");
                }
                lease.RequireOwner(this);
                cancellationToken.ThrowIfCancellationRequested();
                var binding = Binding(batch.Id, proposal);
                return new(journal.Session, binding, snapshot.Run.Revision, journal.Revision,
                    AgentToolPriorInvocationEvidence.Capture(journal, binding), null);
            }
        }
        throw Failure("The completed result is absent from this verified journal.");
    }

    internal async ValueTask<AgentToolInvocationPublicationLease> AcquireClaimPublicationAsync(AgentToolInvocationClaim claim,
        IAgentCatalogReadLease heldCatalog, CancellationToken cancellationToken) {
        claim.RequireOwner(this);
        if (heldCatalog.Scope != WorkspaceScopeDescriptor.Organization(profile.ProfileId.ToString("N"))) {
            throw Failure("The owner mutation's catalog belongs to another execution profile.");
        }
        var publication = await heldCatalog.AcquireExecutionAdmissionReadLeaseAsync(reader, claim.RunLease.Session.ExecutionRunId, cancellationToken);
        try {
            await RequireCurrentClaimIdentityAsync(claim, publication.Snapshot, cancellationToken);
            return new(this, claim, publication);
        } catch (Exception primary) {
            try {
                await publication.DisposeAsync();
            } catch (Exception cleanup) {
                throw new AggregateException(primary, cleanup);
            }
            throw;
        }
    }

    internal async ValueTask RequireCurrentClaimIdentityAsync(AgentToolInvocationClaim claim, ExecutionRunAdmissionSnapshot? protectedSnapshot,
        CancellationToken cancellationToken) {
        claim.RequireOwner(this);
        if (!ReferenceEquals(AgentToolInvocationClaim.Current, claim)) {
            throw Failure("The original claim is not bound to this invocation.");
        }
        var snapshot = protectedSnapshot ?? await RequireSnapshotAsync(claim.RunLease.Session.ExecutionRunId, cancellationToken);
        var journal = RequireJournal(snapshot.Run);
        if (claim.RunLease.Mode == AgentToolRunLeaseMode.SavedEffectDispatch) {
            var admitted = RequireBoundSavedEffect(claim.RunLease);
            journal = RequireSavedOriginal(admitted.Run, new(snapshot.Run, snapshot.ChatSession, [], []), compareRevision: false, admitted.Parent);
        }
        RequireCurrent(journal.Session, snapshot, claim.RunLease.Mode);
        RequireLease(claim.RunLease, journal);
        var proposal = AgentToolJournalTransitions.RequireProposal(journal, claim.Binding);
        RequireClaim(claim, proposal);
        if (proposal.Payload != claim.Proposal.Payload || proposal.ApprovalStatus != ExecutionApprovalStatus.Approved ||
                proposal.ApprovedDigest != proposal.Payload.Digest) {
            throw Failure("The owner mutation no longer has its exact approved proposal.");
        }
        claim.RequireOwner(this);
        cancellationToken.ThrowIfCancellationRequested();
    }

    public async ValueTask<AgentToolAdmittedInvocation> RequireInvocationAsync(AgentToolSessionReference session,
        string toolName, AgentToolSemanticDigest digest, CancellationToken cancellationToken = default) {
        var claim = AgentToolInvocationClaim.Current ?? throw Failure("No current serial proposal is bound to this invocation.");
        claim.RequireOwner(this);
        if (claim.RunLease.Session != session) {
            throw Failure("The current serial proposal belongs to a different execution session.");
        }

        var journal = await ReadAsync(claim.RunLease, cancellationToken);
        var proposal = AgentToolJournalTransitions.RequireProposal(journal, claim.Binding);
        if (proposal.Payload.ToolName != toolName || proposal.Payload.Digest != digest ||
            proposal.State is not (AgentToolProposalState.Executing or AgentToolProposalState.Completed) ||
            proposal.State == AgentToolProposalState.Executing && proposal.DispatchClaimId != claim.Id) {
            throw Failure("The active serial dispatch no longer owns this exact proposal.");
        }

        return new(session, claim.Binding.BatchId, proposal.IntentId, proposal.Payload, proposal.ApprovalStatus, proposal.ApprovedDigest);
    }

    private async Task<AgentToolJournalRecord> MutateAsync(AgentToolRunLease lease, bool requireClaim,
        Func<AgentToolJournalRecord, AgentToolJournalRecord> update, CancellationToken cancellationToken,
        bool reconciliationOnly = false, BackgroundCheck backgroundCheck = BackgroundCheck.Dispatch) {
        using var activity = AgentFrameworkTelemetry.ActivitySource.StartActivity("tool.journal.mutate");
        lease.RequireOwner(this);
        RequireLeaseMode(lease, reconciliationOnly);
        if (lease.Session.BackgroundSource is not null && backgroundCheck != BackgroundCheck.RecordOutcome) {
            await RequireBackgroundSourceAsync((await RequireSnapshotAsync(lease.Session.ExecutionRunId, cancellationToken)).Run,
                reconciliationOnly || backgroundCheck == BackgroundCheck.Read, cancellationToken);
        }
        var detail = await writer.UpdateExecutionRunDetailAsync(lease.Session.ExecutionRunId, current => {
            var journal = RequireJournal(current.Run);
            RequireCurrent(journal.Session, new(current.Run, current.ChatSession), reconciliationOnly);
            if (requireClaim) {
                RequireLease(lease, journal);
            }

            var changed = update(journal);
            AgentToolJournalTransitions.ValidatePersistence(journal, changed);
            return ReferenceEquals(journal, changed) ? current : current with { Run = current.Run with {
                ToolAdmission = changed, Revision = checked(current.Run.Revision + 1), UpdatedAtUtc = clock.GetUtcNow()
            } };
        }, cancellationToken);
        return RequireJournal(detail.Run);
    }

    private void RequireCurrent(AgentToolSessionAdmission session, ExecutionRunAdmissionSnapshot detail, bool reconciliationOnly = false)
        => RequireCurrent(session, detail, reconciliationOnly ? AgentToolRunLeaseMode.CancelledReceiptReconciliation : AgentToolRunLeaseMode.Dispatch);

    private void RequireCurrent(AgentToolSessionAdmission session, ExecutionRunAdmissionSnapshot detail, AgentToolRunLeaseMode mode) {
        var run = detail.Run;
        if (session.Reference.BackgroundSource is { } background) {
            RequireBackgroundIdentity(session, run, background);
            if (detail.ChatSession is not null || !AllowsState(run, mode)) {
                throw Failure("The background execution cannot dispatch in its current terminal or chat state.");
            }
            return;
        }
        var chat = detail.ChatSession;
        var authority = AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(run.MetadataJson);
        if (session.Profile != profile || session.Purpose != AgentRuntimeContextPurpose.InteractiveChat ||
            run.Id != session.Reference.ExecutionRunId || run.AgentId != session.AgentId ||
            run.ChatSessionId != session.Reference.ChatSessionId || chat is null || chat.Id != session.Reference.ChatSessionId ||
            chat.AgentId != session.AgentId || (mode is AgentToolRunLeaseMode.Dispatch or AgentToolRunLeaseMode.SavedEffectDispatch) &&
                chat.LatestExecutionRunId != run.Id ||
            authority is null || authority.AuthorityId != session.Reference.AuthorityId || authority.AgentId != session.AgentId ||
            authority.DatabaseProfileId != profile.ProfileId || authority.DatabaseProfileGeneration != profile.Generation ||
            !authority.ReadAllowed) {
            throw Failure("The saved execution, chat ownership, purpose, authority or profile no longer matches tool admission.");
        }

        if (!AllowsState(run, mode)) {
            throw Failure("A terminal execution cannot dispatch or disclose admitted tool data.");
        }
    }

    private static bool AllowsState(ExecutionRunRecord run, AgentToolRunLeaseMode mode) => mode switch {
        AgentToolRunLeaseMode.Dispatch => run.State != ExecutionState.Completed && run.Outcome != RunOutcome.Cancelled,
        AgentToolRunLeaseMode.CancelledReceiptReconciliation => run.Outcome == RunOutcome.Cancelled,
        AgentToolRunLeaseMode.SavedEffectDispatch => (run.State is ExecutionState.WaitingOnTool or ExecutionState.Running) && run.Outcome is null,
        AgentToolRunLeaseMode.InterruptedEffectReconciliation => run.State switch {
            ExecutionState.Preparing or ExecutionState.Running or ExecutionState.WaitingOnTool or ExecutionState.Persisting => run.Outcome is null,
            ExecutionState.Failed => run.Outcome is null or RunOutcome.Failed,
            _ => false
        },
        _ => false
    };

    private static void RequireLeaseMode(AgentToolRunLease lease, bool reconciliationOnly) {
        var expected = reconciliationOnly ? AgentToolRunLeaseMode.CancelledReceiptReconciliation : AgentToolRunLeaseMode.Dispatch;
        if (lease.Mode != expected) {
            throw Failure("The active lease mode cannot authorize this dispatch, disclosure or reconciliation operation.");
        }
    }

    public async ValueTask<AgentToolRunLease> AcquireCancelledReconciliationAsync(AgentToolSessionReference reference,
        CancellationToken cancellationToken) {
        var handle = await leases.AcquireToolDispatchLeaseAsync(reference.ExecutionRunId, cancellationToken);
        try {
            var lease = new AgentToolRunLease(this, reference, Guid.NewGuid(), handle, AgentToolRunLeaseMode.CancelledReceiptReconciliation);
            await MutateAsync(lease, false, journal => journal with {
                Revision = checked(journal.Revision + 1), ActiveDispatchLeaseId = lease.Id
            }, cancellationToken, reconciliationOnly: true, backgroundCheck: BackgroundCheck.RecordOutcome);
            return lease;
        } catch {
            await handle.DisposeAsync();
            throw;
        }
    }

    internal async ValueTask<AgentToolReceiptReconciliationAdmission> RequireReceiptReconciliationAsync(
        AgentToolReceiptReconciliationClaim claim, CancellationToken cancellationToken, bool includePrior = false) {
        claim.RequireOwner(this);
        if (claim.Lease.Mode == AgentToolRunLeaseMode.SavedEffectDispatch) {
            return await RequireSavedEffectReceiptAsync(claim, cancellationToken, includePrior);
        }
        if (claim.Lease.Mode == AgentToolRunLeaseMode.InterruptedEffectReconciliation) {
            return await RequireInterruptedReceiptAsync(claim, cancellationToken, includePrior);
        }
        var snapshot = await ReadVerifiedSnapshotAsync(claim.Lease, reconciliationOnly: true, cancellationToken);
        var journal = RequireJournal(snapshot.Run);
        if (journal.Session.Reference.BackgroundSource is not null &&
                !await CanReadBackgroundResultAsync((await RequireSnapshotAsync(claim.Lease.Session.ExecutionRunId, cancellationToken)).Run, cancellationToken)) {
            throw new AgentToolReceiptAccessDeniedException();
        }
        var proposal = AgentToolJournalTransitions.RequireProposal(journal, claim.Binding);
        var allowedState = claim.Purpose switch {
            AgentToolReceiptReconciliationPurpose.CachedReceiptDisclosure => proposal.State == AgentToolProposalState.Cancelled &&
                proposal.Cancellation?.Disposition == AgentToolCancellationDisposition.ReceiptCommitted,
            AgentToolReceiptReconciliationPurpose.UncertainEffect => proposal.State is AgentToolProposalState.Executing or AgentToolProposalState.ReconciliationRequired ||
                proposal.State == AgentToolProposalState.Cancelled && proposal.Cancellation?.Disposition == AgentToolCancellationDisposition.CancelledUnreconciled,
            _ => false
        };
        if (proposal.Payload.Recovery != AgentToolProposalRecovery.OwnerReceipt || proposal.DispatchClaimId is null ||
            proposal.ApprovalStatus != ExecutionApprovalStatus.Approved || proposal.ApprovedDigest != proposal.Payload.Digest ||
            !allowedState) {
            throw Failure("The cancelled proposal has no exact originally approved dispatched owner-receipt intent.");
        }

        claim.RequireOwner(this);
        cancellationToken.ThrowIfCancellationRequested();
        return AgentToolPriorInvocationEvidence.Receipt(snapshot, claim.Binding, proposal.Payload, includePrior);
    }

    public async Task<AgentToolRunCancellationReconciliation> ReconcileCancelledAsync(AgentToolRunLease lease,
        IReadOnlyList<IAgentToolReceiptReconciliationProvider> providers, bool currentReadAllowed, CancellationToken cancellationToken) {
        var saved = await MutateAsync(lease, true, AgentToolJournalTransitions.RejectUndispatchedOnCancellation,
            cancellationToken, reconciliationOnly: true, backgroundCheck: BackgroundCheck.RecordOutcome);
        var observedThisRequest = new HashSet<AgentToolBusinessIntentId>();
        foreach (var batch in saved.Batches) {
            foreach (var proposal in batch.Proposals) {
                if (proposal.State is AgentToolProposalState.Completed or AgentToolProposalState.Rejected or AgentToolProposalState.OwnerReceiptConfirmed ||
                    proposal.State == AgentToolProposalState.Cancelled &&
                        proposal.Cancellation?.Disposition != AgentToolCancellationDisposition.CancelledUnreconciled) {
                    continue;
                }

                var binding = Binding(batch.Id, proposal);
                var resolution = new AgentToolCancellationResolution(AgentToolCancellationDisposition.CancelledUnreconciled,
                    AgentToolCancellationReason.NoReceiptProtocol);
                if (proposal.Payload.Effect == AgentToolProposalEffect.Read &&
                    proposal.Payload.Recovery == AgentToolProposalRecovery.RevalidateAndRead) {
                    resolution = new(AgentToolCancellationDisposition.NoExternalMutation, AgentToolCancellationReason.ReadOnlyInvocation);
                } else if (proposal.Payload.Recovery == AgentToolProposalRecovery.OwnerReceipt) {
                    var matches = providers.Where(provider => provider.Supports(proposal.Payload.ToolName)).ToArray();
                    if (matches.Length > 1) {
                        throw new InvalidOperationException("Multiple receipt reconciliation owners registered for one admitted tool.");
                    }

                    if (!currentReadAllowed) {
                        resolution = new(AgentToolCancellationDisposition.CancelledUnreconciled, AgentToolCancellationReason.CurrentAccessDenied);
                    } else if (matches.Length == 1) {
                        var claim = new AgentToolReceiptReconciliationClaim(this, lease, binding);
                        try {
                            var observed = await matches[0].ReconcileAsync(claim, cancellationToken);
                            if ((observed.CommittedEffect is null) != (observed.Receipt is null)) {
                                throw new InvalidDataException("Owner receipt evidence must carry both the original effect identity and its bounded receipt.");
                            }

                            resolution = observed.CommittedEffect is null
                                ? new(AgentToolCancellationDisposition.CancelledUnreconciled, AgentToolCancellationReason.ReceiptNotObserved)
                                : new(AgentToolCancellationDisposition.ReceiptCommitted, AgentToolCancellationReason.ReceiptFound,
                                    observed.CommittedEffect, observed.Receipt);
                            if (observed.CommittedEffect is not null) {
                                observedThisRequest.Add(proposal.IntentId);
                            }
                        } catch (AgentToolReceiptAccessDeniedException) {
                            resolution = new(AgentToolCancellationDisposition.CancelledUnreconciled, AgentToolCancellationReason.CurrentAccessDenied);
                        }
                    }
                }

                await MutateAsync(lease, true, current => AgentToolJournalTransitions.Replace(current, binding, latest => {
                    if (latest.State == AgentToolProposalState.Cancelled && resolution.Disposition != AgentToolCancellationDisposition.ReceiptCommitted) {
                        return latest;
                    }

                    return latest with {
                        State = AgentToolProposalState.Cancelled,
                        Cancellation = resolution,
                        EffectState = resolution.Disposition switch {
                            AgentToolCancellationDisposition.ReceiptCommitted => AgentToolEffectState.Committed,
                            AgentToolCancellationDisposition.NoExternalMutation => AgentToolEffectState.None,
                            _ => AgentToolEffectState.Unknown
                        }
                    };
                }), cancellationToken, reconciliationOnly: true, backgroundCheck: BackgroundCheck.RecordOutcome);
            }
        }

        var completed = await ReadVerifiedAsync(lease, reconciliationOnly: true, cancellationToken);
        foreach (var batch in completed.Batches) {
            foreach (var proposal in batch.Proposals.Where(proposal =>
                proposal.Cancellation?.Disposition == AgentToolCancellationDisposition.ReceiptCommitted &&
                !observedThisRequest.Contains(proposal.IntentId))) {
                await ValidateCachedReceiptDisclosureAsync(lease, batch.Id, proposal, providers, currentReadAllowed, cancellationToken);
            }
        }
        return new(lease.Session.ExecutionRunId, lease.Session.ChatSessionId, completed.Batches.SelectMany(batch => batch.Proposals)
            .Select(proposal => new AgentToolCancellationOutcome(proposal.IntentId, proposal.Payload.ToolName,
                proposal.EffectState, proposal.Cancellation)).ToArray(),
            (completed.ProviderDispatches.IsDefault ? [] : completed.ProviderDispatches)
                .Select(dispatch => new AgentToolProviderDispatchOutcome(dispatch.Id, dispatch.State)).ToArray());
    }

    private async Task ValidateCachedReceiptDisclosureAsync(AgentToolRunLease lease, AgentToolBatchId batch,
        AgentToolProposalRecord proposal, IReadOnlyList<IAgentToolReceiptReconciliationProvider> providers,
        bool currentReadAllowed, CancellationToken cancellationToken) {
        if (!currentReadAllowed) {
            throw new AgentToolAdmissionException("tool-admission.receipt-read-denied", "Current authorization does not permit disclosing this saved owner receipt.");
        }

        var matches = providers.Where(provider => provider.Supports(proposal.Payload.ToolName)).ToArray();
        if (matches.Length != 1) {
            throw new AgentToolAdmissionException("tool-admission.receipt-read-unavailable", "Exactly one owner receipt reader is required before saved receipt disclosure.");
        }

        var claim = new AgentToolReceiptReconciliationClaim(this, lease, Binding(batch, proposal),
            AgentToolReceiptReconciliationPurpose.CachedReceiptDisclosure);
        AgentToolReceiptObservation observed;
        try {
            observed = await matches[0].ReconcileAsync(claim, cancellationToken);
        } catch (AgentToolReceiptAccessDeniedException) {
            throw new AgentToolAdmissionException("tool-admission.receipt-read-denied", "Current authorization does not permit disclosing this saved owner receipt.");
        }

        if (observed.CommittedEffect is null || observed.Receipt is null) {
            throw new AgentToolAdmissionException("tool-admission.receipt-read-unavailable", "The owner could not confirm the saved receipt for disclosure. Durable committed evidence was retained.");
        }

        if (observed.CommittedEffect != proposal.Cancellation!.CommittedEffect || observed.Receipt != proposal.Cancellation.Receipt) {
            throw new AgentToolAdmissionException("tool-admission.receipt-read-mismatch", "The owner receipt differs from the retained committed evidence. Disclosure was rejected.");
        }
    }

    private async Task<ExecutionRunAdmissionSnapshot> RequireSnapshotAsync(Guid runId, CancellationToken cancellationToken) {
        using var activity = AgentFrameworkTelemetry.ActivitySource.StartActivity("tool.journal.read");
        return await reader.GetExecutionRunAdmissionSnapshotAsync(runId, cancellationToken)
            ?? throw Failure("The admitted execution run was not found.");
    }

    private static AgentToolJournalRecord RequireJournal(ExecutionRunRecord run) {
        using var activity = AgentFrameworkTelemetry.ActivitySource.StartActivity("tool.journal.validate");
        var journal = run.ToolAdmission ?? throw Failure("This legacy run has no durable tool admission journal.");
        journal.Validate();
        return journal;
    }

    private static void RequireRecoverable(AgentToolJournalRecord journal) {
        if (journal.Support != AgentToolAdmissionSupport.Recoverable) {
            throw new AgentToolAdmissionException("tool-admission.request-scoped-input",
                "This run contains request-scoped attachments without retained immutable recovery references. Durable effect admission is unavailable.");
        }
    }

    private static void RequireLease(AgentToolRunLease lease, AgentToolJournalRecord journal) {
        if (journal.ActiveDispatchLeaseId != lease.Id || journal.Session.Reference != lease.Session) {
            throw Failure("The active runtime no longer owns the persisted dispatch lease.");
        }
    }

    private static void RequireClaim(AgentToolInvocationClaim claim, AgentToolProposalRecord proposal) {
        if (proposal.State != AgentToolProposalState.Executing || proposal.DispatchClaimId != claim.Id) {
            throw Failure("The serial dispatch claim changed before its checkpoint.");
        }
    }

    private static AgentToolApprovalBinding Binding(AgentToolBatchId batch, AgentToolProposalRecord proposal)
        => new(batch, proposal.IntentId, proposal.Payload.SemanticVersion, proposal.Payload.Digest);

    private static AgentToolAdmissionException Failure(string message)
        => new("tool-admission.denied", message);
}
