using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Modules.Prompts;

public static class PromptsModuleServiceCollectionExtensions
{
    public static IServiceCollection AddPromptsModule(this IServiceCollection services)
    {
        services.AddScoped<PromptsService>();
        return services;
    }
}

public static class PromptsModuleAssemblyMarker;


