using CanDoItAll.Plugins.UI;

namespace CanDoItAll.Modules.Plugins.Presentation;

public sealed class PluginReadLane<T>(PluginReadState<T> state, Action changed, Action<Exception> failed) : IDisposable {
    private readonly Lock gate = new();
    private PendingRead? current;
    private bool disposed;
    private bool hasValue;

    public void Invalidate(bool clear = false, T empty = default!) {
        lock (gate) {
            InvalidateCurrent(clear, empty);
        }
    }

    private void InvalidateCurrent(bool clear, T empty) {
        current?.Cancel();
        current = null;
        if (clear) {
            state.Value = empty;
            state.Status = PluginReadStatus.Initial;
            hasValue = false;
        } else if (state.Status == PluginReadStatus.Loading) {
            state.Status = hasValue ? PluginReadStatus.Stale : PluginReadStatus.Initial;
        }
    }

    public async Task<bool> LoadAsync(Func<CancellationToken, Task<T>> read, Action<T>? accept = null) {
        await using var request = new PendingRead();
        lock (gate) {
            if (disposed) {
                return false;
            }
            current?.Cancel();
            current = request;
            state.Status = PluginReadStatus.Loading;
        }
        try {
            changed();
            var value = await read(request.Token);
            lock (gate) {
                if (current != request) {
                    return false;
                }
                accept?.Invoke(value);
                // Accepting a selection can invalidate the read that supplied it.
                if (current != request) {
                    return false;
                }
                state.Value = value;
                hasValue = true;
                state.Status = PluginReadStatus.Ready;
            }
            return true;
        } catch (OperationCanceledException) when (request.Token.IsCancellationRequested) {
            return false;
        } catch (Exception exception) {
            lock (gate) {
                if (current != request) {
                    return false;
                }
                state.Status = hasValue ? PluginReadStatus.Stale : PluginReadStatus.Unavailable;
            }
            failed(exception);
            return false;
        } finally {
            bool notify;
            lock (gate) {
                notify = current == request;
                if (notify) {
                    current = null;
                }
            }
            if (notify) {
                changed();
            }
        }
    }

    public void Dispose() {
        lock (gate) {
            disposed = true;
            InvalidateCurrent(clear: false, default!);
        }
    }

    private sealed class PendingRead : IAsyncDisposable {
        private readonly CancellationTokenSource source = new();
        private Task callbacks = Task.CompletedTask;
        public CancellationToken Token => source.Token;
        public void Cancel() => callbacks = source.CancelAsync();

        public async ValueTask DisposeAsync() {
            try {
                await callbacks;
            } finally {
                source.Dispose();
            }
        }
    }
}
