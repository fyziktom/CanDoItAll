using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.Integration;
using Microsoft.JSInterop;

namespace CanDoItAll.AppComponents.FileTools;

public enum FileToolsHostAction
{
    OpenInPreferredApplication,
    OpenContainingFolder,
    Download
}

public delegate ValueTask<FileToolsBrowseItemActionResult> FileToolsLocalLaunchHandler(
    FileToolsLocalFileAction action,
    CancellationToken cancellationToken);

public delegate ValueTask<IFileToolsDownloadLease> FileToolsDownloadAuthorizationHandler(
    CancellationToken cancellationToken);

public sealed class FileToolsHostActionRunner(IJSRuntime jsRuntime) : IAsyncDisposable
{
    private const string ModulePath = "./_content/CanDoItAll.AppComponents/js/file-tools-host-actions.js";
    private readonly IJSRuntime runtime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
    private int disposed;

    public async ValueTask<FileToolsBrowseItemActionResult> ExecuteAsync(
        FileToolsHostAction action,
        FileToolsLocalLaunchHandler launch,
        FileToolsDownloadAuthorizationHandler authorizeDownload,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(action))
        {
            throw new ArgumentOutOfRangeException(nameof(action));
        }

        ArgumentNullException.ThrowIfNull(launch);
        ArgumentNullException.ThrowIfNull(authorizeDownload);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        return action switch
        {
            FileToolsHostAction.OpenInPreferredApplication => await launch(
                FileToolsLocalFileAction.OpenInPreferredApplication,
                cancellationToken),
            FileToolsHostAction.OpenContainingFolder => await launch(
                FileToolsLocalFileAction.OpenContainingFolder,
                cancellationToken),
            FileToolsHostAction.Download => await DownloadAsync(authorizeDownload, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };
    }

    private async ValueTask<FileToolsBrowseItemActionResult> DownloadAsync(
        FileToolsDownloadAuthorizationHandler authorizeDownload,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authorizeDownload);
        await using IFileToolsDownloadLease download = await authorizeDownload(cancellationToken);
        await using FileContentLease content = await download.OpenReadAsync(cancellationToken);
        using var contentReference = new DotNetStreamReference(content.Stream, leaveOpen: true);
        string fileName = Path.GetFileName(download.FileName.Replace('\\', '/'));
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new InvalidOperationException("The authorized download did not provide a valid file name.");
        }

        await InvokeDownloadAsync(fileName, contentReference, cancellationToken);
        return FileToolsBrowseItemActionResult.Success($"Downloading {fileName}.");
    }

    private async ValueTask InvokeDownloadAsync(
        string fileName,
        DotNetStreamReference contentReference,
        CancellationToken cancellationToken)
    {
        IJSObjectReference module = await runtime.InvokeAsync<IJSObjectReference>(
            "import",
            cancellationToken,
            ModulePath);
        try
        {
            await module.InvokeVoidAsync(
                "downloadFileFromStream",
                cancellationToken,
                fileName,
                contentReference);
        }
        finally
        {
            try
            {
                await module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        Interlocked.Exchange(ref disposed, 1);
        return ValueTask.CompletedTask;
    }
}
