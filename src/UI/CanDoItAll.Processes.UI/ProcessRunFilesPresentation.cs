using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.FileBrowser.Components;
using CanDoItAll.FileTools.FileInteraction;

namespace CanDoItAll.Processes.UI;

public sealed record ProcessRunFilesPresentation(
    Guid RunId,
    IFileBrowserSession? Browser,
    int SourceCount,
    string Revision,
    FileInteractionRequest? Request,
    IFileContentSource? ContentSource,
    IFileBrowserHostActionCatalog? HostActionCatalog,
    bool IsLoading,
    string? OpenError,
    string? ActivationError,
    string? ActionFeedback);
