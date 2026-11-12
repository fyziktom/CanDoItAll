using System.Collections.Concurrent;

namespace CanDoItAll.Processes.Persistence;

internal static class ProcessRuntimeRootSequenceLocks {
    private const int RegistryPartitionCount = 64;
    private static readonly Lock[] RegistryLocks = Enumerable.Range(0, RegistryPartitionCount).Select(_ => new Lock()).ToArray();
    private static readonly ConcurrentDictionary<Guid, Entry> Locks = new();

    internal static int RetainedRootCount => Locks.Count;

    public static async Task<IDisposable> AcquireAsync(IEnumerable<Guid> rootRunIds, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(rootRunIds);
        var roots = rootRunIds.Distinct().Order().ToArray();
        if (roots.Length == 0) {
            return NoopReleaser.Instance;
        }
        var acquired = new List<Entry>(roots.Length);
        try {
            foreach (var root in roots) {
                Entry entry;
                lock (RegistryLock(root)) {
                    if (!Locks.TryGetValue(root, out entry!)) {
                        entry = new(root);
                        Locks[root] = entry;
                    }
                    entry.References++;
                }
                try {
                    await entry.Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                } catch {
                    ReleaseReference(entry);
                    throw;
                }
                acquired.Add(entry);
            }
            return new RootSequenceLockReleaser(acquired);
        } catch {
            Release(acquired);
            throw;
        }
    }

    private static void Release(IReadOnlyList<Entry> entries) {
        for (var index = entries.Count - 1; index >= 0; index--) {
            var entry = entries[index];
            entry.Semaphore.Release();
            ReleaseReference(entry);
        }
    }

    private static void ReleaseReference(Entry entry) {
        lock (RegistryLock(entry.RootRunId)) {
            if (--entry.References == 0) {
                Locks.TryRemove(entry.RootRunId, out _);
                entry.Semaphore.Dispose();
            }
        }
    }

    private static Lock RegistryLock(Guid rootRunId)
        => RegistryLocks[unchecked((uint)rootRunId.GetHashCode()) % RegistryPartitionCount];

    private sealed class Entry(Guid rootRunId) {
        internal Guid RootRunId { get; } = rootRunId;
        internal SemaphoreSlim Semaphore { get; } = new(1, 1);
        internal int References { get; set; }
    }

    private sealed class RootSequenceLockReleaser(IReadOnlyList<Entry> entries) : IDisposable {
        private IReadOnlyList<Entry>? held = entries;
        public void Dispose() {
            if (Interlocked.Exchange(ref held, null) is { } original) {
                Release(original);
            }
        }
    }

    private sealed class NoopReleaser : IDisposable {
        public static NoopReleaser Instance { get; } = new();
        public void Dispose() { }
    }
}
