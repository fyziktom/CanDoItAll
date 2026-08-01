using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.FileTools.Integration;

internal sealed record StorageBrowseCachePolicy(
    bool Enabled,
    StorageBrowseCacheSettings Settings)
{
    public static StorageBrowseCachePolicy Resolve(
        StorageCatalogRecord storage,
        FileToolsStorageBinding binding,
        IStorageBrowseDriver driver)
    {
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(driver);
        StorageBrowseCacheSettings settings = StorageJson
            .ParseProviderConfiguration(storage.ConfigJson)
            .BrowseCache;
        if (binding.HostCacheMode == FileToolsHostBrowseCacheMode.Disabled || !settings.Enabled)
        {
            return new StorageBrowseCachePolicy(false, settings);
        }

        if (settings.Mode != StorageBrowseCacheMode.Memory)
        {
            throw Invalid("Only process-local memory browse caching is supported.");
        }

        if (settings.ImmutableVersionPolicy == StorageBrowseImmutableVersionPolicy.RequireProviderVerifiedVersion &&
            (!driver.Capabilities.HasFlag(StorageBrowseCapability.ImmutableVersion) ||
             storage.ProviderKind != StorageProviderKind.Ipfs ||
             !binding.Root.Value.StartsWith("cid:", StringComparison.Ordinal)))
        {
            throw Invalid("The configured browse root does not have a provider-verified immutable version.");
        }

        return new StorageBrowseCachePolicy(true, settings);
    }

    private static StorageBrowseException Invalid(string message)
        => new(new StorageBrowseError(StorageBrowseErrorCode.InvalidConfiguration, message));
}
