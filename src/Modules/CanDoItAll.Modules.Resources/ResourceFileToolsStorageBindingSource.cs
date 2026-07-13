using System.Security.Cryptography;
using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.Modules.Resources;

internal sealed class ResourceFileToolsStorageBindingSource(
    IStorageCatalogService storageCatalog,
    IStorageBrowseDriverRegistry browseDrivers) : IFileToolsStorageBindingSource
{
    private static readonly FileToolsBrowseWorkLimits WorkLimits = new(
        maximumReturnedItems: 100,
        maximumInspectedItems: 2_000,
        maximumMetadataProbes: 100,
        maximumConcurrentMetadataProbes: 1,
        maximumDuration: TimeSpan.FromSeconds(5));

    public FileToolsSemanticScopeKind ScopeKind => FileToolsSemanticScopeKind.ResourceSource;

    public async ValueTask<IReadOnlyList<FileToolsStorageBinding>> ResolveAsync(
        FileToolsSemanticScope scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (scope.Kind != ScopeKind ||
            !ResourceStorageSourceScopeKey.TryParse(scope.Id, out Guid storageId, out string expectedFingerprint))
        {
            throw ProviderError(
                FileBrowserErrorCode.InvalidOperation,
                "The Resources storage-source scope identifier is invalid.");
        }

        StorageCatalogRecord storage = await storageCatalog.GetAsync(storageId, cancellationToken)
            ?? throw ProviderError(
                FileBrowserErrorCode.NotFound,
                "The Resources storage source no longer exists.");
        string currentFingerprint = ResourceStorageSourceScopeKey.BuildFingerprint(storage);
        if (!CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(expectedFingerprint),
            Convert.FromHexString(currentFingerprint)))
        {
            throw ProviderError(
                FileBrowserErrorCode.Conflict,
                "The Resources storage source changed. Refresh the source catalog before continuing.");
        }

        if (!storage.IsEnabled ||
            !storage.CapabilityMask.HasFlag(StorageCapability.Read) ||
            !browseDrivers.TryResolve(storage.ProviderKind, out _))
        {
            throw ProviderError(
                FileBrowserErrorCode.Unavailable,
                "The Resources storage source is not currently available for browsing.");
        }

        return
        [
            new FileToolsStorageBinding(
                storage.Id,
                storage.Name,
                WorkLimits,
                FileToolsStorageRoot.StorageRoot,
                FileToolsHostBrowseCacheMode.UseStoragePolicy)
        ];
    }

    private static FileBrowserProviderException ProviderError(FileBrowserErrorCode code, string message)
        => new(new FileBrowserError(code, message));
}
