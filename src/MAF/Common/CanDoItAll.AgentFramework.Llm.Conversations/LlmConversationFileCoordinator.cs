using System.Collections.Concurrent;

namespace CanDoItAll.AgentFramework.Llm.Conversations;

internal static class LlmConversationFileCoordinator
{
    private static readonly ConcurrentDictionary<string, Entry> Entries = new(
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    internal static async ValueTask<IDisposable> AcquireAsync(
        string path, CancellationToken cancellationToken)
    {
        var canonicalPath = Path.GetFullPath(path);
        while (true)
        {
            var entry = Entries.GetOrAdd(canonicalPath, static _ => new Entry());
            if (!entry.TryAddReference())
            {
                Remove(canonicalPath, entry);
                continue;
            }

            try
            {
                await entry.WaitAsync(cancellationToken).ConfigureAwait(false);
                return new Lease(canonicalPath, entry);
            }
            catch
            {
                ReleaseReference(canonicalPath, entry);
                throw;
            }
        }
    }

    internal static bool IsTracked(string path)
        => Entries.ContainsKey(Path.GetFullPath(path));

    private static void ReleaseReference(string canonicalPath, Entry entry)
    {
        if (!entry.ReleaseReference())
        {
            return;
        }

        Remove(canonicalPath, entry);
        entry.Dispose();
    }

    private static void Remove(string canonicalPath, Entry entry)
        => ((ICollection<KeyValuePair<string, Entry>>)Entries).Remove(
            new KeyValuePair<string, Entry>(canonicalPath, entry));

    private sealed class Lease(string canonicalPath, Entry entry) : IDisposable
    {
        private Entry? _entry = entry;

        public void Dispose()
        {
            var ownedEntry = Interlocked.Exchange(ref _entry, null);
            if (ownedEntry is null)
            {
                return;
            }

            ownedEntry.Release();
            ReleaseReference(canonicalPath, ownedEntry);
        }
    }

    private sealed class Entry : IDisposable
    {
        private readonly Lock _stateLock = new();
        private readonly SemaphoreSlim _gate = new(1, 1);
        private int _referenceCount;
        private bool _retired;

        public bool TryAddReference()
        {
            lock (_stateLock)
            {
                if (_retired)
                {
                    return false;
                }

                _referenceCount++;
                return true;
            }
        }

        public bool ReleaseReference()
        {
            lock (_stateLock)
            {
                _referenceCount--;
                if (_referenceCount != 0)
                {
                    return false;
                }

                _retired = true;
                return true;
            }
        }

        public Task WaitAsync(CancellationToken cancellationToken)
            => _gate.WaitAsync(cancellationToken);

        public void Release()
            => _gate.Release();

        public void Dispose()
            => _gate.Dispose();
    }
}
