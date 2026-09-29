using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Modules.Workspace.ApiAccess.Contracts;
using CanDoItAll.Modules.Workspace.ApiAccess.Presentation;
using CanDoItAll.Workspace.ApiAccess.UI;
using CanDoItAll.Workspace.StorageCatalog.UI;
using CanDoItAll.Modules.Workspace.StorageCatalog.Contracts;

namespace CanDoItAll.Modules.Workspace;

public static class WorkspaceModuleServiceCollectionExtensions
{
    public static IServiceCollection AddWorkspaceModule(this IServiceCollection services)
    {
        services.AddPooledDbContextFactory<WorkspaceSettingsDbContext>((provider, options) => {
            AppDbContextOptionsConfigurator.Configure(options, provider.GetRequiredService<ICanonicalRuntimeDatabase>().Profile);
        });
        services.AddPooledDbContextFactory<WorkspaceConnectorCommandDbContext>((provider, options) => {
            AppDbContextOptionsConfigurator.Configure(options, provider.GetRequiredService<ICanonicalRuntimeDatabase>().Profile);
        });
        services.TryAddEnumerable(ServiceDescriptor.Scoped<
            IProjectTransferTargetStateParticipant,
            WorkspaceProjectTransferTargetStateParticipant>());
        services.AddOptions<ApiAccessOptions>();
        services.AddSingleton<WorkspaceDefaultsBootstrapService>();
        services.TryAddSingleton<IApiTokenService, ApiTokenService>();
        services.TryAddSingleton<ApiPasswordService>();
        services.TryAddSingleton<ApiSessionService>();
        services.TryAddScoped<ApiUserAdministrationService>();
        services.TryAddScoped<IApiTokenAdministrationAccess, UnavailableApiTokenAdministrationAccess>();
        services.TryAddScoped<ApiTokenAdministrationService>();
        services.TryAddScoped<IApiAccessConfigurationOwner, ApiAccessConfigurationOwner>();
        services.TryAddScoped<IApiTokenOwner, ApiTokenOwner>();
        services.TryAddScoped<IApiAccountOwner, ApiAccountOwner>();
        services.TryAddScoped<ApiOperationLedger>();
        services.TryAddScoped<ConnectorPluginRegistry>();
        services.TryAddScoped<ISettingsRendererRegistry, SettingsRendererRegistry>();
        services.AddScoped<ConnectorCommandProcessor>();
        services.AddScoped<ConnectorOutboxService>();
        services.AddScoped<WorkspaceService>();
        services.AddScoped<StorageCatalogCommands>();
        services.AddScoped<IStorageCatalogOwner, WorkspaceStorageCatalogOwner>();
        services.AddScoped<CatalogOperationLedger>();
        services.AddScoped<IWorkspaceDefaultsOwner, WorkspaceDefaultsOwner>();
        services.AddScoped<IWorkspaceSecretsOwner, WorkspaceSecretsOwner>();
        services.AddScoped<IWorkspaceFilesOwner, WorkspaceFilesOwner>();
        services.TryAddScoped<IStorageCatalogSelectionSource, WorkspaceStorageCatalogSelectionSource>();
        services.AddScoped<DatabaseProfileWorkspaceService>();
        services.AddScoped<IProjectManagementKnowledgeProvider, StaticProjectManagementKnowledgeProvider>();
        services.AddScoped<ProjectManagementKnowledgeService>();
        return services;
    }
}

public static class WorkspaceModuleAssemblyMarker;
