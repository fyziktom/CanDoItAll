using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectStructureSourceWorkspacePathResolver(
    IWorkspacePathResolutionService workspacePaths,
    IWorkspacePathResolver workspacePathResolver,
    IPhysicalFileSystemPathPolicyFactory physicalPathPolicyFactory,
    IExternalTargetPathRegistry externalTargetPathRegistry)
{
    private readonly string workspaceRoot = ResolveWorkspaceRoot(workspacePathResolver);

    public WorkspaceResolvedPath ResolveExistingFile(Guid projectId, string sourceWorkspacePath)
    {
        var (effectivePaths, explicitTargetScope) = ResolveEffectivePathService(projectId, sourceWorkspacePath);
        WorkspaceResolvedPath resolution;
        try
        {
            resolution = effectivePaths.ResolveFilePath(sourceWorkspacePath, allowMissing: true);
        }
        catch (WorkspacePathResolutionException ex)
        {
            throw ProjectStructureAgentException.CreateAgentVisible(
                400,
                "SourceWorkspacePathInvalid",
                "Source workspace path must be a valid path inside the active workspace scope.",
                canRetryWithCorrectedInput: true,
                diagnosticDetails: new { exceptionType = ex.GetType().Name });
        }

        if (!resolution.IsWorkspacePath)
        {
            throw ProjectStructureAgentException.CreateAgentVisible(
                400,
                "SourceWorkspacePathInvalid",
                "Source workspace path must resolve inside the active workspace scope.",
                canRetryWithCorrectedInput: true);
        }

        if (explicitTargetScope is not null &&
            !explicitTargetScope.ManagedRootRelativePaths.Any(root => MatchesRoot(resolution.RelativePath, root)))
        {
            throw CreateScopeDeniedException(projectId);
        }

        if (!File.Exists(resolution.FullPath))
        {
            throw ProjectStructureAgentException.CreateAgentVisible(
                404,
                "SourceWorkspaceFileNotFound",
                $"Source workspace file was not found at resolved workspace path '{resolution.RelativePath}'.",
                canRetryWithCorrectedInput: true);
        }

        return resolution;
    }

    private (IWorkspacePathResolutionService Paths, WorkspaceScopeDescriptor? ExplicitTargetScope) ResolveEffectivePathService(
        Guid projectId,
        string sourceWorkspacePath)
    {
        var normalizedPath = WorkspaceScopeDescriptor.NormalizeRelativePath(sourceWorkspacePath);
        if (ContainsDotPathSegment(normalizedPath))
        {
            throw ProjectStructureAgentException.CreateAgentVisible(
                400,
                "SourceWorkspacePathInvalid",
                "Source workspace path must be canonical and cannot contain '.' or '..' segments.",
                canRetryWithCorrectedInput: true);
        }

        if (!IsExplicitProjectManagedScopePath(normalizedPath))
        {
            return (workspacePaths, null);
        }

        var targetScope = WorkspaceScopeDescriptor.Project(projectId.ToString("D"));
        if (!targetScope.ManagedRootRelativePaths.Any(root => MatchesRoot(normalizedPath, root)))
        {
            throw CreateScopeDeniedException(projectId);
        }

        return (new WorkspacePathResolutionService(
            workspaceRoot,
            physicalPathPolicyFactory,
            targetScope,
            externalTargetPathRegistry), targetScope);
    }

    private static bool ContainsDotPathSegment(string normalizedPath)
        => normalizedPath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(segment => segment is "." or "..");

    private static string ResolveWorkspaceRoot(IWorkspacePathResolver resolver)
    {
        var root = resolver.ResolveWorkspaceRoot();
        PhysicalPathSyntaxPolicy.EnsureNativeOrRelative(root, "project-structure workspace root");
        return Path.GetFullPath(root);
    }

    private static ProjectStructureAgentException CreateScopeDeniedException(Guid projectId)
        => ProjectStructureAgentException.CreateAgentVisible(
            403,
            "SourceWorkspaceScopeDenied",
            $"Project asset sources may use only the active workspace scope or the target project scope for project '{projectId:D}'.",
            canRetryWithCorrectedInput: true);

    private static bool IsExplicitProjectManagedScopePath(string normalizedPath)
    {
        var segments = normalizedPath.Split(
            '/',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Length >= 4 &&
               WorkspaceScopeDescriptor.ManagedRootNames.Contains(
                   segments[0],
                   StringComparer.OrdinalIgnoreCase) &&
               string.Equals(segments[1], "scopes", StringComparison.OrdinalIgnoreCase) &&
               Enum.TryParse<WorkspaceScopeKind>(segments[2], ignoreCase: true, out var scopeKind) &&
               scopeKind == WorkspaceScopeKind.Project;
    }

    private static bool MatchesRoot(string normalizedPath, string root)
    {
        return string.Equals(normalizedPath, root, StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase);
    }
}
