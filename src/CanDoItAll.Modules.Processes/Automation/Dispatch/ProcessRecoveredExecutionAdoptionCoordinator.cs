using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessRecoveredExecutionAdoptionCoordinator
{
    internal static async Task<ProcessRecoveredExecutionAdoption> AdoptAsync(
        IProcessAutomationExecutionClient executionClient,
        Guid executionRunId,
        Func<ProcessAutomationExecutionRunDetail, string> resolveResponseText,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(executionClient);
        ArgumentNullException.ThrowIfNull(resolveResponseText);

        var detail = await executionClient.GetExecutionRunDetailAsync(executionRunId, cancellationToken);
        return new ProcessRecoveredExecutionAdoption(
            executionRunId,
            detail,
            resolveResponseText(detail),
            detail.Run.ChatSessionId);
    }
}

internal sealed record ProcessRecoveredExecutionAdoption(
    Guid ExecutionRunId,
    ProcessAutomationExecutionRunDetail Detail,
    string ResponseText,
    Guid? ChatSessionId);
