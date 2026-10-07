using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.Integration;

namespace CanDoItAll.Modules.Projects;

internal sealed record ProjectFilesOwnedWorkspace(
    IAsyncDisposable Owner,
    IFileBrowserSession Browser,
    Func<FileBrowserSourceId, FileToolsSemanticScope> ResolveScope,
    Func<FileBrowserSourceId, FileToolsBrowseSourceActionAvailability> Availability,
    Func<FileBrowserItemKey, CancellationToken, ValueTask<ProjectFilesPilotInteraction>> Activate,
    int ProjectCount,
    string Summary) {
    public static ProjectFilesOwnedWorkspace From(ProjectFilesPilotWorkspace workspace, IProjectFilesPilotCoordinator coordinator)
        => new(workspace, workspace.Browser, source => {
            ObjectDisposedException.ThrowIf(workspace.IsDisposed, workspace);
            RequireSource(workspace.Browser, source);
            return workspace.Scope;
        }, workspace.GetActionAvailability, (key, token) => coordinator.ActivateAsync(workspace, key, token), 1, workspace.ProjectName);

    public static ProjectFilesOwnedWorkspace From(ProjectFilePortfolioWorkspace workspace, IProjectFilePortfolioCoordinator coordinator)
        => new(workspace, workspace.Browser, source => {
            ObjectDisposedException.ThrowIf(workspace.IsDisposed, workspace);
            if (!workspace.TryGetScope(source, out var scope) || scope is null) {
                throw RemovedSource();
            }
            return scope;
        }, workspace.GetActionAvailability, (key, token) => coordinator.ActivateAsync(workspace, key, token), workspace.ProjectCount,
            $"{workspace.ProjectCount} project(s) · {workspace.SourceCount} source(s) · revision {workspace.Revision.Value[..12]}");

    private static void RequireSource(IFileBrowserSession browser, FileBrowserSourceId source) {
        if (!browser.Snapshot.Sources.Any(item => item.Id == source)) {
            throw RemovedSource();
        }
    }

    private static FileBrowserProviderException RemovedSource()
        => new(new(FileBrowserErrorCode.Conflict, "The selected file source is no longer part of this project view."));
}
