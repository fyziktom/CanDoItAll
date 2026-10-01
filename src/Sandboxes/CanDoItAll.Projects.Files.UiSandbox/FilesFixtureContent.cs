using CanDoItAll.FileTools.FileInteraction;

namespace CanDoItAll.Projects.Files.UiSandbox;

public sealed class FilesFixtureContent(FilesFixture fixture) : IFileContentSource, IAsyncDisposable {
    private bool disposed;
    public FileReference File { get; } = new("projects-files-fixture", Guid.NewGuid().ToString("N"));
    public int Releases { get; private set; }
    public FileInteractionRequest Request => new(File, fixture.Name, FileInteractionMode.View, fixture.MediaType, fixture.Bytes.Length);

    public ValueTask<FileContentLease> OpenReadAsync(FileContentReadRequest request, CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(disposed, this);
        if (request.File != File) {
            throw new InvalidOperationException("The fixture content identity does not match.");
        }
        int offset = checked((int)Math.Min(request.Offset, fixture.Bytes.Length));
        int count = checked((int)Math.Min(request.Length ?? fixture.Bytes.Length, fixture.Bytes.Length - offset));
        return ValueTask.FromResult(new FileContentLease(new MemoryStream(fixture.Bytes, offset, count, writable: false), fixture.MediaType, count));
    }

    public ValueTask DisposeAsync() {
        if (!disposed) {
            disposed = true;
            Releases++;
        }
        return ValueTask.CompletedTask;
    }
}
