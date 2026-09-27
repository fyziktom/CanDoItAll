using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

internal sealed record AgentToolCompletionAssessment(
    ExecutionState State,
    RunOutcome? Outcome,
    string FailureSummary,
    AgentToolCompletionFailureKind FailureKind = AgentToolCompletionFailureKind.None)
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
                FailureSummary: string.Empty,
                AgentToolCompletionFailureKind.PortableOutputValidation);
        }

        // The summary names one unresolved attempt: an uncertain effect before a proven no-effect rejection, and otherwise
        // the latest attempt, because the agent may already have corrected the input an earlier attempt was refused for.
        var unresolvedMutation = traces
            .Where(trace =>
                trace.Classification == ToolInvocationClassification.Mutation &&
                trace.CompletedAtUtc.HasValue &&
                (trace.Outcome != AgentToolInvocationOutcome.Succeeded ||
                 trace.EffectState != AgentToolEffectState.Committed))
            .Where(trace => !IsProhibitedBeforeEffect(trace))
            .Where(trace => !IsResolvedByLaterCommittedAttempt(trace, traces))
            .OrderByDescending(trace => trace.EffectState is not (AgentToolEffectState.NotCommitted or AgentToolEffectState.None))
            .ThenByDescending(trace => trace.Sequence)
            .FirstOrDefault();
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
            $"Required mutation '{unresolvedMutation.ToolName}' did not complete: {reason}",
            AgentToolCompletionFailureKind.RequiredMutation);
    }

    // An owner that never permits the requested operation refuses it before any effect and directs the agent to
    // another approach. No retry of that operation can commit, so the refusal is not a mutation the run still owes.
    internal static bool IsProhibitedBeforeEffect(AgentToolInvocationTrace trace)
        => trace.EffectState is AgentToolEffectState.NotCommitted or AgentToolEffectState.None &&
           string.Equals(
               trace.FailureCode,
               AgentToolInvocationEffectScope.OperationProhibitedFailureCode,
               StringComparison.Ordinal);

    internal static bool IsResolvedByLaterCommittedAttempt(
        AgentToolInvocationTrace failedAttempt,
        IReadOnlyList<AgentToolInvocationTrace> traces)
    {
        // A pre-invoke failure (NotCommitted) and a typed rejection with proven no effect (None, for example an owner
        // refusing invalid asset content before any write) are both resolved when a later attempt of the same tool for
        // the same operation identity committed. Unknown effect states are never resolved this way.
        if (failedAttempt.EffectState is not (AgentToolEffectState.NotCommitted or AgentToolEffectState.None) ||
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
internal enum AgentToolCompletionFailureKind { None, PortableOutputValidation, RequiredMutation }
