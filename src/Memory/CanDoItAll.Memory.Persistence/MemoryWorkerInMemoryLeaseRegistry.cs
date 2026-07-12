using CanDoItAll.Memory.Persistence.Hosting;

namespace CanDoItAll.Memory.Persistence;

internal sealed class MemoryWorkerInMemoryLeaseRegistry
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<MemoryBackgroundWorkerPhase, MemoryWorkerLease> leases = [];

    public async Task<T> ExecuteAsync<T>(
        Func<IDictionary<MemoryBackgroundWorkerPhase, MemoryWorkerLease>, Task<T>> execute,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(execute);
        await gate.WaitAsync(cancellationToken);
        try
        {
            return await execute(leases);
        }
        finally
        {
            gate.Release();
        }
    }
}
