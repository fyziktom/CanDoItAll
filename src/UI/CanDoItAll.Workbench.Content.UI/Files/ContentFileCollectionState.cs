using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.FileBrowser.Components;
using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.FileInteraction.Components;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Workbench.Content.UI;

public sealed record ContentFileCollectionState {
    public required Guid OpeningId { get; init; }
    public required string Title { get; init; }
    public required string Summary { get; init; }
    public required string SourceStatus { get; init; }
    public bool IsProjectCollection { get; init; }
    public bool IncludeSubprojects { get; init; }
    public bool IsLoading { get; init; }
    public string? OpenError { get; init; }
    public string? ActivationError { get; init; }
    public string? ActionFeedback { get; init; }
    public ContentFileBrowseView? Browse { get; init; }
    public ContentFilePreviewView? Preview { get; init; }
    public EventCallback Retry { get; init; }
    public EventCallback Back { get; init; }
    public EventCallback<bool> IncludeSubprojectsChanged { get; init; }
    public EventCallback OpenRoot { get; init; }
}

public sealed record ContentFileBrowseView(
    IFileBrowserSession Session,
    IFileBrowserHostActionCatalog Actions,
    EventCallback<FileBrowserSnapshot> SnapshotChanged,
    EventCallback<FileBrowserItemInvokedEventArgs> ItemInvoked,
    EventCallback<FileBrowserItemActionEventArgs> ActionRequested);

public sealed record ContentFilePreviewView(
    FileInteractionRequest Request,
    IFileContentSource ContentSource,
    FileInteractionComponentComposition Composition,
    int MaximumContentBytes,
    EventCallback<FileInteractionRequest> OpenExternally);
