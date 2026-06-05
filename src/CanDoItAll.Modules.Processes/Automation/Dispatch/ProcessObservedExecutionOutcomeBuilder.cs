using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessObservedExecutionOutcomeBuilder
{
    internal static ProcessRunAutomationDispatchService.DispatchExecutionOutcome Create(
        ProcessAutomationExecutionRunDetail detail,
        string responseText,
        int attemptNumber)
    {
        ArgumentNullException.ThrowIfNull(detail);

        return new ProcessRunAutomationDispatchService.DispatchExecutionOutcome(
            detail,
            responseText,
            ProcessStepRunStatus.InProgress,
            BuildCompletionReason(detail.Run),
            [],
            attemptNumber,
            null);
    }

    internal static string BuildCompletionReason(ProcessAutomationExecutionRunRecord run)
    {
        ArgumentNullException.ThrowIfNull(run);

        return $"AgentFramework run '{run.Title}' is still {run.State}; automation will observe it again after it becomes terminal or stale.";
    }
}
