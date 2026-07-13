using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Caching.Hybrid;
using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.FileTools.Integration;

public static class FileToolsIntegrationServiceCollectionExtensions
{
    public static IServiceCollection AddCanDoItAllFileToolsIntegration(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddLogging();
        services.AddOptions<FileAccessHandleOptions>()
            .Validate(
                options => options.MaximumEntries is >= 16 and <= 65_536 &&
                           options.Lifetime >= TimeSpan.FromSeconds(10) &&
                           options.Lifetime <= TimeSpan.FromHours(1) &&
                           options.MaximumContentBytes is >= 1024 and <= 256L * 1024 * 1024,
                "File access handle settings are invalid.")
            .ValidateOnStart();
        services.TryAddSingleton(TimeProvider.System);
        services.AddHybridCache(options =>
        {
            options.MaximumKeyLength = Math.Max(
                options.MaximumKeyLength,
                StorageBrowseCacheKeyBuilder.MaximumCacheKeyLength);
            options.MaximumPayloadBytes = Math.Max(
                options.MaximumPayloadBytes,
                StorageBrowseCacheSettings.AbsoluteMaximumPayloadBytes);
        });
        services.TryAddSingleton<StorageBrowseCacheMetrics>();
        services.TryAddSingleton<IStorageBrowseCacheMetrics>(provider =>
            provider.GetRequiredService<StorageBrowseCacheMetrics>());
        services.TryAddSingleton<IStorageBrowseCacheStore, HybridStorageBrowseCacheStore>();
        services.TryAddSingleton<ProcessLocalFileCatalogRevisionService>();
        services.TryAddSingleton<IFileCatalogRevisionReader>(provider =>
            provider.GetRequiredService<ProcessLocalFileCatalogRevisionService>());
        services.TryAddSingleton<IFileCatalogChangeSink>(provider =>
            provider.GetRequiredService<ProcessLocalFileCatalogRevisionService>());
        services.TryAddSingleton<IFileAccessHandleRegistry, FileAccessHandleRegistry>();
        services.TryAddSingleton<IFileAccessPolicy, LocalWorkspaceFileAccessPolicy>();
        services.TryAddSingleton<IFileAccessContextProvider, LocalWorkspaceFileAccessContextProvider>();
        services.TryAddScoped<IStorageFileAccessAuthorizationCoordinator, StorageFileAccessAuthorizationCoordinator>();
        services.TryAddScoped<IFileToolsStorageBindingProvider, CompositeFileToolsStorageBindingProvider>();
        services.AddScoped<IFileToolsBrowseSessionFactory, StorageFileToolsBrowseSessionFactory>();
        services.AddScoped<IFileToolsBrowseItemActivator, StorageFileToolsBrowseItemActivator>();
        services.AddScoped<IFileToolsKnownFileActivator, StorageFileToolsKnownFileActivator>();
        services.AddScoped<AuthorizedFileContentSource>();
        services.AddScoped<AuthorizedFileSaveTarget>();
        services.AddScoped<IFileToolsKnownFileSessionFactory, AuthorizedFileToolsKnownFileSessionFactory>();
        services.AddScoped<IFileToolsKnownFileSessionReleaser, AuthorizedFileToolsKnownFileSessionReleaser>();
        services.AddScoped<IAuthorizedFileHttpContentService, AuthorizedFileHttpContentService>();
        return services;
    }

    public static IServiceCollection AddCanDoItAllFileToolsStoragePlacementRevision(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        ServiceDescriptor[] placementRegistrations = services
            .Where(descriptor => descriptor.ServiceType == typeof(IStoragePlacementService))
            .ToArray();
        bool hasConcretePlacement = services.Any(descriptor =>
            descriptor.ServiceType == typeof(StoragePlacementService));
        if (!hasConcretePlacement || placementRegistrations.Length != 1)
        {
            throw new InvalidOperationException(
                "FileTools placement revision requires the single Infrastructure storage placement registration.");
        }

        services.RemoveAll<IStoragePlacementService>();
        services.AddSingleton<IStoragePlacementService, RevisionPublishingStoragePlacementService>();
        return services;
    }
}
