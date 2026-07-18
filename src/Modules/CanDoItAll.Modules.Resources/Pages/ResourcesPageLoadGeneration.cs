namespace CanDoItAll.Modules.Resources.Pages;

internal readonly record struct ResourcesPageLoadStamp(long Generation);

internal sealed class ResourcesPageLoadGeneration
{
    private readonly Lock sync = new();
    private long generation;

    public ResourcesPageLoadStamp Begin()
    {
        lock (sync)
        {
            return new ResourcesPageLoadStamp(++generation);
        }
    }

    public bool IsCurrent(ResourcesPageLoadStamp stamp)
    {
        lock (sync)
        {
            return stamp.Generation == generation;
        }
    }

    public bool TryCommit(ResourcesPageLoadStamp stamp, Action commit)
    {
        ArgumentNullException.ThrowIfNull(commit);

        lock (sync)
        {
            if (stamp.Generation != generation)
            {
                return false;
            }

            commit();
            return true;
        }
    }
}
