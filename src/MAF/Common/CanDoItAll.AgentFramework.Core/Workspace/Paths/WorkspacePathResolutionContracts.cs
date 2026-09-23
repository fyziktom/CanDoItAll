using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Infrastructure.FileSystem;

namespace CanDoItAll.AgentFramework.Core;

public sealed record WorkspaceResolvedPath(
    string FullPath,
    string RelativePath,
    bool IsWorkspacePath);

public interface IWorkspacePathResolutionService
{
    WorkspaceExecutionScope ExecutionScope => throw new NotSupportedException(
        "This workspace path owner does not expose its immutable execution scope.");

    WorkspaceResolvedPath ResolvePath(string path) => throw new NotSupportedException(
        "This workspace path owner does not expose its actual generic path resolution.");

    /// <summary>
    /// Returns the path owner for the same workspace root and external-target registry bound to another workspace
    /// scope, such as the active scope of an agent run whose workspace tools map managed roots into that scope.
    /// </summary>
    IWorkspacePathResolutionService ForScope(WorkspaceScopeDescriptor workspaceScope) => throw new NotSupportedException(
        "This workspace path owner cannot derive a path owner for another workspace scope.");

    WorkspaceResolvedPath ResolveFilePath(string path, bool allowMissing);

    WorkspaceResolvedPath ResolveDirectoryPath(string path, bool allowMissing);

    WorkspaceResolvedPath ResolveDirectoryPath(
        string path,
        bool allowMissing,
        IReadOnlyList<string>? allowedExternalRoots)
    {
        if (allowedExternalRoots is { Count: > 0 })
        {
            throw WorkspacePathResolutionException.OutsideWorkspace(
                "This workspace path resolver does not support external-root authority.");
        }

        return ResolveDirectoryPath(path, allowMissing);
    }
}

public sealed class WorkspacePathResolutionService : IWorkspacePathResolutionService
{
    private readonly WorkspacePathPolicy pathPolicy;
    private readonly IPhysicalFileSystemPathPolicyFactory physicalPathPolicyFactory;
    private readonly IExternalTargetPathRegistry? externalTargetRegistry;

    public WorkspacePathResolutionService(
        string workspaceRoot,
        IPhysicalFileSystemPathPolicyFactory physicalPathPolicyFactory,
        WorkspaceScopeDescriptor? workspaceScope = null,
        IExternalTargetPathRegistry? externalTargetRegistry = null)
    {
        pathPolicy = new WorkspacePathPolicy(
            workspaceRoot,
            physicalPathPolicyFactory,
            workspaceScope,
            externalTargetRegistry);
        this.physicalPathPolicyFactory = physicalPathPolicyFactory;
        this.externalTargetRegistry = externalTargetRegistry;
        ExecutionScope = new WorkspaceExecutionScope(pathPolicy.WorkspaceRoot, pathPolicy.WorkspaceScope,
            rootCaseSensitivity: physicalPathPolicyFactory.Create(pathPolicy.WorkspaceRoot).CaseSensitivity);
    }

    public WorkspaceExecutionScope ExecutionScope { get; }

    public IWorkspacePathResolutionService ForScope(WorkspaceScopeDescriptor workspaceScope)
    {
        ArgumentNullException.ThrowIfNull(workspaceScope);
        return workspaceScope == ExecutionScope.Scope
            ? this
            : new WorkspacePathResolutionService(
                ExecutionScope.WorkspaceRoot,
                physicalPathPolicyFactory,
                workspaceScope,
                externalTargetRegistry);
    }

    public WorkspaceResolvedPath ResolvePath(string path) {
        var resolution = pathPolicy.ResolveAccessiblePath(path);
        return new(resolution.FullPath, resolution.RelativePath, resolution.IsWorkspacePath);
    }

    public WorkspaceResolvedPath ResolveFilePath(string path, bool allowMissing)
    {
        var resolution = allowMissing
            ? pathPolicy.ResolveAccessiblePath(path)
            : pathPolicy.ResolveExistingPath(path, allowFiles: true, allowDirectories: false);

        if (!allowMissing && !File.Exists(resolution.FullPath))
        {
            throw WorkspacePathResolutionException.PathMissing(
                $"Path '{resolution.DisplayPath}' does not resolve to an existing file.");
        }

        if (Directory.Exists(resolution.FullPath))
        {
            throw WorkspacePathResolutionException.FileRequired(
                $"Path '{resolution.DisplayPath}' resolves to a directory, but a file was required.");
        }

        return new WorkspaceResolvedPath(
            resolution.FullPath,
            resolution.RelativePath,
            resolution.IsWorkspacePath);
    }

    public WorkspaceResolvedPath ResolveDirectoryPath(string path, bool allowMissing)
        => ResolveDirectoryPath(path, allowMissing, allowedExternalRoots: null);

    public WorkspaceResolvedPath ResolveDirectoryPath(
        string path,
        bool allowMissing,
        IReadOnlyList<string>? allowedExternalRoots)
    {
        var resolution = allowMissing
            ? pathPolicy.ResolveAccessiblePath(path, allowedExternalRoots)
            : pathPolicy.ResolveExistingPath(
                path,
                allowFiles: false,
                allowDirectories: true,
                allowedExternalRoots);

        if (!allowMissing && !Directory.Exists(resolution.FullPath))
        {
            throw WorkspacePathResolutionException.PathMissing(
                $"Path '{resolution.DisplayPath}' does not resolve to an existing directory.");
        }

        if (File.Exists(resolution.FullPath))
        {
            throw WorkspacePathResolutionException.DirectoryRequired(
                $"Path '{resolution.DisplayPath}' resolves to a file, but a directory was required.");
        }

        return new WorkspaceResolvedPath(
            resolution.FullPath,
            resolution.RelativePath,
            resolution.IsWorkspacePath);
    }
}
