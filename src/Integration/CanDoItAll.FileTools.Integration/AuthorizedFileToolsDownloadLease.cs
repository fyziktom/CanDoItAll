using CanDoItAll.FileTools.FileInteraction;

namespace CanDoItAll.FileTools.Integration;

internal sealed class AuthorizedFileToolsDownloadLease(
    FileReference file,
    string fileName,
    IFileContentSource contentSource,
    IStorageFileAccessAuthorizationCoordinator authorizationCoordinator) : IFileToolsDownloadLease
{
    private int disposed;

    public string FileName { get; } = !string.IsNullOrWhiteSpace(fileName)
        ? fileName.Trim()
        : throw new ArgumentException("A download file name is required.", nameof(fileName));

    public ValueTask<FileContentLease> OpenReadAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        return contentSource.OpenReadAsync(new FileContentReadRequest(file), cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0)
        {
            await authorizationCoordinator.RevokeAsync(file, CancellationToken.None);
        }
    }
}
