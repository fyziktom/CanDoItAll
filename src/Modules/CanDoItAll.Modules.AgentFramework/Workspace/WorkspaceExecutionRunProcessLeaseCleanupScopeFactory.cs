using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.Modules.AgentFramework;

internal sealed class WorkspaceExecutionRunProcessLeaseCleanupScopeFactory(
    IReadOnlyList<IWorkspaceCommandReceiptLifecycleFactExtractor> lifecycleFactExtractors,
    IPhysicalFileSystemPathPolicyFactory physicalPathPolicyFactory,
    IExternalTargetPathRegistryFactory externalTargetPathRegistryFactory)
    : IWorkspaceExecutionRunProcessLeaseCleanupScopeFactory
{
    public IWorkspaceExecutionRunProcessLeaseCleanupScope Create(
        WorkspaceExecutionScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var externalTargets = externalTargetPathRegistryFactory.Create(
            scope.ExternalTargetRootBindings);
        var processHost = new LocalWorkspaceProcessHost();
        var commandExecutionService = new WorkspaceCommandExecutionService(
            scope.WorkspaceRoot,
            processHost,
            physicalPathPolicyFactory,
            scope.Scope,
            lifecycleFactExtractors,
            externalTargets);
        return new WorkspaceExecutionRunProcessLeaseCleanupScope(
            scope,
            commandExecutionService,
            processHost);
    }
}
