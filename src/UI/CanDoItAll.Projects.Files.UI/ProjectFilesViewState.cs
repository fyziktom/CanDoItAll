using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.FileBrowser.Components;
using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.FileInteraction.Components;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Projects.Files.UI;

public sealed record ProjectFilesViewState {
    public bool IsLoading { get; init; }
    public bool IsEmpty { get; init; }
    public string SourceSummary { get; init; } = string.Empty;
    public string? OpenError { get; init; }
    public string? ActivationError { get; init; }
    public string? ActionFeedback { get; init; }
    public FileBrowserSnapshot? Snapshot { get; init; }
    public ProjectFilesBrowseView? Browse { get; init; }
    public ProjectFilesPreviewView? Preview { get; init; }
    public EventCallback Retry { get; init; }
    public EventCallback Refresh { get; init; }
    public EventCallback Back { get; init; }
    public EventCallback Closed { get; init; }
}

public sealed record ProjectFilesBrowseView(
    IFileBrowserSession Session,
    IFileBrowserHostActionCatalog Actions,
    EventCallback<FileBrowserSnapshot> SnapshotChanged,
    EventCallback<FileBrowserItemInvokedEventArgs> ItemInvoked,
    EventCallback<FileBrowserItemActionEventArgs> ActionRequested);

public sealed record ProjectFilesPreviewView(
    FileInteractionRequest Request,
    IFileContentSource ContentSource,
    FileInteractionComponentComposition Composition,
    EventCallback<FileInteractionRequest> OpenExternally);
