namespace CanDoItAll.AgentFramework.Core;

public sealed class AgentReferenceDataCache : IAgentReferenceDataCacheInvalidator
{
    private readonly object syncRoot = new();
    private readonly Dictionary<AgentReferenceDataRequest, AgentReferenceDataCacheEntry> entries = [];

    public event EventHandler? Invalidated;

    public async Task<AgentReferenceDataSnapshot> GetOrCreateAsync(
        AgentReferenceDataRequest request,
        DateTimeOffset now,
        TimeSpan ttl,
        Func<Task<AgentReferenceDataSnapshot>> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        Lazy<Task<AgentReferenceDataSnapshot>> lazy;
        lock (syncRoot)
        {
            if (entries.TryGetValue(request, out var existingEntry) && existingEntry.ExpiresAtUtc > now)
            {
                lazy = existingEntry.Value;
            }
            else
            {
                lazy = new Lazy<Task<AgentReferenceDataSnapshot>>(
                    factory,
                    LazyThreadSafetyMode.ExecutionAndPublication);
                entries[request] = new AgentReferenceDataCacheEntry(lazy, now.Add(ttl));
            }
        }

        try
        {
            return await lazy.Value.ConfigureAwait(false);
        }
        catch
        {
            Remove(request, lazy);
            throw;
        }
    }

    public void Invalidate()
    {
        lock (syncRoot)
        {
            entries.Clear();
        }

        Invalidated?.Invoke(this, EventArgs.Empty);
    }

    private void Remove(
        AgentReferenceDataRequest request,
        Lazy<Task<AgentReferenceDataSnapshot>> value)
    {
        lock (syncRoot)
        {
            if (entries.TryGetValue(request, out var existingEntry) && ReferenceEquals(existingEntry.Value, value))
            {
                entries.Remove(request);
            }
        }
    }

    private sealed record AgentReferenceDataCacheEntry(
        Lazy<Task<AgentReferenceDataSnapshot>> Value,
        DateTimeOffset ExpiresAtUtc);
}
