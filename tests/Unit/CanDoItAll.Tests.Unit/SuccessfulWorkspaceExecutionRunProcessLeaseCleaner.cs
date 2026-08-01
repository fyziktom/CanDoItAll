using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.Tests.Unit;

internal sealed class SuccessfulWorkspaceExecutionRunProcessLeaseCleaner
    : IWorkspaceExecutionRunProcessLeaseCleaner
{
    public static SuccessfulWorkspaceExecutionRunProcessLeaseCleaner Instance { get; } = new();

    private SuccessfulWorkspaceExecutionRunProcessLeaseCleaner()
    {
    }

    public Task<WorkspaceExecutionRunProcessCleanupResult> CleanupAsync(Guid executionRunId)
        => Task.FromResult(
            WorkspaceExecutionRunProcessCleanupResult.Empty(executionRunId));
}
