using CanDoItAll.FileTools.Integration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Modules.Projects;

public static class ProjectsModuleServiceCollectionExtensions
{
    public static IServiceCollection AddProjectsModule(this IServiceCollection services)
    {
        services.AddScoped<ProjectsService>();
        services.AddScoped<IRecentProjectActivityQueryService, RecentProjectActivityQueryService>();
        services.AddScoped<IProjectNodeScopeBridge, NoopProjectNodeScopeBridge>();
        services.TryAddScoped<IProjectNodeDetailsBridge, NoopProjectNodeDetailsBridge>();
        services.AddScoped<IProjectNodeAssignmentPolicyBridge, NoopProjectNodeAssignmentPolicyBridge>();
        services.TryAddScoped<
            IProjectWorkItemAssignmentMutationBridge,
            NoopProjectWorkItemAssignmentMutationBridge>();
        services.AddScoped<IProjectPartyIntegrationBridge, NoopProjectPartyIntegrationBridge>();
        services.AddScoped<IProjectPartyCostRateBridge, NoopProjectPartyCostRateBridge>();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IFileToolsStorageBindingSource, ProjectFileToolsStorageBindingSource>());
        services.AddScoped<ProjectFileReadOnlyInteractionFactory>();
        services.AddScoped<IProjectFileScopeProvider, ProjectFileScopeProvider>();
        services.AddScoped<IProjectFilesPilotCoordinator, ProjectFilesPilotCoordinator>();
        services.AddScoped<IProjectFilePortfolioCoordinator, ProjectFilePortfolioCoordinator>();
        return services;
    }
}

public static class ProjectsModuleAssemblyMarker;


