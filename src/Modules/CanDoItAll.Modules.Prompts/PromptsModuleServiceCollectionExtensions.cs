using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace CanDoItAll.Modules.Prompts;

public static class PromptsModuleServiceCollectionExtensions
{
    public static IServiceCollection AddPromptsModule(this IServiceCollection services)
    {
        services.TryAddScoped<IPromptGallerySearchDriver, EfPromptGallerySearchDriver>();
        services.TryAddSingleton<DisabledPromptGalleryProjectionDriver>();
        services.TryAddSingleton<IPromptGalleryProjectionDriver>(serviceProvider =>
            serviceProvider.GetRequiredService<DisabledPromptGalleryProjectionDriver>());
        services.TryAddSingleton<PromptGalleryCompatibilityEvaluator>();
        services.TryAddSingleton<PromptGallerySeedLoader>();
        services.TryAddScoped<PromptGallerySeedImporter>();
        services.TryAddScoped<PromptGalleryProjectionCoordinator>();
        services.TryAddScoped<IPromptGalleryProjectionCoordinator>(serviceProvider =>
            serviceProvider.GetRequiredService<PromptGalleryProjectionCoordinator>());
        services.TryAddScoped<PromptsService>();
        services.TryAddScoped<IPromptGalleryService>(serviceProvider =>
            serviceProvider.GetRequiredService<PromptsService>());
        services.TryAddScoped<IPromptGalleryImportService>(serviceProvider =>
            serviceProvider.GetRequiredService<PromptsService>());
        services.TryAddSingleton<IPromptGalleryCuratorLauncher, UnavailablePromptGalleryCuratorLauncher>();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, PromptGallerySeedImportHostedService>());
        return services;
    }

    public static IServiceCollection AddPromptGallerySearchIndexProjection(this IServiceCollection services)
    {
        services.Replace(
            ServiceDescriptor.Scoped<IPromptGalleryProjectionDriver, SearchIndexPromptGalleryProjectionDriver>());
        return services;
    }
}

public static class PromptsModuleAssemblyMarker;


