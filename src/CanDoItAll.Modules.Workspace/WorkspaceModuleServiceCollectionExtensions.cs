using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Modules.Workspace;

public static class WorkspaceModuleServiceCollectionExtensions
{
    public static IServiceCollection AddWorkspaceModule(this IServiceCollection services)
    {
        services.AddHttpClient();
        services.AddScoped<IProviderAdapter, OpenAiProviderAdapter>();
        services.AddScoped<IProviderAdapter, OllamaProviderAdapter>();
        services.AddScoped<IProviderAdapter, OllamaRemoteProviderAdapter>();
        services.AddScoped<ProviderRegistry>();
        services.AddScoped<ProviderExecutionService>();
        services.AddScoped<WorkspaceService>();
        return services;
    }
}

public static class WorkspaceModuleAssemblyMarker;


