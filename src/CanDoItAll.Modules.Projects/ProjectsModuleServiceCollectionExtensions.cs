using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Modules.Projects;

public static class ProjectsModuleServiceCollectionExtensions
{
    public static IServiceCollection AddProjectsModule(this IServiceCollection services)
    {
        services.AddScoped<ProjectsService>();
        services.AddScoped<IProjectNodeScopeBridge, NoopProjectNodeScopeBridge>();
        services.AddScoped<IProjectNodeAssignmentPolicyBridge, NoopProjectNodeAssignmentPolicyBridge>();
        services.AddScoped<IProjectPartyIntegrationBridge, NoopProjectPartyIntegrationBridge>();
        return services;
    }
}

public static class ProjectsModuleAssemblyMarker;


