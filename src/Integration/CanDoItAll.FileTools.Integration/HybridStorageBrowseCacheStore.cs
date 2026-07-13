using CanDoItAll.Infrastructure.Storage;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.FileTools.Integration;

internal interface IStorageBrowseCacheStore
{
    ValueTask<StorageBrowsePage> GetOrCreateAsync(
        string key,
        Guid partitionId,
        StorageBrowseCacheSettings settings,
        Func<CancellationToken, ValueTask<StorageBrowsePage>> factory,
        CancellationToken cancellationToken);

    void RecordBypass();
}

internal sealed class HybridStorageBrowseCacheStore(
    HybridCache cache,
    TimeProvider timeProvider,
    StorageBrowseCacheMetrics metrics,
    ILogger<HybridStorageBrowseCacheStore> logger) : IStorageBrowseCacheStore
{
    private readonly object _sync = new();
    private readonly SemaphoreSlim _admissionGate = new(1, 1);
    private readonly Dictionary<string, TrackedEntry> _entries = new(StringComparer.Ordinal);
    private int _retainedItems;
    private int _retainedContinuations;
    private long _retainedBytes;

    public async ValueTask<StorageBrowsePage> GetOrCreateAsync(
        string key,
        Guid partitionId,
        StorageBrowseCacheSettings settings,
        Func<CancellationToken, ValueTask<StorageBrowsePage>> factory,
        CancellationToken cancellationToken)
    {
        bool created = false;
        try
        {
            byte[] cachedPayload = await cache.GetOrCreateAsync(
                key,
                async token =>
                {
                    created = true;
                    metrics.Miss();
                    StorageBrowsePage loaded = await factory(token);
                    byte[] payload = CachedStorageBrowsePage.From(loaded).Serialize();
                    var usage = new EntryUsage(
                        loaded.Entries.Count,
                        loaded.NextCursor is null ? 0 : 1,
                        payload.LongLength);
                    if (loaded.Entries.Count > settings.MaximumItems ||
                        usage.Continuations > settings.MaximumContinuations ||
                        usage.Bytes > settings.MaximumPayloadBytes)
                    {
                        throw new CacheBypassException(loaded);
                    }

                    await _admissionGate.WaitAsync(token);
                    try
                    {
                        IReadOnlyList<string> evictedKeys = Admit(key, partitionId, usage, settings);
                        foreach (string evictedKey in evictedKeys)
                        {
                            await cache.RemoveAsync(evictedKey, CancellationToken.None);
                        }

                        if (evictedKeys.Count > 0)
                        {
                            logger.LogInformation(
                                "Storage browse cache evicted {EvictedCount} bounded entries.",
                                evictedKeys.Count);
                        }
                    }
                    finally
                    {
                        _admissionGate.Release();
                    }

                    return payload;
                },
                new HybridCacheEntryOptions
                {
                    Expiration = settings.MaximumLifetime,
                    LocalCacheExpiration = settings.TimeToLive,
                    Flags = HybridCacheEntryFlags.DisableDistributedCache
                },
                cancellationToken: cancellationToken);
            Touch(key);
            if (!created)
            {
                metrics.Hit();
            }

            return CachedStorageBrowsePage.Deserialize(cachedPayload).ToPage();
        }
        catch (CacheBypassException exception)
        {
            metrics.Bypass();
            return exception.Page;
        }
    }

    public void RecordBypass() => metrics.Bypass();

    private IReadOnlyList<string> Admit(
        string key,
        Guid partitionId,
        EntryUsage usage,
        StorageBrowseCacheSettings settings)
    {
        var evicted = new List<string>();
        lock (_sync)
        {
            RemoveTracked(key);
            DateTimeOffset now = timeProvider.GetUtcNow();
            foreach (string expiredKey in _entries
                         .Where(pair => pair.Value.ExpiresAtUtc <= now)
                         .Select(pair => pair.Key)
                         .ToArray())
            {
                RemoveTracked(expiredKey);
                evicted.Add(expiredKey);
            }

            TrackedEntry[] partitionEntries = _entries.Values
                .Where(entry => entry.PartitionId == partitionId)
                .ToArray();
            int partitionCount = partitionEntries.Length;
            int partitionItems = partitionEntries.Sum(entry => entry.Usage.Items);
            int partitionContinuations = partitionEntries.Sum(entry => entry.Usage.Continuations);
            long partitionBytes = partitionEntries.Sum(entry => entry.Usage.Bytes);
            while (partitionCount >= settings.MaximumEntries ||
                   partitionItems + usage.Items > settings.MaximumItems ||
                   partitionContinuations + usage.Continuations > settings.MaximumContinuations ||
                   partitionBytes + usage.Bytes > settings.MaximumRetainedBytes)
            {
                string? oldestKey = FindOldestKey(partitionId);
                if (oldestKey is null || !_entries.TryGetValue(oldestKey, out TrackedEntry? oldest))
                {
                    break;
                }

                RemoveTracked(oldestKey);
                evicted.Add(oldestKey);
                partitionCount--;
                partitionItems -= oldest.Usage.Items;
                partitionContinuations -= oldest.Usage.Continuations;
                partitionBytes -= oldest.Usage.Bytes;
            }

            while (_entries.Count >= StorageBrowseCacheSettings.AbsoluteMaximumEntries ||
                   _retainedItems + usage.Items > StorageBrowseCacheSettings.AbsoluteMaximumItems ||
                   _retainedContinuations + usage.Continuations >
                   StorageBrowseCacheSettings.AbsoluteMaximumContinuations ||
                   _retainedBytes + usage.Bytes > StorageBrowseCacheSettings.AbsoluteMaximumRetainedBytes)
            {
                string? oldestKey = FindOldestKey(partitionId: null);
                if (oldestKey is null)
                {
                    break;
                }

                RemoveTracked(oldestKey);
                evicted.Add(oldestKey);
            }

            AddTracked(key, new TrackedEntry(
                partitionId,
                usage,
                now,
                now + settings.MaximumLifetime));
            UpdateRetention();
        }

        metrics.Evicted(evicted.Count);
        return evicted;
    }

    private void Touch(string key)
    {
        lock (_sync)
        {
            if (_entries.TryGetValue(key, out TrackedEntry? entry))
            {
                _entries[key] = entry with { LastAccessUtc = timeProvider.GetUtcNow() };
            }
        }
    }

    private string? FindOldestKey(Guid? partitionId)
    {
        string? oldestKey = null;
        DateTimeOffset oldestAccess = DateTimeOffset.MaxValue;
        foreach ((string key, TrackedEntry entry) in _entries)
        {
            if ((!partitionId.HasValue || entry.PartitionId == partitionId.Value) &&
                (entry.LastAccessUtc < oldestAccess ||
                (entry.LastAccessUtc == oldestAccess &&
                 string.CompareOrdinal(key, oldestKey) < 0)))
            {
                oldestKey = key;
                oldestAccess = entry.LastAccessUtc;
            }
        }

        return oldestKey;
    }

    private void AddTracked(string key, TrackedEntry entry)
    {
        _entries.Add(key, entry);
        _retainedItems += entry.Usage.Items;
        _retainedContinuations += entry.Usage.Continuations;
        _retainedBytes += entry.Usage.Bytes;
    }

    private void RemoveTracked(string key)
    {
        if (!_entries.Remove(key, out TrackedEntry? entry))
        {
            return;
        }

        _retainedItems -= entry.Usage.Items;
        _retainedContinuations -= entry.Usage.Continuations;
        _retainedBytes -= entry.Usage.Bytes;
    }

    private void UpdateRetention()
        => metrics.SetRetention(new StorageBrowseCacheRetention(
            _entries.Count,
            _retainedItems,
            _retainedContinuations,
            _retainedBytes));

    private sealed record TrackedEntry(
        Guid PartitionId,
        EntryUsage Usage,
        DateTimeOffset LastAccessUtc,
        DateTimeOffset ExpiresAtUtc);

    private readonly record struct EntryUsage(int Items, int Continuations, long Bytes);

    private sealed class CacheBypassException(StorageBrowsePage page) : Exception
    {
        public StorageBrowsePage Page { get; } = page;
    }
}
