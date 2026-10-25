namespace CanDoItAll.AgentFramework.Persistence;

internal enum FileSandboxWorkspaceAccessPriority { Ordinary, Control }

internal sealed class FileSandboxWorkspaceCoordinationGate(string lockPath) {
    private const int MaximumControlBurst = 8;
    private static readonly Lock PoolLock = new();
    private static readonly Dictionary<string, Entry> Entries = new(StringComparer.Ordinal);
    private readonly string key = Path.GetFullPath(lockPath);

    internal ValueTask<IDisposable> AcquireAsync(FileSandboxWorkspaceAccessPriority priority, CancellationToken cancellationToken) {
        if (!Enum.IsDefined(priority)) {
            throw new ArgumentOutOfRangeException(nameof(priority));
        }
        cancellationToken.ThrowIfCancellationRequested();
        Waiter waiter;
        lock (PoolLock) {
            if (!Entries.TryGetValue(key, out var entry)) {
                entry = new(key) { ConsecutiveControl = priority == FileSandboxWorkspaceAccessPriority.Control ? 1 : 0 };
                Entries.Add(key, entry);
                return ValueTask.FromResult<IDisposable>(new Lease(entry));
            }
            waiter = new(cancellationToken);
            waiter.Node = (priority == FileSandboxWorkspaceAccessPriority.Control ? entry.Control : entry.Ordinary).AddLast(waiter);
        }
        return new(AwaitAsync(waiter));
    }

    private static async Task<IDisposable> AwaitAsync(Waiter waiter) {
        using var registration = waiter.Token.UnsafeRegister(static state => Cancel((Waiter)state!), waiter);
        return await waiter.Completion.Task.ConfigureAwait(false);
    }

    private static void Cancel(Waiter waiter) {
        lock (PoolLock) {
            if (waiter.Node is not { } node || node.List is not { } queue) {
                return;
            }
            queue.Remove(node);
            waiter.Node = null;
            waiter.Completion.TrySetCanceled(waiter.Token);
        }
    }

    private static void Release(Entry entry) {
        lock (PoolLock) {
            if (!Entries.TryGetValue(entry.Key, out var current) || !ReferenceEquals(current, entry)) {
                throw new InvalidOperationException("Workspace admission released a different active queue.");
            }
            while (entry.Control.Count != 0 || entry.Ordinary.Count != 0) {
                var control = entry.Control.Count != 0 && (entry.ConsecutiveControl < MaximumControlBurst || entry.Ordinary.Count == 0);
                var queue = control ? entry.Control : entry.Ordinary;
                var waiter = queue.First!.Value;
                queue.RemoveFirst();
                waiter.Node = null;
                if (waiter.Token.IsCancellationRequested) {
                    waiter.Completion.TrySetCanceled(waiter.Token);
                    continue;
                }
                entry.ConsecutiveControl = control ? Math.Min(MaximumControlBurst, entry.ConsecutiveControl + 1) : 0;
                waiter.Completion.SetResult(new Lease(entry));
                return;
            }
            Entries.Remove(entry.Key);
        }
    }

    private sealed class Entry(string key) {
        internal string Key { get; } = key;
        internal int ConsecutiveControl { get; set; }
        internal LinkedList<Waiter> Ordinary { get; } = new();
        internal LinkedList<Waiter> Control { get; } = new();
    }

    private sealed class Waiter(CancellationToken token) {
        internal CancellationToken Token { get; } = token;
        internal TaskCompletionSource<IDisposable> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal LinkedListNode<Waiter>? Node { get; set; }
    }

    private sealed class Lease(Entry owner) : IDisposable {
        private Entry? held = owner;

        public void Dispose() {
            if (Interlocked.Exchange(ref held, null) is { } released) {
                Release(released);
            }
        }
    }
}
