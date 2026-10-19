using System.Text.Json;

namespace CanDoItAll.Infrastructure.ControlPlane;

public sealed class DatasetActivationRestorationLease : IAsyncDisposable {
    private readonly Func<CancellationToken, Task<DatasetActivationFileObservation>> observeExisting;
    private readonly Action invalidateCachedBinding;
    private readonly string root;
    private readonly string path;
    private readonly DurableFileWriter files;
    private readonly IAsyncDisposable coordination;
    private DatasetActivationFileObservation observed;
    private int disposed;

    internal DatasetActivationRestorationLease(Func<CancellationToken, Task<DatasetActivationFileObservation>> observeExisting,
        Action invalidateCachedBinding, string root, string path,
        DurableFileWriter files, IAsyncDisposable coordination, DatasetActivationFileObservation observed,
        DatasetActivationFileRecord selected) {
        this.observeExisting = observeExisting;
        this.invalidateCachedBinding = invalidateCachedBinding;
        this.root = root;
        this.path = path;
        this.files = files;
        this.coordination = coordination;
        this.observed = observed;
        Selection = selected;
    }

    public DatasetActivationFileRecord Selection { get; }

    internal Task PublishPreparedAsync(CancellationToken cancellationToken)
        => PublishAsync(Selection, cancellationToken);

    internal Task PublishActiveAsync(CancellationToken cancellationToken)
        => PublishAsync(new(DatasetActivationFileRecord.CurrentVersion, Selection.ProfileId,
            DatasetActivationFileState.Active, Selection.Plan, Selection.Restoration), cancellationToken);

    private async Task PublishAsync(DatasetActivationFileRecord next, CancellationToken cancellationToken) {
        await RequireUnchangedAsync(cancellationToken);
        if (observed.Record == next) {
            return;
        }
        var bytes = JsonSerializer.SerializeToUtf8Bytes(next, DatasetActivationRecordCodec.Options);
        if (bytes.Length > DatasetActivationService.MaximumRecordBytes) {
            throw new InvalidDataException("The restoration record exceeds its supported bound.");
        }
        invalidateCachedBinding();
        await files.WriteBytesAsync(root, path, bytes, new() {
            RequirePrivateUnixMode = true,
            RequireExistingPrivateTarget = true,
            CommitMode = DurableFileCommitMode.ReplaceExisting,
            LockTimeout = TimeSpan.FromSeconds(5)
        }, cancellationToken, beforeCommit: token => new(RequireUnchangedAsync(token)));
        var published = await observeExisting(cancellationToken);
        if (published.Record != next) {
            throw new InvalidOperationException("The acknowledged restoration publication no longer matches its exact record.");
        }
        observed = published;
    }

    private async Task RequireUnchangedAsync(CancellationToken cancellationToken) {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        if (await observeExisting(cancellationToken) != observed) {
            throw new InvalidOperationException("The existing activation file changed during restoration.");
        }
    }

    public async ValueTask DisposeAsync() {
        if (Interlocked.Exchange(ref disposed, 1) == 0) {
            await coordination.DisposeAsync();
        }
    }
}
