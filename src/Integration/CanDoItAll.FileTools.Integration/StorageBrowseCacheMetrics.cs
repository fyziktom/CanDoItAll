namespace CanDoItAll.FileTools.Integration;

public sealed record StorageBrowseCacheMetricsSnapshot(
    long Hits,
    long Misses,
    long Bypasses,
    long Evictions,
    int RetainedEntries,
    int RetainedItems,
    int RetainedContinuations,
    long RetainedBytes);

public interface IStorageBrowseCacheMetrics
{
    StorageBrowseCacheMetricsSnapshot GetSnapshot();
}

internal sealed class StorageBrowseCacheMetrics : IStorageBrowseCacheMetrics
{
    private long _hits;
    private long _misses;
    private long _bypasses;
    private long _evictions;
    private StorageBrowseCacheRetention _retention = StorageBrowseCacheRetention.Empty;

    public StorageBrowseCacheMetricsSnapshot GetSnapshot()
    {
        StorageBrowseCacheRetention retention = Volatile.Read(ref _retention);
        return new StorageBrowseCacheMetricsSnapshot(
            Interlocked.Read(ref _hits),
            Interlocked.Read(ref _misses),
            Interlocked.Read(ref _bypasses),
            Interlocked.Read(ref _evictions),
            retention.Entries,
            retention.Items,
            retention.Continuations,
            retention.Bytes);
    }

    public void Hit() => Interlocked.Increment(ref _hits);

    public void Miss() => Interlocked.Increment(ref _misses);

    public void Bypass() => Interlocked.Increment(ref _bypasses);

    public void Evicted(int count)
    {
        if (count > 0)
        {
            Interlocked.Add(ref _evictions, count);
        }
    }

    public void SetRetention(StorageBrowseCacheRetention retention)
        => Volatile.Write(ref _retention, retention);
}

internal sealed record StorageBrowseCacheRetention(
    int Entries,
    int Items,
    int Continuations,
    long Bytes)
{
    public static StorageBrowseCacheRetention Empty { get; } = new(0, 0, 0, 0);
}
