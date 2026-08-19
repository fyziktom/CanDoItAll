using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.Tests.Unit;

internal static class TestWorkspaceServices
{
    internal static IPhysicalFileSystemPathPolicyFactory PhysicalPathPolicyFactory { get; } =
        new PhysicalFileSystemPathPolicyFactory();

    internal static WorkspacePathPolicy CreatePathPolicy(
        string workspaceRoot,
        WorkspaceScopeDescriptor? workspaceScope = null,
        IExternalTargetPathRegistry? externalTargetRegistry = null)
        => new(
            workspaceRoot,
            PhysicalPathPolicyFactory,
            workspaceScope,
            externalTargetRegistry);

    internal static WorkspaceFileService CreateFileService(
        string workspaceRoot,
        WorkspaceScopeDescriptor? workspaceScope = null,
        IExternalTargetPathRegistry? externalTargetRegistry = null)
        => new(
            workspaceRoot,
            PhysicalPathPolicyFactory,
            workspaceScope,
            externalTargetRegistry);

    internal static WorkspacePathResolutionService CreatePathResolutionService(
        string workspaceRoot,
        WorkspaceScopeDescriptor? workspaceScope = null,
        IExternalTargetPathRegistry? externalTargetRegistry = null)
        => new(
            workspaceRoot,
            PhysicalPathPolicyFactory,
            workspaceScope,
            externalTargetRegistry);

    internal static WorkspaceCommandExecutionService CreateCommandExecutionService(
        string workspaceRoot,
        IWorkspaceProcessHost processHost,
        WorkspaceScopeDescriptor? workspaceScope = null,
        IEnumerable<IWorkspaceCommandReceiptLifecycleFactExtractor>? lifecycleFactExtractors = null,
        IExternalTargetPathRegistry? externalTargetRegistry = null,
        Func<Uri, CancellationToken, Task<bool>>? dotnetReadinessProbe = null)
        => new(
            workspaceRoot,
            processHost,
            PhysicalPathPolicyFactory,
            workspaceScope,
            lifecycleFactExtractors,
            externalTargetRegistry,
            dotnetReadinessProbe ?? (static (_, _) => Task.FromResult(true)));

    internal static WorkspaceImageOperationService CreateImageOperationService(
        string workspaceRoot,
        WorkspaceScopeDescriptor? workspaceScope = null,
        IExternalTargetPathRegistry? externalTargetRegistry = null)
        => new(
            workspaceRoot,
            PhysicalPathPolicyFactory,
            workspaceScope,
            externalTargetRegistry);

    internal static WorkspaceImageOperationService CreateImageOperationService(
        string workspaceRoot,
        WorkspaceScopeDescriptor? workspaceScope,
        Func<string, byte[]> readAllBytes,
        IExternalTargetPathRegistry? externalTargetRegistry = null)
        => new(
            workspaceRoot,
            PhysicalPathPolicyFactory,
            workspaceScope,
            readAllBytes,
            externalTargetRegistry);

    internal static WorkspaceArtifactToolService CreateArtifactToolService(
        string workspaceRoot,
        IWorkspaceCommandExecutionService commandExecutionService,
        IWorkspaceDocumentMarkdownConverter documentMarkdownConverter,
        WorkspaceScopeDescriptor? workspaceScope = null)
    {
        var externalTargets = TestExternalTargetPathRegistry.Create();
        return new WorkspaceArtifactToolService(
            workspaceRoot,
            commandExecutionService,
            documentMarkdownConverter,
            PhysicalPathPolicyFactory,
            workspaceScope,
            CreateImageOperationService(workspaceRoot, workspaceScope, externalTargets),
            externalTargets);
    }

    internal static WorkspaceArtifactToolService CreateArtifactToolService(
        string workspaceRoot,
        IWorkspaceCommandExecutionService commandExecutionService,
        IWorkspaceDocumentMarkdownConverter documentMarkdownConverter,
        WorkspaceScopeDescriptor? workspaceScope,
        IWorkspaceImageOperationService imageOperationService,
        IExternalTargetPathRegistry externalTargetRegistry)
        => new(
            workspaceRoot,
            commandExecutionService,
            documentMarkdownConverter,
            PhysicalPathPolicyFactory,
            workspaceScope,
            imageOperationService,
            externalTargetRegistry);
}
