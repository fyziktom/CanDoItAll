using System.Collections.Concurrent;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessDispatchGuardLease : IDisposable
{
    private readonly Guid stepRunId;
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> dispatchGuards;
    private readonly SemaphoreSlim dispatchGuard;
    private int released;

    private ProcessDispatchGuardLease(
        Guid stepRunId,
        ConcurrentDictionary<Guid, SemaphoreSlim> dispatchGuards,
        SemaphoreSlim dispatchGuard)
    {
        this.stepRunId = stepRunId;
        this.dispatchGuards = dispatchGuards;
        this.dispatchGuard = dispatchGuard;
    }

    public static async Task<ProcessDispatchGuardLease> WaitAsync(
        Guid stepRunId,
        ConcurrentDictionary<Guid, SemaphoreSlim> dispatchGuards,
        CancellationToken cancellationToken)
    {
        if (stepRunId == Guid.Empty)
        {
            throw new ArgumentException("Step run id is required.", nameof(stepRunId));
        }

        ArgumentNullException.ThrowIfNull(dispatchGuards);

        var dispatchGuard = dispatchGuards.GetOrAdd(stepRunId, static _ => new SemaphoreSlim(1, 1));
        await dispatchGuard.WaitAsync(cancellationToken);
        return new ProcessDispatchGuardLease(stepRunId, dispatchGuards, dispatchGuard);
    }

    public void Release()
    {
        if (Interlocked.Exchange(ref released, 1) == 1)
        {
            return;
        }

        dispatchGuard.Release();
    }

    public void Dispose()
    {
        Release();
        TryRemoveReleasedDispatchGuard();
    }

    private void TryRemoveReleasedDispatchGuard()
    {
        if (dispatchGuard.CurrentCount != 1)
        {
            return;
        }

        ((ICollection<KeyValuePair<Guid, SemaphoreSlim>>)dispatchGuards)
            .Remove(new KeyValuePair<Guid, SemaphoreSlim>(stepRunId, dispatchGuard));
    }
}
