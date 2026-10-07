using CanDoItAll.Plugins.UI;

namespace CanDoItAll.Modules.Plugins.Presentation;

public sealed class PluginReadLane<T>(PluginReadState<T> state, Action changed, Action<Exception> failed) : IDisposable {
    private CancellationTokenSource? current;
    private long generation;
    private bool disposed;
    private bool hasValue;

    public void Invalidate(bool clear = false, T empty = default!) {
        generation++;
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
        if (disposed) {
            return false;
        }
        Invalidate();
        var request = generation;
        using var source = new CancellationTokenSource();
        current = source;
        state.Status = PluginReadStatus.Loading;
        changed();
        try {
            var value = await read(source.Token);
            if (disposed || request != generation) {
                return false;
            }
            accept?.Invoke(value);
            state.Value = value;
            hasValue = true;
            state.Status = PluginReadStatus.Ready;
            return true;
        } catch (OperationCanceledException) when (source.IsCancellationRequested) {
            return false;
        } catch (Exception exception) {
            if (!disposed && request == generation) {
                state.Status = hasValue ? PluginReadStatus.Stale : PluginReadStatus.Unavailable;
                failed(exception);
            }
            return false;
        } finally {
            if (!disposed && request == generation) {
                current = null;
                changed();
            }
        }
    }

    public void Dispose() {
        disposed = true;
        Invalidate();
    }
}
