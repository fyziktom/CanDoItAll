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
        await using var lease = await owner.StartAsync(AgentPlanningDecisionExpectation.From(original), async (observation, token) => {
            if (observation.Session.Reference != decision.Session) {
                return false;
            }
            await participant.RequireCurrentReadAsync(decision, token);
            return true;
        }, cancellationToken);
        using var bound = lease.Bind();
        var work = lease.PlanningWork ?? throw new InvalidOperationException("The admitted planning lease has no work interval.");
        AgentRuntimeResponse? accumulated = null;
        try {
            while (true) {
                await owner.CaptureBasisAsync(work, participant.CaptureBasisAsync, cancellationToken);
                var result = await runtime.RunAsync(new(agent, provider, owner, work, participant, contract), cancellationToken);
                accumulated = Accumulate(accumulated, result.RuntimeResponse);
                if (result.Admission.Refusal is { } refusal) {
                    using var settlement = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                    var stopped = await owner.SettleTerminalAsync(work, AgentPlanningGenerationState.Failed, settlement.Token);
                    return new(stopped, accumulated, refusal);
                }
                var admitted = result.Admission.Run.ToolAdmission!;
                var publication = admitted.PlanningDecision!.Current.Steps[^1].Publications.SingleOrDefault();
                if (publication is not null) {
                    var proposal = AgentToolJournalTransitions.RequireProposal(admitted, publication.Proposal);
                    var pending = new PendingToolApprovalRecord(Guid.NewGuid().ToString("D"), proposal.CallId,
                        proposal.Payload.ToolName, "function", "Review the prepared publication.", proposal.Payload.ArgumentsJson);
                    var waiting = await owner.PauseForApprovalAsync(work, pending, cancellationToken);
                    return new(waiting, accumulated with {
                        PendingApprovals = waiting.PendingApprovals, RuntimeSessionKey = string.Empty, SerializedSessionStateJson = null
                    }, null);
                }
                work = await owner.AdvanceStepAsync(work, cancellationToken);
            }
        } catch (Exception failure) when (accumulated is not null) {
            var failed = failure as AgentRuntimeUsageException;
            IReadOnlyList<ProviderUsageObservation> additional = failed is null ? [] : ProviderUsageHistory.Attach(failed.UsageObservations, failed.HistoryEvidence);
            throw new AgentRuntimeUsageException("The original planning decision stopped; recorded provider usage is retained.",
                failure, [.. accumulated.UsageObservations, .. additional],
                [.. accumulated.ToolInvocationTraces, .. (failed?.ToolInvocationTraces ?? [])],
                failed?.EntryAgentRequestCompatibilityEvidence ?? accumulated.EntryAgentRequestCompatibilityEvidence,
                failed?.FailureOrigin ?? AgentRuntimeFailureOrigin.Runtime, failed?.ProviderFailureIdentity) {
                HistoryEvidence = failed?.HistoryEvidence ?? accumulated.HistoryEvidence,
                ProviderStatusCode = failed?.ProviderStatusCode
            };
        }
    }

    private IAgentPlanningParticipant RequireParticipant(AgentPlanningOwner selection)
        => Enum.IsDefined(selection) && participants.TryGetValue(selection, out var participant) ? participant
            : throw new AgentToolAdmissionException(UnsupportedCode, "The selected planning participant is not registered.");

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
        var response = await continuation.DispatchSavedEffectAsync(new(original.ToolAdmission!.Session.Reference, binding), cancellationToken);
        using var settlement = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var acknowledged = (await journal.ReadPlanningSnapshotAsync(lease, settlement.Token)).Run;
        var proposal = AgentToolJournalTransitions.RequireProposal(acknowledged.ToolAdmission!, binding);
        if (response.Binding != binding || response.RunRevision != acknowledged.Revision ||
            response.JournalRevision != acknowledged.ToolAdmission!.Revision || response.State != proposal.State ||
            response.EffectState != proposal.EffectState) {
            throw new AgentToolAdmissionException(UnsupportedCode, "The publication acknowledgement no longer matches its original journal.");
        }
        var assessment = participant.ClassifyPublication(AgentToolSessionObservation.From(original), proposal.Payload, response);
        if (!Enum.IsDefined(assessment.Outcome)) {
            throw new AgentToolAdmissionException(UnsupportedCode, "The planning participant returned an unsupported publication outcome.");
        }
        var work = lease.PlanningWork ?? throw new InvalidOperationException("Saved planning dispatch has no active work interval.");
        var decision = acknowledged.ToolAdmission.PlanningDecision!;
        var completed = assessment.Outcome == AgentPlanningPublicationOutcome.BasisChanged && decision.Policy.OnBasisChange != OnBasisChange.Fail
            ? await owner.PauseForBasisChangeAsync(work, binding, settlement.Token)
            : await owner.SettleTerminalAsync(work, assessment.Outcome == AgentPlanningPublicationOutcome.Committed
                ? AgentPlanningGenerationState.Completed : AgentPlanningGenerationState.Failed, settlement.Token);
        return new(completed, assessment);
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

    private static AgentRuntimeResponse Accumulate(AgentRuntimeResponse? previous, AgentRuntimeResponse current) {
        var usage = ProviderUsageHistory.Attach(current.UsageObservations, current.HistoryEvidence);
        return previous is null ? current with { UsageObservations = usage } : current with {
            InputTokens = checked(previous.InputTokens + current.InputTokens),
            CachedInputTokens = checked(previous.CachedInputTokens + current.CachedInputTokens),
            OutputTokens = checked(previous.OutputTokens + current.OutputTokens),
            ToolCalls = checked(previous.ToolCalls + current.ToolCalls),
            UsageObservations = [.. previous.UsageObservations, .. usage],
            ToolInvocationTraces = [.. previous.ToolInvocationTraces, .. current.ToolInvocationTraces]
        };
    }
}

internal sealed record AgentPlanningPreparedRun(AgentPlanningRunAdmission Admission,
    AgentPlanningOwner Owner, AgentToolSemanticDigest RuntimeContract);

internal sealed record AgentPlanningExecutionResult(ExecutionRunRecord Run, AgentRuntimeResponse Response,
    AgentPlanningAdmissionRefusal? Refusal);

internal sealed record AgentPlanningPublicationResult(ExecutionRunRecord Run, AgentPlanningPublicationAssessment Assessment);
