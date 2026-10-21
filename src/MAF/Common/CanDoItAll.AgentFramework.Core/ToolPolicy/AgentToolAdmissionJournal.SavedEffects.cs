using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Runtime.Abstractions;

namespace CanDoItAll.AgentFramework.Core;

public sealed partial class AgentToolAdmissionJournal {
    private const string SavedEffectDecisionSource = "saved-effect-decision";

    public async Task<ExecutionRunRecord> ReadSavedEffectAsync(AgentToolRunLease lease,
        AgentToolApprovalBinding binding, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(binding);
        if (RequireBoundSavedEffect(lease).Binding != binding) {
            throw Failure("This saved lease cannot read another selected effect.");
        }
        return (await ReadSavedEffectSnapshotAsync(lease, cancellationToken)).Run;
    }

    public WorkspaceScopeDescriptor RequireSavedEffectContextScope(AgentToolRunLease lease) {
        var admitted = RequireBoundSavedEffect(lease);
        if (admitted.Parent is { } parent) {
            parent.RequireOriginal(admitted.Run);
            return AgentPlanningRunMetadata.ReadScope(admitted.Run);
        }
        return AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(admitted.Run.MetadataJson)?.WorkspaceScope
            ?? throw Failure("The saved effect has no original interactive context scope.");
    }

    public Task<AgentToolRunLease> AcquireSavedEffectDispatchAsync(AgentSavedEffectContinuationRequest request,
        Func<AgentToolSessionObservation, CancellationToken, ValueTask<bool>> currentRead, CancellationToken cancellationToken)
        => AcquireSavedEffectDispatchAsync(request, currentRead, null, null, null, cancellationToken);

    internal Task<AgentToolRunLease> AcquirePlanningEffectDispatchAsync(AgentPlanningDecisionOwner owner,
        AgentSavedEffectContinuationRequest request, AgentPlanningDecisionExpectation expected,
        Func<AgentToolSessionObservation, CancellationToken, ValueTask<bool>> currentRead, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(expected);
        return AcquireSavedEffectDispatchAsync(request, currentRead, owner, expected, null, cancellationToken);
    }

    internal Task<AgentToolRunLease> AcquireInheritedPlanningEffectDispatchAsync(AgentPlanningDecisionOwner owner,
        AgentSavedEffectContinuationRequest request, AgentPlanningDecisionExpectation expected, AgentInheritedSavedEffectContinuation parent,
        Func<AgentToolSessionObservation, CancellationToken, ValueTask<bool>> currentRead, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(parent);
        parent.RequireSelection(expected, request.Binding);
        return AcquireSavedEffectDispatchAsync(request, currentRead, owner, expected, parent, cancellationToken);
    }

