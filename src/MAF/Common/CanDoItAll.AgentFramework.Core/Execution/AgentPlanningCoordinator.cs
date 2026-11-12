using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Runtime.Abstractions;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.AgentFramework.Core;

internal sealed class AgentPlanningCoordinator(AgentToolAdmissionJournal journal, IAgentPlanningStepRuntime runtime,
    IEnumerable<IAgentPlanningParticipant> participants) {
    private const string UnsupportedCode = "planning.unsupported";
    private const string ChangedContractCode = "planning.runtime-contract-changed";
    private readonly Dictionary<AgentPlanningOwner, IAgentPlanningParticipant> participants = participants.ToDictionary(item => item.Owner);
    private readonly AgentPlanningDecisionOwner owner = new(journal);

    internal static void RequireSupportedRequest(ExecutionRunRequest request, bool chatBacked) {
        if (request.PlanningOwner is not { } selection) {
            return;
        }
        if (!Enum.IsDefined(selection) || !chatBacked || request.AutoApprovePendingToolCalls ||
            request.InputAttachmentPaths is { Count: > 0 } || request.TransientContext is { Attachments.IsDefaultOrEmpty: false } ||
            request.StructuredOutput is not null || request.JsonSchemaOutput is not null) {
            throw new AgentToolAdmissionException(UnsupportedCode,
                "Planning requires an interactive chat, explicit publication review, and its own material basis and output contract.");
        }
        if (request.TransientContext is { } context && context.WorkspaceScope !=
            AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(request.Context?.MetadataJson)?.WorkspaceScope) {
            throw new AgentToolAdmissionException(UnsupportedCode, "Planning context must match its originally admitted workspace scope.");
        }
    }

    internal async Task<AgentPlanningPreparedRun> PrepareAsync(AgentPlanningOwner selection, AgentDefinition agent,
        ProviderProfile provider, CatalogDataRevision revision, ExecutionInvocationContext context, CancellationToken cancellationToken) {
        var participant = RequireParticipant(selection);
        var source = AgentTurnContextMetadata.TryReadTurnContextReference(context.MetadataJson)
            ?? throw new AgentToolAdmissionException(UnsupportedCode, "Planning requires its original source context.");
        var governance = AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(context.MetadataJson)
            ?? throw new AgentToolAdmissionException(UnsupportedCode, "Planning requires its original execution authority.");
        var admission = await participant.PrepareRunAsync(new(agent, revision, journal.Profile, source, governance), cancellationToken);
        return new(admission, selection, runtime.CaptureRuntimeContract(agent, provider, participant));
    }

    internal async Task<AgentPlanningExecutionResult> RunAsync(AgentDefinition agent, ProviderProfile provider, ExecutionRunRecord original,
        CancellationToken cancellationToken) {
        var decision = original.ToolAdmission?.PlanningDecision
            ?? throw new AgentToolAdmissionException(UnsupportedCode, "This run has no admitted planning decision.");
        var participant = RequireParticipant(decision.Owner);
        var contract = RequireRuntimeContract(agent, provider, original, participant);
        var lease = await owner.StartAsync(AgentPlanningDecisionExpectation.From(original), async (observation, token) => {
            if (observation.Session.Reference != decision.Session) {
                return false;
            }
            await participant.RequireCurrentReadAsync(decision, token);
            return true;
        }, cancellationToken);
        return await new AgentPlanningExecutionLoop(owner, runtime).RunAsync(agent, provider, lease, participant, contract,
            (work, token) => owner.CaptureBasisAsync(work, participant.CaptureBasisAsync, token), cancellationToken);
    }

    private IAgentPlanningParticipant RequireParticipant(AgentPlanningOwner selection)
        => Enum.IsDefined(selection) && participants.TryGetValue(selection, out var participant) ? participant
            : throw new AgentToolAdmissionException(UnsupportedCode, "The selected planning participant is not registered.");

    internal async Task<AgentPlanningReview> ReadReviewAsync(ExecutionRunDetail detail, CancellationToken cancellationToken) {
        var run = detail.Run;
        var expected = AgentPlanningDecisionExpectation.From(run);
        var admitted = run.ToolAdmission!;
        var decision = admitted.PlanningDecision!;
        var participant = RequireParticipant(decision.Owner);
        try {
            await participant.RequireCurrentReadAsync(decision, cancellationToken);
        } catch (UnauthorizedAccessException) {
            throw new AgentToolReceiptAccessDeniedException();
        }
        var now = journal.Clock.GetUtcNow();
        if (now < decision.Work.LastObservedAtUtc) {
            throw new AgentPlanningAdmissionException(decision.DecisionId, decision.CurrentGeneration, AgentPlanningAdmissionRefusal.ClockInvalid);
        }
        var basis = decision.Current.Bases.LastOrDefault();
        var unsettled = decision.Work.Open is not null;
        TimeSpan? remaining = unsettled ? null : TimeSpan.FromTicks(Math.Max(0,
            decision.Policy.Budget.MaximumWorkDuration.Ticks - decision.Work.ClosedWorkTicks));
        var reviewable = !unsettled && run.Outcome is null && detail.ChatSession?.LatestExecutionRunId == run.Id &&
            decision.Current.State is AgentPlanningGenerationState.AwaitingApproval or AgentPlanningGenerationState.PausedBasisChanged &&
            !admitted.HasUnresolvedProviderDispatch && !admitted.Batches.SelectMany(batch => batch.Proposals).Any(item =>
                item.State is AgentToolProposalState.Executing or AgentToolProposalState.ReconciliationRequired ||
                item.EffectState == AgentToolEffectState.Committed ||
                item.EffectState == AgentToolEffectState.Unknown && item is not {
                    State: AgentToolProposalState.Prepared, ApprovalStatus: ExecutionApprovalStatus.Pending,
                    DispatchClaimId: null, Result: null, OwnerReceipt: null, ProviderCall: null } ||
                item.State == AgentToolProposalState.Prepared && item.ApprovalStatus != ExecutionApprovalStatus.Pending);
        List<AgentPlanningAction> actions = [];
        AgentPlanningPublicationReview? publication = null;
        if (decision.Current.State == AgentPlanningGenerationState.AwaitingApproval && run.PendingApprovals.SingleOrDefault() is { ToolAdmission: { } binding } pending) {
            var selected = AgentToolJournalTransitions.RequireProposal(admitted, binding);
            if (decision.Current.Steps[^1].Publications.SingleOrDefault()?.Proposal != binding) {
                throw new AgentToolAdmissionException(UnsupportedCode, "The displayed publication differs from its original planning step.");
            }
            publication = new(binding, pending.ApprovalId, participant.DescribePublication(
                AgentToolSessionObservation.From(run), selected.Payload, binding));
            if (reviewable && remaining > TimeSpan.Zero && basis is not null && now >= basis.CapturedAtUtc &&
                now - basis.CapturedAtUtc < decision.Policy.Budget.MaximumBasisAge) {
                actions.Add(AgentPlanningAction.Approve);
            }
        }
        if (reviewable) {
            if (remaining > TimeSpan.Zero && decision.Generations.Length < AgentPlanningDecisionRecord.MaximumGenerations &&
                decision.Generations.Sum(item => item.Steps.Length) < AgentPlanningDecisionRecord.MaximumSteps) {
                actions.Add(AgentPlanningAction.Refresh);
            }
            actions.Add(AgentPlanningAction.Cancel);
        }
        cancellationToken.ThrowIfCancellationRequested();
        return new(expected, run.ChatSessionId!.Value, decision.Owner, decision.Policy, decision.Current.State, now,
            basis?.CapturedAtUtc, TimeSpan.FromTicks(decision.Work.ClosedWorkTicks), remaining, unsettled,
            decision.AutomaticReplans, [.. actions], publication);
    }

    internal async Task<AgentPlanningPublicationResult> ApproveAsync(ExecutionRunRecord original, AgentPlanningActionRequest request,
        IAgentContinuationRuntime continuation, CancellationToken cancellationToken) {
        var participant = RequireAction(original, request, AgentPlanningAction.Approve);
        if (continuation.SavedEffectDispatchSupport != AgentSavedEffectDispatchSupport.OriginalPreparedOwnerEffect) {
            throw new NotSupportedException("The current runtime cannot dispatch an original prepared planning publication.");
        }
        var pending = original.PendingApprovals.SingleOrDefault();
        if (pending is null || pending.ApprovalId != request.ApprovalId || pending.ToolAdmission != request.Binding) {
            throw new AgentToolAdmissionException(UnsupportedCode, "Approval must name the one exact displayed planning publication.");
        }
        var binding = request.Binding!;
        var saved = new AgentSavedEffectContinuationRequest(original.Id, original.ChatSessionId,
            request.Expected.RunRevision, request.Expected.JournalRevision, binding,
            [new(pending.ApprovalId, true) { ToolAdmission = binding }]);
        await using var lease = await owner.ResumeSavedEffectAsync(saved, request.Expected,
            CurrentRead(participant, original), cancellationToken);
        using var bound = lease.Bind();
        return await new AgentPlanningPublicationCompletion(journal, owner).DispatchAsync(original, lease,
            new(original.ToolAdmission!.Session.Reference, binding), participant, continuation, cancellationToken);
    }

    internal Task<ExecutionRunDetail> RefreshAsync(AgentDefinition agent, ProviderProfile provider, ExecutionRunRecord original,
        AgentPlanningActionRequest request, bool automatic, CancellationToken cancellationToken) {
        var participant = RequireAction(original, request, AgentPlanningAction.Refresh);
        _ = RequireRuntimeContract(agent, provider, original, participant);
        return owner.RefreshAsync(request.Expected, automatic, CurrentRead(participant, original), cancellationToken);
    }

    internal Task<ExecutionRunDetail> CancelAsync(ExecutionRunRecord original, AgentPlanningActionRequest request,
        CancellationToken cancellationToken) {
        var participant = RequireAction(original, request, AgentPlanningAction.Cancel);
        return owner.CancelAsync(request.Expected, CurrentRead(participant, original), cancellationToken);
    }

    internal static bool ShouldReplan(ExecutionRunRecord acknowledged) {
        var decision = acknowledged.ToolAdmission!.PlanningDecision!;
        return decision.Current.State == AgentPlanningGenerationState.PausedBasisChanged && decision.Work.Open is null &&
            decision.Policy.OnBasisChange == OnBasisChange.Replan && decision.AutomaticReplans < decision.Policy.Budget.MaximumReplans &&
            decision.Work.ClosedWorkTicks < decision.Policy.Budget.MaximumWorkDuration.Ticks &&
            decision.Generations.Length < AgentPlanningDecisionRecord.MaximumGenerations &&
            decision.Generations.Sum(item => item.Steps.Length) < AgentPlanningDecisionRecord.MaximumSteps;
    }

    private IAgentPlanningParticipant RequireAction(ExecutionRunRecord original, AgentPlanningActionRequest request,
        AgentPlanningAction action) {
        if (request.Action != action || request.ChatSessionId != original.ChatSessionId ||
            original.ToolAdmission?.PlanningDecision is not { } decision || AgentPlanningDecisionExpectation.From(original) != request.Expected) {
            throw new AgentToolAdmissionException(UnsupportedCode, "The original displayed planning action or revision changed.");
        }
        return RequireParticipant(decision.Owner);
    }

    private AgentToolSemanticDigest RequireRuntimeContract(AgentDefinition agent, ProviderProfile provider,
        ExecutionRunRecord original, IAgentPlanningParticipant participant) {
        var contract = AgentPlanningRunMetadata.Read(original);
        if (runtime.CaptureRuntimeContract(agent, provider, participant) != contract) {
            throw new AgentToolAdmissionException(ChangedContractCode, "The originally admitted planning provider or contract changed.");
        }
        return contract;
    }

    private static Func<AgentToolSessionObservation, CancellationToken, ValueTask<bool>> CurrentRead(
        IAgentPlanningParticipant participant, ExecutionRunRecord original) => async (observation, token) => {
        var expected = original.ToolAdmission!.PlanningDecision!;
        if (observation.Session != original.ToolAdmission.Session || observation.Planning?.Decision.DecisionId != expected.DecisionId ||
            observation.Planning.Decision.CurrentGeneration != expected.CurrentGeneration) {
            return false;
        }
        await participant.RequireCurrentReadAsync(expected, token);
        return true;
    };
}

internal sealed record AgentPlanningPreparedRun(AgentPlanningRunAdmission Admission,
    AgentPlanningOwner Owner, AgentToolSemanticDigest RuntimeContract);
