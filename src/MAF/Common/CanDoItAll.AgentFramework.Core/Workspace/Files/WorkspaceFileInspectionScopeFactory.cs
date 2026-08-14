using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.AgentFramework.Core;

public sealed class WorkspaceFileInspectionScopeFactory
{
    private readonly string workspaceRoot;
    private readonly WorkspaceScopeDescriptor workspaceScope;
    private readonly IPhysicalFileSystemPathPolicyFactory physicalFileSystemPathPolicyFactory;
    private readonly IExternalTargetPathRegistryFactory externalTargetPathRegistryFactory;

    public WorkspaceFileInspectionScopeFactory(
        string workspaceRoot,
        WorkspaceScopeDescriptor workspaceScope,
        IPhysicalFileSystemPathPolicyFactory physicalFileSystemPathPolicyFactory,
        IExternalTargetPathRegistryFactory externalTargetPathRegistryFactory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        this.workspaceRoot = workspaceRoot;
        this.workspaceScope = workspaceScope ?? throw new ArgumentNullException(nameof(workspaceScope));
        this.physicalFileSystemPathPolicyFactory = physicalFileSystemPathPolicyFactory ??
            throw new ArgumentNullException(nameof(physicalFileSystemPathPolicyFactory));
        this.externalTargetPathRegistryFactory = externalTargetPathRegistryFactory ??
            throw new ArgumentNullException(nameof(externalTargetPathRegistryFactory));
    }

    public IWorkspaceFileInspectionService Create(
        IEnumerable<ExternalTargetRootBinding> externalTargetRootBindings)
    {
        ArgumentNullException.ThrowIfNull(externalTargetRootBindings);
        return new WorkspaceFileService(
            workspaceRoot,
            physicalFileSystemPathPolicyFactory,
            workspaceScope,
            externalTargetPathRegistryFactory.Create(externalTargetRootBindings));
    }
}
