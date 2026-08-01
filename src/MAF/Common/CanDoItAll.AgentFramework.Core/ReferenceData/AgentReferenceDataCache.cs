namespace CanDoItAll.AgentFramework.Core;

public sealed class AgentReferenceDataCache : IAgentReferenceDataCacheInvalidator, IDisposable
{
    private readonly object syncRoot = new();
    private readonly Dictionary<AgentReferenceDataRequest, AgentReferenceDataCacheEntry> entries = [];
    private readonly CancellationTokenSource lifetimeCancellation = new();
    private readonly CancellationToken lifetimeToken;
    private readonly AgentReferenceDataInvalidationHub? sharedInvalidator;
    private bool disposed;

    public AgentReferenceDataCache(AgentReferenceDataInvalidationHub? sharedInvalidator = null)
    {
        lifetimeToken = lifetimeCancellation.Token;
        this.sharedInvalidator = sharedInvalidator;
        if (sharedInvalidator is not null)
        {
            sharedInvalidator.Invalidated += HandleSharedInvalidation;
        }
    }

    public event EventHandler? Invalidated;

    public async Task<AgentReferenceDataSnapshot> GetOrCreateAsync(
        AgentReferenceDataRequest request,
        DateTimeOffset now,
        TimeSpan ttl,
        Func<CancellationToken, Task<AgentReferenceDataSnapshot>> factory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(factory);

        AgentReferenceDataCacheEntry entry;
        lock (syncRoot)
        {
            ObjectDisposedException.ThrowIf(disposed, this);

            if (entries.TryGetValue(request, out var existingEntry) && existingEntry.ExpiresAtUtc > now)
            {
                entry = existingEntry;
            }
            else
            {
                entry = new AgentReferenceDataCacheEntry(
                    this,
                    request,
                    factory,
                    now.Add(ttl));
                entries[request] = entry;
            }
        }

        return await entry.Value.Value
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public void Invalidate()
    {
        Clear();
    }

    public void Dispose()
    {
        lock (syncRoot)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            entries.Clear();
            Invalidated = null;
        }

        if (sharedInvalidator is not null)
        {
            sharedInvalidator.Invalidated -= HandleSharedInvalidation;
        }

        lifetimeCancellation.Cancel();
        lifetimeCancellation.Dispose();
    }

    private void HandleSharedInvalidation(object? sender, EventArgs eventArgs)
    {
        Clear();
    }

    private void Clear()
    {
        lock (syncRoot)
        {
            if (disposed)
            {
                return;
            }

            entries.Clear();
        }

        EventHandlerNotification.NotifyAll(Invalidated, this);
    }

    private async Task<AgentReferenceDataSnapshot> RunFactoryAsync(
        AgentReferenceDataRequest request,
        AgentReferenceDataCacheEntry entry,
        Func<CancellationToken, Task<AgentReferenceDataSnapshot>> factory)
    {
        try
        {
            lifetimeToken.ThrowIfCancellationRequested();
            return await factory(lifetimeToken).ConfigureAwait(false);
        }
        catch
        {
            Remove(request, entry);
            throw;
        }
    }

    private void Remove(
        AgentReferenceDataRequest request,
        AgentReferenceDataCacheEntry entry)
    {
        lock (syncRoot)
        {
            if (entries.TryGetValue(request, out var existingEntry) &&
                ReferenceEquals(existingEntry, entry))
            {
                entries.Remove(request);
            }
        }
    }

    private sealed class AgentReferenceDataCacheEntry
    {
        public AgentReferenceDataCacheEntry(
            AgentReferenceDataCache owner,
            AgentReferenceDataRequest request,
            Func<CancellationToken, Task<AgentReferenceDataSnapshot>> factory,
            DateTimeOffset expiresAtUtc)
        {
            Value = new Lazy<Task<AgentReferenceDataSnapshot>>(
                () => owner.RunFactoryAsync(request, this, factory),
                LazyThreadSafetyMode.ExecutionAndPublication);
            ExpiresAtUtc = expiresAtUtc;
        }

        public Lazy<Task<AgentReferenceDataSnapshot>> Value { get; }

        public DateTimeOffset ExpiresAtUtc { get; }
    }
}
