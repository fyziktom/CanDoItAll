using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.Tests.Unit;

internal sealed class TestWorkspaceExecutionRunProcessLeaseCleanupScopeFactory(
    Func<IWorkspaceProcessHost> createProcessHost)
    : IWorkspaceExecutionRunProcessLeaseCleanupScopeFactory
{
    public IWorkspaceExecutionRunProcessLeaseCleanupScope Create(
        WorkspaceExecutionScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var processHost = createProcessHost()
            ?? throw new InvalidOperationException(
                "The test cleanup scope factory produced no process host.");
        var commandExecutionService = new WorkspaceCommandExecutionService(
            scope.WorkspaceRoot,
            processHost,
            scope.Scope);
        return new WorkspaceExecutionRunProcessLeaseCleanupScope(
            scope,
            commandExecutionService,
            processHost);
    }
}
