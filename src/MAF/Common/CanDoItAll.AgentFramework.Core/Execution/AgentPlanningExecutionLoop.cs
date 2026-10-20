using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Runtime.Abstractions;

namespace CanDoItAll.AgentFramework.Core;

internal sealed class AgentPlanningExecutionLoop(AgentPlanningDecisionOwner owner, IAgentPlanningStepRuntime runtime) {
    internal async Task<AgentPlanningExecutionResult> RunAsync(AgentDefinition agent, ProviderProfile provider, AgentToolRunLease lease,
        IAgentPlanningStepParticipant participant, AgentToolSemanticDigest contract,
        Func<AgentPlanningLiveWork, CancellationToken, Task<ExecutionRunRecord>> bindBasis, CancellationToken cancellationToken) {
        Exception? primary = null;
        try {
            using var bound = lease.Bind();
            var work = lease.PlanningWork ?? throw new InvalidOperationException("The admitted planning lease has no work interval.");
            return await RunCoreAsync(agent, provider, work, participant, contract, bindBasis, cancellationToken);
        } catch (Exception failure) {
            primary = failure;
            throw;
        } finally {
            try {
                await lease.DisposeAsync();
            } catch (Exception cleanup) when (primary is not null) {
                throw new AggregateException(primary, cleanup);
            }
        }
    }

    private async Task<AgentPlanningExecutionResult> RunCoreAsync(AgentDefinition agent, ProviderProfile provider, AgentPlanningLiveWork work,
        IAgentPlanningStepParticipant participant, AgentToolSemanticDigest contract,
        Func<AgentPlanningLiveWork, CancellationToken, Task<ExecutionRunRecord>> bindBasis, CancellationToken cancellationToken) {
        AgentRuntimeResponse? accumulated = null;
        try {
            while (true) {
                await bindBasis(work, cancellationToken);
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
