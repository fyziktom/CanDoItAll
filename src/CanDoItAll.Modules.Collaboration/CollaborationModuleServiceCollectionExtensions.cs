using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Modules.Collaboration;

public static class CollaborationModuleServiceCollectionExtensions
{
    public static IServiceCollection AddCollaborationModule(this IServiceCollection services)
    {
        services.AddScoped<CollaborationService>();
        return services;
    }
}

public static class CollaborationModuleAssemblyMarker;
