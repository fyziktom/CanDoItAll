namespace CanDoItAll.Modules.CrmHr.Pages;

internal readonly record struct CrmQueryLoadStamp(long Generation, string QueryKey);

internal sealed class CrmQueryLoadGeneration
{
    private long generation;

    public CrmQueryLoadStamp Begin(string queryKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queryKey);
        return new CrmQueryLoadStamp(
            Interlocked.Increment(ref generation),
            queryKey);
    }

    public bool IsCurrent(CrmQueryLoadStamp stamp, string currentQueryKey)
    {
        return stamp.Generation == Volatile.Read(ref generation) &&
               string.Equals(
                   stamp.QueryKey,
                   currentQueryKey,
                   StringComparison.Ordinal);
    }

    public void Invalidate()
        => Interlocked.Increment(ref generation);
}
