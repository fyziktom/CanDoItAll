using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Runtime.Abstractions;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class WorkspacePublishedOutputRuntime(
    IWorkspacePublishedOutputCommands commands,
    WorkspaceRuntimeFileAccessGuard fileAccess,
    AgentWorkspaceToolAccessSettings access) : IWorkspacePublishedOutputCommands {
    // The redirect is decided here from the agent's access; a caller-supplied artifactsPath is ignored.
    public Task<WorkspaceCommandExecutionResult> DotnetPublish(string targetPath, string configuration = "Release", bool noRestore = false, string? workingDirectory = null, int timeoutSeconds = 600, string? artifactsPath = null) {
        RequireValidation();
        var allowedTargetPath = fileAccess.PrepareFileReadPath(targetPath)!;
        var allowedWorkingDirectory = fileAccess.PrepareFileReadPath(workingDirectory);
        return commands.DotnetPublish(allowedTargetPath, configuration, noRestore, allowedWorkingDirectory, timeoutSeconds,
            WorkspaceReadOnlyBuildOutput.ResolveRelativePath(
                fileAccess.ResolveExternalTargetAccess(),
                allowedTargetPath,
                allowedWorkingDirectory,
                WorkspaceProcessEnvironmentSettings.Current.RedirectReadOnlyDotnetOutput));
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
