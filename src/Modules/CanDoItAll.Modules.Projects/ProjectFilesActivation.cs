using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.Integration;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.Projects;

internal sealed class ProjectFilesActivation(
    Func<CancellationToken, ValueTask<ProjectFilesOwnedWorkspace>> open,
    string summary,
    EventCallback closed) {
    public Func<CancellationToken, ValueTask<ProjectFilesOwnedWorkspace>> Open { get; } = open;
    public string Summary { get; } = summary;
    public EventCallback Closed { get; } = closed;
    public FileAccessContext? Context { get; set; }
    public ProjectFilesOwnedWorkspace? Workspace { get; set; }
    public ProjectFilesPilotInteraction? Preview { get; set; }
    public FileBrowserItemKey? PreviewItem { get; set; }
    public ProjectFilesOperation? Operation { get; set; }
    public bool Retired { get; set; }
}

internal sealed class ProjectFilesOperation(bool action = false) : IDisposable {
    private readonly CancellationTokenSource cancellation = new();
    public CancellationToken Token => cancellation.Token;
    public bool IsAction { get; } = action;
    public void Cancel() => cancellation.Cancel();
    public void Dispose() => cancellation.Dispose();
}
