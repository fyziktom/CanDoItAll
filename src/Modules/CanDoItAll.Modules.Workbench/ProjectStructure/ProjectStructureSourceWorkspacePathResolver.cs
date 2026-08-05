using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectStructureSourceWorkspacePathResolver(
    IWorkspacePathResolutionService workspacePaths,
    IWorkspacePathResolver workspacePathResolver)
{
    private readonly string workspaceRoot = Path.GetFullPath(workspacePathResolver.ResolveWorkspaceRoot());

    public WorkspaceResolvedPath ResolveExistingFile(Guid projectId, string sourceWorkspacePath)
    {
        var effectivePaths = ResolveEffectivePathService(projectId, sourceWorkspacePath);
        WorkspaceResolvedPath resolution;
        try
        {
            resolution = effectivePaths.ResolveFilePath(sourceWorkspacePath, allowMissing: true);
        }
        catch (InvalidOperationException ex)
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

    private IWorkspacePathResolutionService ResolveEffectivePathService(
        Guid projectId,
        string sourceWorkspacePath)
    {
        var normalizedPath = WorkspaceScopeDescriptor.NormalizeRelativePath(sourceWorkspacePath);
        if (!IsProjectArtifactScopePath(normalizedPath))
        {
            return workspacePaths;
        }

        var targetScope = WorkspaceScopeDescriptor.Project(projectId.ToString("D"));
        if (!MatchesRoot(normalizedPath, targetScope.ArtifactRootRelativePath))
        {
            throw ProjectStructureAgentException.CreateAgentVisible(
                403,
                "SourceWorkspaceScopeDenied",
                $"Project asset sources may use only the active workspace scope or the target project scope for project '{projectId:D}'.",
                canRetryWithCorrectedInput: true);
        }

        return new WorkspacePathResolutionService(workspaceRoot, targetScope);
    }

    private static bool IsProjectArtifactScopePath(string normalizedPath)
    {
        var segments = normalizedPath.Split(
            '/',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Length >= 4 &&
               string.Equals(segments[0], "artifacts", StringComparison.OrdinalIgnoreCase) &&
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
