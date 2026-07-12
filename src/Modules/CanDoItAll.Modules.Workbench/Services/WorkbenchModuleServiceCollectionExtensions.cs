using CanDoItAll.Memory.SourceGateway;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Memory.Application;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.Processes.Application;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

public static class WorkbenchModuleServiceCollectionExtensions
{
    public static IServiceCollection AddWorkbenchModule(this IServiceCollection services)
    {
        services.AddHttpClient();
        services.AddScoped<WorkbenchStateService>();
        services.AddScoped<ProjectCrossModuleMutationCoordinator>();
        services.AddScoped<ProjectCrossModuleMutationProcessor>();
        services.AddScoped<ProjectWorkbenchCommandService>();
        services.AddScoped<ProjectWorkbenchCrossModuleMutationService>();
        services.AddScoped<ProjectWorkbenchLifecycleService>();
        services.AddScoped<ProjectWorkbenchRelationService>();
        services.AddScoped<ProjectStructureAssemblyService>();
        services.AddScoped<ProjectStructureProjectionMaintenanceService>();
        services.AddScoped<IProjectStructureProjectionContributor, ProjectHierarchyProjectionContributor>();
        services.AddScoped<IProjectStructureProjectionContributor, ProjectResourceProjectionContributor>();
        services.AddScoped<IProjectStructureProjectionContributor, PromptFactoryProjectionContributor>();
        services.AddScoped<IProjectStructureProjectionContributor, ProjectStructureProcessProjectionContributor>();
        services.AddScoped<IProjectStructureProjectionContributor, TestPlanProjectionContributor>();
        services.AddScoped<ProjectWorkbenchService>();
        services.AddScoped<IProjectNodeAssignmentPolicyBridge, ProjectNodeAssignmentPolicyBridge>();
        services.AddScoped<IProjectNodeScopeBridge, ProjectNodeScopeBridge>();
        services.AddScoped<ProjectStructureLeaseService>();
        services.AddScoped<ProjectStructureAnalyticsService>();
        services.AddScoped<ProjectStructureChecklistService>();
        services.AddScoped<ProjectStructureImportService>();
        services.AddSingleton<ProjectStructureDeferredNodeCompletionQueue>();
        services.AddSingleton<IProjectStructureDeferredNodeCompletionQueue>(serviceProvider =>
            serviceProvider.GetRequiredService<ProjectStructureDeferredNodeCompletionQueue>());
        services.AddScoped<ProjectStructureDeferredNodeCompletionProcessor>();
        services.AddSingleton<ProjectStructureDeferredNodeCompletionWorker>();
        services.AddHostedService(serviceProvider =>
            serviceProvider.GetRequiredService<ProjectStructureDeferredNodeCompletionWorker>());
        services.TryAddScoped<ProcessLaunchVariablePreparationService>();
        services.AddScoped<ProjectStructureProcessNodeService>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IProcessSubprocessLaunchCoordinator, ProjectStructureProcessSubprocessLaunchCoordinator>());
        services.AddScoped<ProjectStructureWorkflowNodeService>();
        services.TryAddScoped<IWorkspacePathResolutionService>(serviceProvider =>
        {
            var workspaceRoot = serviceProvider.GetRequiredService<IWorkspacePathResolver>().ResolveWorkspaceRoot();
            var profile = serviceProvider.GetRequiredService<IDatabaseProfileRuntimeAccessor>().ResolveCurrentProfile();
            var scope = WorkspaceScopeDescriptor.Organization(profile.Profile.Id.ToString("N"));
            return new WorkspacePathResolutionService(workspaceRoot, scope);
        });
        services.AddScoped<ProjectStructureSourceWorkspacePathResolver>();
        services.AddScoped<ProjectStructureAgentService>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAgentRuntimeToolProvider, ProjectStructureAgentRuntimeToolProvider>());
        services.AddScoped<IProjectStructureRuntimeGateway, WorkbenchProjectStructureRuntimeGateway>();
        services.AddScoped<IProjectStructureSourceSnapshotProvider, WorkbenchProjectStructureSourceSnapshotProvider>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IMemorySourceGatewayAdapter, WorkbenchProjectStructureMemorySourceGatewayAdapter>());
        services.AddScoped<ProjectMemoryIngestionService>();
        services.AddScoped<IProjectGanttPreviewService, ProjectGanttPreviewService>();
        services.AddScoped<IProjectStructureLocalFileOpener, ProjectStructureLocalFileOpener>();
        services.AddScoped<IProjectStructureRuntimeLauncher, ProjectStructureRuntimeLauncher>();
        services.AddScoped<IProjectWorkbenchSeedService>(serviceProvider => serviceProvider.GetRequiredService<ProjectWorkbenchService>());
        services.AddScoped<IDatabaseTransferHandler, ProjectsDatabaseTransferHandler>();
        services.AddScoped<IProjectPackageService, ProjectPackageService>();
        return services;
    }
}

public static class WorkbenchModuleAssemblyMarker;


