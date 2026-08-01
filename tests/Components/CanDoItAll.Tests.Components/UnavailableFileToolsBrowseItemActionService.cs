using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.Integration;

namespace CanDoItAll.Tests.Components;

internal sealed class UnavailableFileToolsBrowseItemActionService : IFileToolsBrowseItemActionService
{
    public bool IsLocalLaunchAvailable => false;

    public ValueTask<FileToolsBrowseItemActionResult> LaunchAsync(
        FileToolsSemanticScope scope,
        FileBrowserItemKey itemKey,
        FileToolsLocalFileAction action,
        CancellationToken cancellationToken = default)
        => ValueTask.FromException<FileToolsBrowseItemActionResult>(
            new InvalidOperationException("Local file actions are not expected in this test."));

    public ValueTask<IFileToolsDownloadLease> AuthorizeDownloadAsync(
        FileToolsSemanticScope scope,
        FileBrowserItemKey itemKey,
        CancellationToken cancellationToken = default)
        => ValueTask.FromException<IFileToolsDownloadLease>(
            new InvalidOperationException("File downloads are not expected in this test."));
}
