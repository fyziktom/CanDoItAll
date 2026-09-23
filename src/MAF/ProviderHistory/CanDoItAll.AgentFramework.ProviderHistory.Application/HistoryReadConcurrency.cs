namespace CanDoItAll.AgentFramework.ProviderHistory;

public sealed class HistoryReadConcurrency : IDisposable {
    private readonly SemaphoreSlim permits = new(4, 4);

    public IDisposable Enter() {
        if (!permits.Wait(0)) {
            throw new ProviderHistoryException(HistoryFailure.Unavailable, "History is handling other reads. Retry this request shortly.");
        }
        return new Lease(permits);
    }

    public void Dispose() => permits.Dispose();

    private sealed class Lease(SemaphoreSlim permits) : IDisposable {
        private int released;
        public void Dispose() {
            if (Interlocked.Exchange(ref released, 1) == 0) {
                permits.Release();
            }
        }
    }
}
