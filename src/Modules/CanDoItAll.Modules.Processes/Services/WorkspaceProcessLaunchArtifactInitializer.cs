using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Processes.Application;

namespace CanDoItAll.Modules.Processes;

internal sealed class WorkspaceProcessLaunchArtifactInitializer(IWorkspaceFileService workspaceFiles) : IProcessLaunchArtifactInitializer
{
    public Task InitializeAsync(
        ProcessLaunchArtifactInitializationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        EnsureDirectory(request.ManagedArtifactRoot);
        EnsureDirectory($"{request.ManagedArtifactRoot}/steps");
        EnsureDirectory($"{request.ManagedArtifactRoot}/logs");
        EnsureDirectory($"{request.ManagedArtifactRoot}/screenshots");

        return Task.CompletedTask;
    }

    private void EnsureDirectory(string relativePath)
    {
        var result = workspaceFiles.CreateDirectory(relativePath);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(result.Message);
        }
    }
}
