using System.Collections.Concurrent;

namespace CanDoItAll.Processes.Persistence;

internal static class ProcessRuntimeRootSequenceLocks
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> Locks = new();

    public static async Task<IDisposable> AcquireAsync(
        IEnumerable<Guid> rootRunIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rootRunIds);

        var distinctRootRunIds = rootRunIds
            .Distinct()
            .OrderBy(id => id)
            .ToArray();
        if (distinctRootRunIds.Length == 0)
        {
            return NoopReleaser.Instance;
        }

        var acquired = new List<SemaphoreSlim>(distinctRootRunIds.Length);
        try
        {
            foreach (var rootRunId in distinctRootRunIds)
            {
                var semaphore = Locks.GetOrAdd(rootRunId, _ => new SemaphoreSlim(1, 1));
                await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                acquired.Add(semaphore);
            }

            return new RootSequenceLockReleaser(acquired);
        }
        catch
        {
            Release(acquired);
            throw;
        }
    }

    private static void Release(IReadOnlyList<SemaphoreSlim> semaphores)
    {
        for (var index = semaphores.Count - 1; index >= 0; index--)
        {
            semaphores[index].Release();
        }
    }

    private sealed class RootSequenceLockReleaser(IReadOnlyList<SemaphoreSlim> semaphores) : IDisposable
    {
        private bool disposed;

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            Release(semaphores);
        }
    }

    private sealed class NoopReleaser : IDisposable
    {
        public static NoopReleaser Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}
