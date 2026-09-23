using Microsoft.EntityFrameworkCore;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Modules.Projects;

public static class ProjectsModuleServiceCollectionExtensions
{
    public static IServiceCollection AddProjectsModule(this IServiceCollection services) {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<
            IToolInvocationPolicyContextContributor, ProjectWorkspacePathContributor>());
        services.AddPooledDbContextFactory<ProjectsDbContext>((provider, options) => {
            AppDbContextOptionsConfigurator.Configure(options, provider.GetRequiredService<ICanonicalRuntimeDatabase>().Profile);
        });
        services.TryAddEnumerable(ServiceDescriptor.Scoped<
            IAgentExecutionSourceAuthorityProvider,
            ProjectsExecutionAuthorityProvider>());
        services.AddScoped<ProjectsService>();
        services.AddScoped<IProjectSummaryQueryService>(provider => provider.GetRequiredService<ProjectsService>());
        services.AddScoped<ProjectWriteAdmissionService>();
        services.AddScoped<ProjectWriteSelectionQuery>();
        services.AddScoped<ProjectRecordQueryService>();
        services.AddScoped<ProjectsProfileTransferStore>();
        services.AddSingleton<ProjectIdentityQueryService>();
        services.AddScoped<ProjectStructureProjectionQueryService>();
        services.AddScoped<IProjectTransferReferenceQuery, ProjectTransferReferenceQuery>();
        services.AddScoped<IProjectRecordQueryService>(provider => provider.GetRequiredService<ProjectRecordQueryService>());
        services.AddScoped<IRecentProjectActivityQueryService, RecentProjectActivityQueryService>();
        // Projects owns the bridge contracts and their no-op defaults; the owning modules (Workbench, CrmHr) replace
        // them, so the defaults never win over an owner whatever the module registration order.
        services.TryAddScoped<IProjectNodeScopeBridge, NoopProjectNodeScopeBridge>();
        services.TryAddScoped<IProjectNodeDetailsBridge, NoopProjectNodeDetailsBridge>();
        services.TryAddScoped<IProjectNodeAssignmentPolicyBridge, NoopProjectNodeAssignmentPolicyBridge>();
        services.TryAddScoped<
            IProjectWorkItemAssignmentMutationBridge,
            NoopProjectWorkItemAssignmentMutationBridge>();
        services.TryAddScoped<IProjectPartyIntegrationBridge, NoopProjectPartyIntegrationBridge>();
        services.TryAddScoped<IProjectPartyCostRateBridge, NoopProjectPartyCostRateBridge>();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IFileToolsStorageBindingSource, ProjectFileToolsStorageBindingSource>());
        services.AddScoped<ProjectFileReadOnlyInteractionFactory>();
        services.AddScoped<IProjectFileScopeProvider, ProjectFileScopeProvider>();
        services.AddScoped<IProjectFilesPilotCoordinator, ProjectFilesPilotCoordinator>();
        services.AddScoped<IProjectFilePortfolioCoordinator, ProjectFilePortfolioCoordinator>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<
            IProjectTransferTargetStateParticipant,
            ProjectsProjectTransferTargetStateParticipant>());
        return services;
    }
}

public static class ProjectsModuleAssemblyMarker;
