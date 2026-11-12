namespace CanDoItAll.Modules.SchedulerPlanner.Presentation;

internal sealed class SchedulerReadLane : IDisposable {
    private CancellationTokenSource? current;
    private long generation;
    public Lease Begin() {
        current?.Cancel();
        current = new();
        return new(this, ++generation, current);
    }
    public void Dispose() {
        generation++;
        current?.Cancel();
        current = null;
    }
    internal sealed class Lease(SchedulerReadLane owner, long generation, CancellationTokenSource source) : IDisposable {
        public CancellationToken Token => source.Token;
        public bool IsCurrent => owner.generation == generation;
        public void Dispose() {
            if (ReferenceEquals(owner.current, source)) {
                owner.current = null;
            }
            source.Dispose();
        }
    }
}