    private async Task<AgentToolRunLease> AcquireSavedEffectDispatchAsync(AgentSavedEffectContinuationRequest request,
        Func<AgentToolSessionObservation, CancellationToken, ValueTask<bool>> currentRead,
        AgentPlanningDecisionOwner? planningOwner, AgentPlanningDecisionExpectation? planningExpected,
        AgentInheritedSavedEffectContinuation? parent, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(currentRead);
        var original = await RequireSavedRequestAsync(request.ExecutionRunId, request.ChatSessionId,
            request.ExpectedRunRevision, request.ExpectedJournalRevision, cancellationToken, planningExpected, parent);
        await RequireSavedReadAsync(original, currentRead, cancellationToken, parent);
        var handle = await leases.AcquireToolDispatchLeaseAsync(request.ExecutionRunId, cancellationToken);
        try {
            await RequireSavedReadAsync(original, currentRead, cancellationToken, parent);
            var lease = new AgentToolRunLease(this, original.ToolAdmission!.Session.Reference, Guid.NewGuid(), handle,
                AgentToolRunLeaseMode.SavedEffectDispatch);
            long started = 0;
            var saved = await writer.UpdateExecutionRunDetailAsync(request.ExecutionRunId, current => {
                var journal = RequireSavedOriginal(original, current, compareRevision: true, parent);
                if (planningExpected is not null) {
                    RequirePlanningExpectation(current.Run, planningExpected, inherited: parent is not null);
                }
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
                if (planningOwner is not null) {
                    RequireSelectedPrepared(changed, request.Binding);
                    var decision = changed.PlanningDecision!;
                    var step = decision.Current.Steps[^1];
                    if (step.Publications.Length != 1 || step.Publications[0].Proposal != request.Binding) {
                        throw Failure("The saved action must select the current planning step's original publication.");
                    }
                    started = clock.GetTimestamp();
                    changed = changed with {
                        PlanningDecision = AgentPlanningDecisionTransitions.ResumeEffects(decision, step.Id, lease.Id, now)
                    };
                }
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
                request.Binding, currentRead, parent));
            if (planningOwner is not null) {
                lease.AdmitPlanningWork(this, new(planningOwner, lease, saved.Run, started));
            }
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
        RequireSavedMutationCeiling(observed.Run, admitted.Parent);
        var selected = RequireSelectedPrepared(RequireJournal(observed.Run), binding);
        lease.PlanningWork?.RequireMutation(observed.Run, binding);
        if (selected.Payload != restored) {
            throw Failure("Restoration must preserve the entire original arguments and source preparation.");
        }
        lease.BeginSavedClaim(this);
        var claimId = Guid.NewGuid();
        var saved = await writer.UpdateExecutionRunDetailAsync(lease.Session.ExecutionRunId, current => {
            RequireBoundSavedEffect(lease);
            var journal = RequireSavedOriginal(admitted.Run, current, compareRevision: true, admitted.Parent);
            RequireCurrent(journal.Session, new(current.Run, current.ChatSession), AgentToolRunLeaseMode.SavedEffectDispatch);
            RequireSavedMutationCeiling(current.Run, admitted.Parent);
            RequireLease(lease, journal);
            var proposal = RequireSelectedPrepared(journal, binding);
            lease.PlanningWork?.RequireMutation(current.Run, binding);
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
            var journal = RequireSavedOriginal(observed.Run, current, compareRevision: true, admitted.Parent);
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
            return CancelSavedDetail(current, clock.GetUtcNow());
        }, cancellationToken);
    }

    internal static ExecutionRunDetail CancelSavedDetail(ExecutionRunDetail current, DateTimeOffset now) {
        var journal = current.Run.ToolAdmission!;
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
    }

    private async Task<ExecutionRunRecord> RequireSavedRequestAsync(Guid runId, Guid? chatId, long runRevision,
        long journalRevision, CancellationToken cancellationToken, AgentPlanningDecisionExpectation? planningExpected = null,
        AgentInheritedSavedEffectContinuation? parent = null) {
        var snapshot = await RequireSnapshotAsync(runId, cancellationToken);
        var run = snapshot.Run with { PendingApprovals = snapshot.Run.PendingApprovals.ToImmutableArray() };
        var journal = RequireJournal(run);
        RequireSavedJournal(run, journal, parent);
        if (planningExpected is null) {
            if (journal.SchemaVersion != AgentToolJournalRecord.EffectJournalSchemaVersion) {
                throw Failure("Planning actions require their original decision owner and exact displayed expectation.");
            }
        } else {
            RequirePlanningExpectation(run, planningExpected, inherited: parent is not null);
        }
        RequireCurrent(journal.Session, new(run, snapshot.ChatSession), AgentToolRunLeaseMode.SavedEffectDispatch);
        if (run.ChatSessionId != chatId || run.Revision != runRevision || journal.Revision != journalRevision) {
            throw Failure("The displayed saved-effect run or journal revision changed. Read the current state before another explicit action.");
        }
        return run;
    }

    private AgentSavedEffectAdmission RequireBoundSavedEffect(AgentToolRunLease lease) {
        var admitted = lease.RequireSavedEffect(this);
        if (!ReferenceEquals(AgentToolRunLease.Current, lease) ||
            admitted.Run.ToolAdmission?.PlanningDecision is not null && lease.PlanningWork is null) {
            throw Failure("The original saved-effect lease must be bound to this operation.");
        }
        return admitted;
    }

    private async Task<ExecutionRunAdmissionSnapshot> ReadSavedEffectSnapshotAsync(AgentToolRunLease lease,
        CancellationToken cancellationToken) {
        var admitted = RequireBoundSavedEffect(lease);
        var snapshot = await RequireSnapshotAsync(lease.Session.ExecutionRunId, cancellationToken);
        var journal = RequireSavedOriginal(admitted.Run, new(snapshot.Run, snapshot.ChatSession, [], []), compareRevision: false, admitted.Parent);
        RequireCurrent(journal.Session, snapshot, AgentToolRunLeaseMode.SavedEffectDispatch);
        RequireLease(lease, journal);
        await RequireSavedReadAsync(snapshot.Run, admitted.CurrentRead, cancellationToken, admitted.Parent);
        lease.RequireOwner(this);
        return snapshot;
    }

    private async Task RequireSavedReadAsync(ExecutionRunRecord run,
        Func<AgentToolSessionObservation, CancellationToken, ValueTask<bool>> currentRead, CancellationToken cancellationToken,
        AgentInheritedSavedEffectContinuation? parent = null) {
        await RequireEffectReadPolicyAsync(run, AgentToolSessionObservation.From(run),
            parent is null ? currentRead : RequireParentReadAsync,
            recheckAfterBackground: parent is not null, cancellationToken,
            parent is null || parent.Purpose == AgentInheritedContinuationPurpose.ManagerReview
                ? BackgroundCheck.Read : BackgroundCheck.Dispatch);
        cancellationToken.ThrowIfCancellationRequested();

        async ValueTask<bool> RequireParentReadAsync(AgentToolSessionObservation observation, CancellationToken token) {
            if (!await currentRead(observation, token)) {
                return false;
            }
            await parent!.RequireCurrentAsync(run, token);
            return true;
        }
    }

    private async ValueTask<AgentToolReceiptReconciliationAdmission> RequireSavedEffectReceiptAsync(
        AgentToolReceiptReconciliationClaim claim, CancellationToken cancellationToken, bool includePrior = false) {
        claim.RequireOwner(this);
        var admitted = RequireBoundSavedEffect(claim.Lease);
        if (claim.Purpose != AgentToolReceiptReconciliationPurpose.PreparedSavedEffect || claim.Binding != admitted.Binding) {
            throw Failure("The receipt preflight must name this lease's one original prepared effect.");
        }
        var snapshot = await ReadSavedEffectSnapshotAsync(claim.Lease, cancellationToken);
        var journal = RequireJournal(snapshot.Run);
        var proposal = RequireSelectedPrepared(journal, admitted.Binding);
        claim.RequireOwner(this);
        cancellationToken.ThrowIfCancellationRequested();
        return AgentToolPriorInvocationEvidence.Receipt(snapshot, admitted.Binding, proposal.Payload, includePrior);
    }

    private async Task RecordSavedEffectOutcomeAsync(AgentToolInvocationClaim claim,
        Func<AgentToolProposalRecord, AgentToolProposalRecord> update, CancellationToken cancellationToken,
        bool preserveUncertainty = false) {
        claim.RequireOwner(this);
        var admitted = RequireBoundSavedEffect(claim.RunLease);
        await writer.UpdateExecutionRunDetailAsync(claim.RunLease.Session.ExecutionRunId, current => {
            claim.RequireOwner(this);
            var journal = RequireSavedOriginal(admitted.Run, current, compareRevision: false, admitted.Parent);
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
        bool compareRevision, AgentInheritedSavedEffectContinuation? parent = null) {
        var journal = RequireOriginalRun(original, current, compareRevision);
        RequireSavedJournal(current.Run, journal, parent);
        return journal;
    }

    private static AgentToolJournalRecord RequireOriginalRun(ExecutionRunRecord original, ExecutionRunDetail current,
        bool compareRevision) {
        var run = current.Run;
        var journal = RequireJournal(run);
        if (run.Id != original.Id || run.AgentId != original.AgentId || run.ChatSessionId != original.ChatSessionId ||
            run.CreatedAtUtc != original.CreatedAtUtc || run.MetadataJson != original.MetadataJson ||
            run.SourceKind != original.SourceKind || run.SourceId != original.SourceId ||
            journal.Session != original.ToolAdmission!.Session ||
            compareRevision && (run.Revision != original.Revision || journal.Revision != original.ToolAdmission.Revision ||
                !run.PendingApprovals.SequenceEqual(original.PendingApprovals))) {
            throw Failure("The original execution authority, decisions or exact checkpoint changed.");
        }
        return journal;
    }

    private static void RequireSavedJournal(ExecutionRunRecord run, AgentToolJournalRecord journal,
        AgentInheritedSavedEffectContinuation? parent = null) {
        var supported = journal.SchemaVersion switch {
            AgentToolJournalRecord.EffectJournalSchemaVersion => parent is null && journal.Segments.Length == 1,
            AgentToolJournalRecord.PlanningSchemaVersion => parent is null && journal.PlanningDecision?.Current.Steps.IsEmpty == false,
            AgentToolJournalRecord.InheritedPlanningSchemaVersion => parent is not null && journal.PlanningDecision?.Current.Steps.IsEmpty == false,
            _ => false
        };
        var interactiveMismatch = parent is null && (journal.Session.Reference.BackgroundSource is not null || !string.IsNullOrEmpty(run.ProcessRunId));
        if (!supported || interactiveMismatch ||
            journal.Support != AgentToolAdmissionSupport.EffectJournalOnly || journal.Segments.IsEmpty ||
            journal.Segments.Any(segment => segment.CheckpointKind != AgentToolCheckpointKind.EffectsOnly)) {
            throw Failure("Saved effect dispatch requires its original supported admission and explicit decision owner.");
        }
        parent?.RequireOriginal(run);
    }

    private static void RequirePreparedOwnerEffect(AgentToolProposalRecord proposal) {
        if (proposal.State != AgentToolProposalState.Prepared || proposal.ProviderCall is not null ||
            proposal.Payload.Effect != AgentToolProposalEffect.Mutation || proposal.Payload.Recovery != AgentToolProposalRecovery.OwnerReceipt ||
            proposal.Payload.SourcePreparation is null || proposal.DispatchClaimId is not null) {
            throw Failure("Only an original prepared owner-receipted effect can acquire saved mutation authority; entered effects require receipt reconciliation.");
        }
    }

    private static void RequireSavedMutationCeiling(ExecutionRunRecord run, AgentInheritedSavedEffectContinuation? parent) {
        if (parent is not null) {
            parent.RequireOriginal(run);
            return;
        }
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
