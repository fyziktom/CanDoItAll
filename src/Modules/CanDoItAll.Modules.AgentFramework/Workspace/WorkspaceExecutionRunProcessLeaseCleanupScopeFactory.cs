using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.Modules.AgentFramework;

internal sealed class WorkspaceExecutionRunProcessLeaseCleanupScopeFactory(
    IReadOnlyList<IWorkspaceCommandReceiptLifecycleFactExtractor> lifecycleFactExtractors)
    : IWorkspaceExecutionRunProcessLeaseCleanupScopeFactory
{
    public IWorkspaceExecutionRunProcessLeaseCleanupScope Create(
        WorkspaceExecutionScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var processHost = new LocalWorkspaceProcessHost();
        var commandExecutionService = new WorkspaceCommandExecutionService(
            scope.WorkspaceRoot,
            processHost,
            scope.Scope,
            lifecycleFactExtractors);
        return new WorkspaceExecutionRunProcessLeaseCleanupScope(
            scope,
            commandExecutionService,
            processHost);
    }
}
