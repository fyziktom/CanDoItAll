using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Modules.Factory;

public static class FactoryModuleServiceCollectionExtensions
{
    public static IServiceCollection AddFactoryModule(this IServiceCollection services)
    {
        services.AddSingleton<PromptLibraryPackLoader>();
        services.AddScoped<PromptFactoryService>();
        return services;
    }
}

public static class FactoryModuleAssemblyMarker;
