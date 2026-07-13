using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.FileTools.Integration;

internal sealed class CachingStorageBrowseDriver(
    IStorageBrowseDriver inner,
    StorageBrowseCacheContext context,
    StorageBrowseCachePolicy policy,
    IStorageBrowseCacheStore cache,
    IFileCatalogRevisionReader revisions,
    IDatabaseRuntimeState runtimeState) : IStorageBrowseDriver
{
    public StorageProviderKind ProviderKind => inner.ProviderKind;

    public StorageBrowseCapability Capabilities => inner.Capabilities;

    public StorageBrowseWorkBudget MaximumBudget => inner.MaximumBudget;

    public async Task<StorageBrowsePage> BrowseAsync(
        StorageCatalogRecord storage,
        StorageBrowseRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!policy.Enabled)
        {
            return await inner.BrowseAsync(storage, request, cancellationToken);
        }

        if (request.PageSize > policy.Settings.MaximumPageSize)
        {
            cache.RecordBypass();
            return await inner.BrowseAsync(storage, request, cancellationToken);
        }

        DatabaseRuntimeSnapshot runtime = runtimeState.GetSnapshot();
        FileCatalogRevision revision = revisions.Get(context.Scope, context.Storage.Id);
        string key = StorageBrowseCacheKeyBuilder.Build(context, request, runtime, revision);
        return await cache.GetOrCreateAsync(
            key,
            context.Storage.Id,
            policy.Settings,
            token => new ValueTask<StorageBrowsePage>(inner.BrowseAsync(storage, request, token)),
            cancellationToken);
    }
}
