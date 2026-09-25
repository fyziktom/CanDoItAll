using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Runtime.Abstractions;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class WorkspacePublishedOutputRuntime(
    IWorkspacePublishedOutputCommands commands,
    WorkspaceRuntimeFileAccessGuard fileAccess,
    AgentWorkspaceToolAccessSettings access) : IWorkspacePublishedOutputCommands {
    public Task<WorkspaceCommandExecutionResult> DotnetPublish(string targetPath, string configuration = "Release", bool noRestore = false, string? workingDirectory = null, int timeoutSeconds = 600) {
        RequireValidation();
        return commands.DotnetPublish(fileAccess.PrepareFileReadPath(targetPath)!, configuration, noRestore,
            fileAccess.PrepareFileReadPath(workingDirectory), timeoutSeconds);
    }

    public Task<WorkspaceCommandExecutionResult> ServeStaticFiles(string hostAssemblyPath, string directoryPath, string? url = null, bool spaFallback = true, int startupTimeoutSeconds = 45) {
        RequireValidation();
        return commands.ServeStaticFiles(hostAssemblyPath, fileAccess.PrepareFileReadPath(directoryPath)!, url, spaFallback, startupTimeoutSeconds);
    }

    private void RequireValidation() {
        if (!access.CanRunValidationCommands) {
            throw new InvalidOperationException("This agent is not allowed to run workspace validation commands.");
        }
    }
}
