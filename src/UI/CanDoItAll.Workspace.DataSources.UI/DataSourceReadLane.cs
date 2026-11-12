namespace CanDoItAll.Workspace.DataSources.UI;

internal sealed class DataSourceReadLane : IDisposable {
    private Read? current;
    private bool disposed;
    public Read Begin() {
        ObjectDisposedException.ThrowIf(disposed, this);
        current?.Cancel();
        current = new(this);
        return current;
    }
    public void Dispose() {
        disposed = true;
        current?.Cancel();
        current = null;
    }
    public sealed class Read(DataSourceReadLane lane) : IDisposable {
        private readonly CancellationTokenSource cancellation = new();
        public CancellationToken Token => cancellation.Token;
        public bool IsCurrent => !lane.disposed && ReferenceEquals(lane.current, this);
        public void Cancel() => cancellation.Cancel();
        public void Dispose() {
            if (ReferenceEquals(lane.current, this)) {
                lane.current = null;
            }
            cancellation.Dispose();
        }
    }
}
