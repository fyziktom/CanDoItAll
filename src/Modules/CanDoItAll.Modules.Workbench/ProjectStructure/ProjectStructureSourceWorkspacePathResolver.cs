using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectStructureSourceWorkspacePathResolver(IWorkspacePathResolutionService workspacePaths)
{
    public WorkspaceResolvedPath ResolveExistingFile(string sourceWorkspacePath)
    {
        WorkspaceResolvedPath resolution;
        try
        {
            resolution = workspacePaths.ResolveFilePath(sourceWorkspacePath, allowMissing: true);
        }
        catch (InvalidOperationException ex)
        {
            throw new ProjectStructureAgentException(
                400,
                "SourceWorkspacePathInvalid",
                ex.Message,
                ex.Message);
        }

        if (!resolution.IsWorkspacePath)
        {
            throw new ProjectStructureAgentException(
                400,
                "SourceWorkspacePathInvalid",
                "Source workspace path must resolve inside the active workspace.");
        }

        if (!File.Exists(resolution.FullPath))
        {
            throw new ProjectStructureAgentException(
                404,
                "SourceWorkspaceFileNotFound",
                $"Source workspace file '{sourceWorkspacePath}' was not found. Resolved workspace path: '{resolution.RelativePath}'.");
        }

        return resolution;
    }
}
