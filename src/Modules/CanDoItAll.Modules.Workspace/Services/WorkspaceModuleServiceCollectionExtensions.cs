using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.Workspace.ApiAccess;

namespace CanDoItAll.Modules.Workspace;

public static class WorkspaceModuleServiceCollectionExtensions
{
    public static IServiceCollection AddWorkspaceModule(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<
            IProjectTransferTargetStateParticipant,
            WorkspaceProjectTransferTargetStateParticipant>());
        services.AddOptions<ApiAccessOptions>();
        services.TryAddSingleton<IApiTokenService, ApiTokenService>();
        services.TryAddScoped<ConnectorPluginRegistry>();
        services.TryAddScoped<ISettingsRendererRegistry, SettingsRendererRegistry>();
        services.AddScoped<ConnectorCommandProcessor>();
        services.AddScoped<ConnectorOutboxService>();
        services.AddScoped<WorkspaceService>();
        services.TryAddScoped<IStorageCatalogSelectionSource, WorkspaceStorageCatalogSelectionSource>();
        services.AddScoped<DatabaseProfileWorkspaceService>();
        services.AddScoped<IProjectManagementKnowledgeProvider, StaticProjectManagementKnowledgeProvider>();
        services.AddScoped<ProjectManagementKnowledgeService>();
        return services;
    }
}

public static class WorkspaceModuleAssemblyMarker;
