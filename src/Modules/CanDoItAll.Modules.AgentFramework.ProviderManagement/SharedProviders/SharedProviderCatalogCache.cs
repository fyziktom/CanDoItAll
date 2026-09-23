namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

public interface ISharedProviderPublicationCommitObserver
{
    Task PublicationChangedAsync(
        Guid providerProfileId,
        CancellationToken cancellationToken = default);
}

public sealed class SharedProviderCatalogCache
{
    private readonly object gate = new();
    private string? persistedStamp;
    private SharedProviderCatalogProjection? projection;

    public bool TryGet(
        string currentPersistedStamp,
        out SharedProviderCatalogProjection cachedProjection)
    {
        ArgumentException.ThrowIfNullOrEmpty(currentPersistedStamp);
        lock (gate)
        {
            if (projection is not null &&
                string.Equals(persistedStamp, currentPersistedStamp, StringComparison.Ordinal))
            {
                cachedProjection = projection;
                return true;
            }
        }

        cachedProjection = null!;
        return false;
    }

    public void Set(
        string currentPersistedStamp,
        SharedProviderCatalogProjection currentProjection)
    {
        ArgumentException.ThrowIfNullOrEmpty(currentPersistedStamp);
        ArgumentNullException.ThrowIfNull(currentProjection);
        lock (gate)
        {
            persistedStamp = currentPersistedStamp;
            projection = currentProjection;
        }
    }

    internal void Invalidate()
    {
        lock (gate)
        {
            persistedStamp = null;
            projection = null;
        }
    }
}

internal sealed class SharedProviderCatalogPublicationCommitObserver(
    SharedProviderCatalogCache cache)
    : ISharedProviderPublicationCommitObserver
{
    public Task PublicationChangedAsync(
        Guid providerProfileId,
        CancellationToken cancellationToken = default)
    {
        cache.Invalidate();
        return Task.CompletedTask;
    }
}

internal sealed class SharedProviderCatalogProfileCommitObserver(
    SharedProviderCatalogCache cache)
    : IProviderProfileCommitObserver
{
    public Task ProviderSavedAsync(
        Guid providerId,
        CancellationToken cancellationToken = default)
    {
        cache.Invalidate();
        return Task.CompletedTask;
    }

    public Task ProviderDeletedAsync(
        Guid providerId,
        CancellationToken cancellationToken = default)
    {
        cache.Invalidate();
        return Task.CompletedTask;
    }
}
