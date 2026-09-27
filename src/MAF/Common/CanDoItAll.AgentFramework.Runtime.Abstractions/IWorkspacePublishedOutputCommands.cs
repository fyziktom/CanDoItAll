using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Runtime.Abstractions;

public interface IWorkspacePublishedOutputCommands {
    // artifactsPath: workspace-relative folder for intermediate build output, set only for read-only targets.
    Task<WorkspaceCommandExecutionResult> DotnetPublish(string targetPath, string configuration = "Release", bool noRestore = false, string? workingDirectory = null, int timeoutSeconds = 600, string? artifactsPath = null);

    Task<WorkspaceCommandExecutionResult> ServeStaticFiles(string hostAssemblyPath, string directoryPath, string? url = null, bool spaFallback = true, int startupTimeoutSeconds = 45);
}
