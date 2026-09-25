using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Runtime.Abstractions;

public interface IWorkspacePublishedOutputCommands {
    Task<WorkspaceCommandExecutionResult> DotnetPublish(string targetPath, string configuration = "Release", bool noRestore = false, string? workingDirectory = null, int timeoutSeconds = 600);

    Task<WorkspaceCommandExecutionResult> ServeStaticFiles(string hostAssemblyPath, string directoryPath, string? url = null, bool spaFallback = true, int startupTimeoutSeconds = 45);
}
