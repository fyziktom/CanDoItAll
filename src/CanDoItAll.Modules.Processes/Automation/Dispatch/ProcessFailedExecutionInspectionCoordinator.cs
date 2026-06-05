using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessFailedExecutionInspectionCoordinator
{
    internal static async Task<ProcessExecutionAttemptResult> InspectAsync(
        IProcessAutomationExecutionClient executionClient,
        ProcessRunAutomationDispatchService.DispatchCandidate candidate,
        ProcessAutomationExecutionFailedException exception,
        Func<ProcessRunAutomationDispatchService.DispatchCandidate, string?, ProcessAutomationExecutionRunDetail, string> resolvePreferredResponseText,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(executionClient);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(resolvePreferredResponseText);

        var detail = await executionClient.GetExecutionRunDetailAsync(
            exception.ExecutionRunId,
            cancellationToken);
        return new ProcessExecutionAttemptResult(
            exception.ExecutionRunId,
            detail,
            resolvePreferredResponseText(candidate, exception.Message, detail),
            exception.ChatSessionId);
    }
}
