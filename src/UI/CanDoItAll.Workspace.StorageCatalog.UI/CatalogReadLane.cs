namespace CanDoItAll.Workspace.StorageCatalog.UI;

public sealed class CatalogReadLane<T>(T initial, Func<bool> current, Action changed) : IDisposable {
    private CancellationTokenSource? reading;
    private long generation;
    private bool disposed;
    public T Value { get; private set; } = initial;
    public bool IsLoading { get; private set; }
    public bool IsAvailable { get; private set; }
    public string Error { get; private set; } = string.Empty;

    public async Task ReadAsync(Func<CancellationToken, Task<T>> read, Action<T>? publish = null) {
        if (disposed || !current()) {
            return;
        }
        reading?.Cancel();
        using var cancellation = new CancellationTokenSource();
        reading = cancellation;
        var attempt = ++generation;
        IsLoading = true;
        Error = string.Empty;
        changed();
        bool CanPublish() => !disposed && current() && attempt == generation;
        try {
            var value = await read(cancellation.Token);
            if (CanPublish()) {
                Value = value;
                IsAvailable = true;
                publish?.Invoke(value);
            }
        } catch (OperationCanceledException) when (cancellation.IsCancellationRequested) {
        } catch (Exception) {
            if (CanPublish()) {
                IsAvailable = false;
                Error = "Read unavailable. Previously shown values may be stale; retry explicitly.";
            }
        } finally {
            if (ReferenceEquals(reading, cancellation)) {
                reading = null;
                IsLoading = false;
            }
            if (CanPublish()) {
                changed();
            }
        }
    }

    public void Invalidate() {
        generation++;
        reading?.Cancel();
        reading = null;
        IsLoading = false;
        IsAvailable = false;
        Error = string.Empty;
    }

    public void Dispose() {
        if (disposed) {
            return;
        }
        disposed = true;
        Invalidate();
    }
}
