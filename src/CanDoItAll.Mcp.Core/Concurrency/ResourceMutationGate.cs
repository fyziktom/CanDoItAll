using System.Collections.Concurrent;
using CanDoItAll.Mcp.Core.Contracts;

namespace CanDoItAll.Mcp.Core.Concurrency;

public sealed class ResourceMutationGate
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _semaphores = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _holders = new(StringComparer.OrdinalIgnoreCase);

    public async Task<MutationLease> AcquireAsync(
        string resourceKey,
        string reason,
        CancellationToken cancellationToken,
        string busyCode = "OperationInProgress")
    {
        var normalizedResourceKey = Normalize(resourceKey);
        var semaphore = _semaphores.GetOrAdd(normalizedResourceKey, static _ => new SemaphoreSlim(1, 1));
        if (!await semaphore.WaitAsync(0, cancellationToken))
        {
            var currentHolder = GetCurrentHolder(normalizedResourceKey) ?? "unknown";
            throw new ToolInvocationException(
                busyCode,
                $"Resource '{normalizedResourceKey}' is busy with '{currentHolder}'. Wait for it to finish or inspect its status/logs before starting another mutating tool.",
                new
                {
                    resource = normalizedResourceKey,
                    currentHolder
                });
        }

        _holders[normalizedResourceKey] = reason;
        return new MutationLease(this, normalizedResourceKey, semaphore);
    }

    public string? GetCurrentHolder(string resourceKey)
    {
        var normalizedResourceKey = Normalize(resourceKey);
        return _holders.TryGetValue(normalizedResourceKey, out var holder) ? holder : null;
    }

    private void Release(string resourceKey, SemaphoreSlim semaphore)
    {
        _holders.TryRemove(resourceKey, out _);
        semaphore.Release();
    }

    private static string Normalize(string resourceKey)
    {
        return string.IsNullOrWhiteSpace(resourceKey) ? "__global__" : resourceKey.Trim();
    }

    public sealed class MutationLease : IAsyncDisposable
    {
        private readonly ResourceMutationGate _owner;
        private readonly string _resourceKey;
        private readonly SemaphoreSlim _semaphore;
        private int _disposed;

        internal MutationLease(ResourceMutationGate owner, string resourceKey, SemaphoreSlim semaphore)
        {
            _owner = owner;
            _resourceKey = resourceKey;
            _semaphore = semaphore;
        }

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return ValueTask.CompletedTask;
            }

            _owner.Release(_resourceKey, _semaphore);
            return ValueTask.CompletedTask;
        }
    }
}
