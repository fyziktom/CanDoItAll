using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

internal sealed record AgentToolCompletionAssessment(
    ExecutionState State,
    RunOutcome? Outcome,
    string FailureSummary)
{
    public static AgentToolCompletionAssessment Create(
        IReadOnlyList<AgentToolInvocationTrace> traces,
        int pendingApprovalCount,
        bool portableOutputValid)
    {
        ArgumentNullException.ThrowIfNull(traces);

        if (pendingApprovalCount > 0)
        {
            return new AgentToolCompletionAssessment(
                ExecutionState.WaitingOnTool,
                Outcome: null,
                FailureSummary: string.Empty);
        }

        if (!portableOutputValid)
        {
            return new AgentToolCompletionAssessment(
                ExecutionState.Failed,
                RunOutcome.Failed,
                FailureSummary: string.Empty);
        }

        var unresolvedMutation = traces
            .Where(trace =>
                trace.Classification == ToolInvocationClassification.Mutation &&
                trace.CompletedAtUtc.HasValue &&
                (trace.Outcome != AgentToolInvocationOutcome.Succeeded ||
                 trace.EffectState != AgentToolEffectState.Committed))
            .FirstOrDefault(trace => !IsResolvedByLaterCommittedAttempt(trace, traces));
        if (unresolvedMutation is null)
        {
            return new AgentToolCompletionAssessment(
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                FailureSummary: string.Empty);
        }

        var reason = string.IsNullOrWhiteSpace(unresolvedMutation.FailureMessage)
            ? "the mutation result or commit state could not be verified"
            : unresolvedMutation.FailureMessage.Trim();
        return new AgentToolCompletionAssessment(
            ExecutionState.Failed,
            RunOutcome.Failed,
            $"Required mutation '{unresolvedMutation.ToolName}' did not complete: {reason}");
    }

    internal static bool IsResolvedByLaterCommittedAttempt(
        AgentToolInvocationTrace failedAttempt,
        IReadOnlyList<AgentToolInvocationTrace> traces)
    {
        if (failedAttempt.EffectState != AgentToolEffectState.NotCommitted ||
            string.IsNullOrWhiteSpace(failedAttempt.OperationCorrelationKey))
        {
            return false;
        }

        return traces.Any(candidate =>
            candidate.Sequence > failedAttempt.Sequence &&
            candidate.Outcome == AgentToolInvocationOutcome.Succeeded &&
            candidate.EffectState == AgentToolEffectState.Committed &&
            string.Equals(
                candidate.ToolName,
                failedAttempt.ToolName,
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                candidate.OperationCorrelationKey,
                failedAttempt.OperationCorrelationKey,
                StringComparison.Ordinal));
    }
}