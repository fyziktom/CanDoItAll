using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.FileTools.Integration;

internal sealed class StorageFileToolsBrowseSessionFactory(
    IFileToolsStorageBindingProvider bindingProvider,
    IStorageCatalogService storageCatalog,
    IStorageBrowseDriverRegistry driverRegistry,
    IStorageBrowseCacheStore cache,
    IFileCatalogRevisionReader revisions,
    IDatabaseRuntimeState runtimeState) : IFileToolsBrowseSessionFactory
{
    public async ValueTask<FileToolsBrowseSession> CreateAsync(
        FileToolsSemanticScope scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        IReadOnlyList<FileToolsStorageBinding> bindings = await bindingProvider.ResolveAsync(scope, cancellationToken);
        if (bindings.Count > StorageBrowseCacheKeyBuilder.MaximumSourceCount)
        {
            throw new FileBrowserProviderException(new FileBrowserError(
                FileBrowserErrorCode.InvalidOperation,
                "The semantic file scope contains too many storage sources."));
        }

        var storageIds = new HashSet<Guid>();
        var providers = new List<IFileBrowserProvider>(bindings.Count);
        var sourceIds = new HashSet<FileBrowserSourceId>();
        var sourceRevisions = new List<StorageBrowseSourceRevisionPart>(bindings.Count);
        string sourceSetFingerprint = StorageBrowseCacheKeyBuilder.BuildSourceSetFingerprint(bindings);
        foreach (FileToolsStorageBinding binding in bindings)
        {
            if (!storageIds.Add(binding.StorageId))
            {
                throw new FileBrowserProviderException(new FileBrowserError(
                    FileBrowserErrorCode.CorruptProviderResponse,
                    "The semantic file scope contains a duplicate storage binding."));
            }

            StorageCatalogRecord storage = await storageCatalog.GetAsync(binding.StorageId, cancellationToken)
                ?? throw new FileBrowserProviderException(new FileBrowserError(
                    FileBrowserErrorCode.NotFound,
                    "A storage binding for the semantic file scope no longer exists."));
            IStorageBrowseDriver driver;
            try
            {
                driver = driverRegistry.Resolve(storage.ProviderKind);
            }
            catch (StorageBrowseException)
            {
                throw new FileBrowserProviderException(new FileBrowserError(
                    FileBrowserErrorCode.Unsupported,
                    "The configured storage does not support file browsing."));
            }

            StorageBrowseCachePolicy cachePolicy = StorageBrowseCachePolicy.Resolve(storage, binding, driver);
            string storageFingerprint = StorageBrowseCacheKeyBuilder.BuildStorageFingerprint(storage, binding);
            var context = new StorageBrowseCacheContext(
                scope,
                binding,
                storage,
                sourceSetFingerprint,
                storageFingerprint);
            var listingDriver = new CachingStorageBrowseDriver(
                driver,
                context,
                cachePolicy,
                cache,
                revisions,
                runtimeState);
            var provider = new StorageFileBrowserProvider(scope, binding, storage, listingDriver, driver);
            if (!sourceIds.Add(provider.Descriptor.Id))
            {
                throw new FileBrowserProviderException(new FileBrowserError(
                    FileBrowserErrorCode.CorruptProviderResponse,
                    "The semantic file scope produced a duplicate source identifier."));
            }

            providers.Add(provider);
            sourceRevisions.Add(new StorageBrowseSourceRevisionPart(
                provider.Descriptor.Id.Value,
                storageFingerprint,
                revisions.Get(scope, storage.Id)));
        }

        return new FileToolsBrowseSession(
            scope,
            providers,
            new FileBrowserSortDescriptor(
                FileBrowserSortField.ProviderNative,
                FileBrowserSortDirection.Ascending,
                FoldersFirst: false),
            StorageBrowseCacheKeyBuilder.BuildSessionRevision(
                scope,
                sourceSetFingerprint,
                sourceRevisions,
                runtimeState.GetSnapshot()));
    }
}
