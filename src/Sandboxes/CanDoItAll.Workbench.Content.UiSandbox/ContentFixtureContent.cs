using System.Security.Cryptography;
using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.FileInteraction.Components;

namespace CanDoItAll.Workbench.Content.UiSandbox;

public sealed class ContentFixtureContent(ContentFixture fixture) : IFileContentSource, IAsyncDisposable {
    private byte[] bytes = fixture.Bytes.ToArray();
    private bool retired;
    public FileReference File { get; } = new("content-scenario", Guid.NewGuid().ToString("N"));
    public FileContentRevision Revision => new(Convert.ToHexString(SHA256.HashData(bytes)));
    public int Saves { get; private set; }
    public int Releases { get; private set; }
    public bool ReadOnly { get; set; }
    public bool FailRead { get; set; }
    public bool LoseSaveAcknowledgement { get; set; }
    public ContentFixtureGate? SaveGate { get; set; }
    public FileInteractionRequest Request(FileInteractionMode mode = FileInteractionMode.View)
        => new(File, fixture.Name, mode, fixture.MediaType, bytes.Length, Revision);

    public ValueTask<FileContentLease> OpenReadAsync(FileContentReadRequest request, CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(retired, this);
        if (request.File != File || FailRead) {
            throw new InvalidOperationException("This scenario content source is unavailable.");
        }
        int offset = checked((int)Math.Min(request.Offset, bytes.Length));
        int count = checked((int)Math.Min(request.Length ?? bytes.Length, bytes.Length - offset));
        return ValueTask.FromResult(new FileContentLease(new MemoryStream(bytes, offset, count, writable: false), fixture.MediaType, count, Revision));
    }

    public async Task SaveAsync(FileInteractionSaveRequestedEventArgs args) {
        if (SaveGate is { } gate) {
            SaveGate = null;
            await gate.WaitAsync();
        }
        ObjectDisposedException.ThrowIf(retired, this);
        if (ReadOnly || args.Request.File != File) {
            throw new InvalidOperationException("The scenario save target is no longer writable.");
        }
        if (args.Request.ExpectedRevision != Revision) {
            throw new FileSaveConflictException(File, args.Request.ExpectedRevision, Revision);
        }
        await using var input = await args.Request.Content.OpenReadAsync();
        using var output = new MemoryStream();
        await input.CopyToAsync(output);
        bytes = output.ToArray();
        Saves++;
        if (LoseSaveAcknowledgement) {
            throw new InvalidOperationException("Scenario acknowledgement was lost after accepting the bytes. Observe the source before another save.");
        }
        args.SetPersistedRevision(Revision);
    }

    public void ChangeRevision() => bytes = System.Text.Encoding.UTF8.GetBytes("Concurrent scenario edit.");

    public ValueTask DisposeAsync() {
        if (!retired) {
            retired = true;
            Releases++;
        }
        return ValueTask.CompletedTask;
    }
}
