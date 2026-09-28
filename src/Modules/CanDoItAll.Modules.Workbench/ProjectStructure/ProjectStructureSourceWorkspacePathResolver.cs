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

    public WorkspaceResolvedPath ResolveExistingFile(
        Guid projectId,
        string sourceWorkspacePath,
        WorkspaceScopeDescriptor? activeWorkspaceScope = null)
    {
        var (effectivePaths, explicitTargetScope) = ResolveEffectivePathService(
            projectId,
            sourceWorkspacePath,
            activeWorkspaceScope);
        WorkspaceResolvedPath resolution;
        try
        {
            resolution = effectivePaths.ResolveFilePath(sourceWorkspacePath, allowMissing: true);
        }
        catch (WorkspacePathResolutionException ex)
        {
            throw CreateSourceRejection(
                400,
                "SourceWorkspacePathInvalid",
                "Source workspace path must be a valid path inside the active workspace scope.",
                diagnosticDetails: new { exceptionType = ex.GetType().Name });
        }

        if (!resolution.IsWorkspacePath)
        {
            throw CreateSourceRejection(
                400,
                "SourceWorkspacePathInvalid",
                "Source workspace path must resolve inside the active workspace scope.");
        }

        if (explicitTargetScope is not null &&
            !explicitTargetScope.ManagedRootRelativePaths.Any(root => MatchesRoot(resolution.RelativePath, root)))
        {
            throw CreateScopeDeniedException(projectId);
        }

        if (!File.Exists(resolution.FullPath))
        {
            throw CreateSourceRejection(
                404,
                "SourceWorkspaceFileNotFound",
                $"Source workspace file was not found at resolved workspace path '{resolution.RelativePath}'.");
        }

        return resolution;
    }

    private (IWorkspacePathResolutionService Paths, WorkspaceScopeDescriptor? ExplicitTargetScope) ResolveEffectivePathService(
        Guid projectId,
        string sourceWorkspacePath,
        WorkspaceScopeDescriptor? activeWorkspaceScope)
    {
        var normalizedPath = WorkspaceScopeDescriptor.NormalizeRelativePath(sourceWorkspacePath);
        if (ContainsDotPathSegment(normalizedPath))
        {
            throw CreateSourceRejection(
                400,
                "SourceWorkspacePathInvalid",
                "Source workspace path must be canonical and cannot contain '.' or '..' segments.");
        }

        if (IsExplicitProjectManagedScopePath(normalizedPath))
        {
            var targetScope = WorkspaceScopeDescriptor.Project(projectId.ToString("D"));
            if (!targetScope.ManagedRootRelativePaths.Any(root => MatchesRoot(normalizedPath, root)))
            {
                throw CreateScopeDeniedException(projectId);
            }

            return (CreatePathService(targetScope), targetScope);
        }

        // A scope-relative managed path means the scope the caller's own workspace tools resolved it in; an
        // explicitly scoped path keeps naming the host workspace scope.
        return activeWorkspaceScope is null || IsExplicitManagedScopePath(normalizedPath)
            ? (workspacePaths, null)
            : (CreatePathService(activeWorkspaceScope), null);
    }

    private WorkspacePathResolutionService CreatePathService(WorkspaceScopeDescriptor scope)
        => new(workspaceRoot, physicalPathPolicyFactory, scope, externalTargetPathRegistry);

    private static bool ContainsDotPathSegment(string normalizedPath)
        => SplitSegments(normalizedPath)
            .Any(segment => segment is "." or "..");

    private static string ResolveWorkspaceRoot(IWorkspacePathResolver resolver)
    {
        var root = resolver.ResolveWorkspaceRoot();
        PhysicalPathSyntaxPolicy.EnsureNativeOrRelative(root, "project-structure workspace root");
        return Path.GetFullPath(root);
    }

    private static ProjectStructureAgentException CreateScopeDeniedException(Guid projectId)
        => CreateSourceRejection(
            403,
            "SourceWorkspaceScopeDenied",
            $"Project asset sources may use only the active workspace scope or the target project scope for project '{projectId:D}'.");

    // Source resolution runs before the asset owner stores or writes anything, so every rejection is a proven
    // no-effect failure that the model may correct and retry.
    private static ProjectStructureAgentException CreateSourceRejection(
        int statusCode,
        string errorCode,
        string safeMessage,
        object? diagnosticDetails = null)
        => ProjectStructureAgentException.CreateAgentVisible(
            statusCode,
            errorCode,
            safeMessage,
            canRetryWithCorrectedInput: true,
            diagnosticDetails,
            AgentToolEffectState.NotCommitted);

    private static bool IsExplicitProjectManagedScopePath(string normalizedPath)
    {
        var segments = SplitSegments(normalizedPath);
        return segments.Length >= 4 &&
               IsExplicitManagedScope(segments) &&
               Enum.TryParse<WorkspaceScopeKind>(segments[2], ignoreCase: true, out var scopeKind) &&
               scopeKind == WorkspaceScopeKind.Project;
    }

    private static bool IsExplicitManagedScopePath(string normalizedPath)
        => IsExplicitManagedScope(SplitSegments(normalizedPath));

    private static bool IsExplicitManagedScope(string[] segments)
        => segments.Length >= 2 &&
           WorkspaceScopeDescriptor.ManagedRootNames.Contains(
               segments[0],
               StringComparer.OrdinalIgnoreCase) &&
           string.Equals(segments[1], "scopes", StringComparison.OrdinalIgnoreCase);

    private static string[] SplitSegments(string normalizedPath)
        => normalizedPath.Split(
            '/',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static bool MatchesRoot(string normalizedPath, string root)
    {
        return string.Equals(normalizedPath, root, StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase);
    }
}
