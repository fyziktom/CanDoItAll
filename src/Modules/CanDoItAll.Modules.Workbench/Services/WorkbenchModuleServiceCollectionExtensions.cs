using CanDoItAll.Memory.SourceGateway;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.AppComponents.FileTools;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.FileTools.FileInteraction.Components;
using CanDoItAll.FileTools.FileInteraction.Markdown;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.Memory.Application;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.Modules.Workbench.CanvasAdapters;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.Processes.Application;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tools.Documents;

namespace CanDoItAll.Modules.Workbench;

public static class WorkbenchModuleServiceCollectionExtensions
{
    public static IServiceCollection AddWorkbenchModule(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<
            IAgentExecutionSourceAuthorityProvider,
            ProjectStructureExecutionAuthorityProvider>());
        services.AddFileInteractionComponents(builder => builder
            .AddBuiltIns()
            .AddZoomPanRenderers()
            .AddMarkdown()
            .AddWorkbenchMermaid()
            .AddWorkbenchSpreadsheetPreview());
        services.TryAddScoped<ISpreadsheetDocumentService, ClosedXmlSpreadsheetDocumentService>();
        services.TryAddScoped<ISpreadsheetWorkbookContentPreviewService>(serviceProvider =>
            serviceProvider.GetRequiredService<ISpreadsheetDocumentService>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<
            IMarkdownFencedCodeComponentRegistration,
            WorkbenchMarkdownMermaidComponentRegistration>());
        services.AddHttpClient();
        services.AddHttpClient(ProjectStructureExternalAssetSourcePolicy.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(ProjectStructureExternalAssetSourceHttpClient.CreatePrimaryHandler);
        services.AddScoped<WorkbenchStateService>();
        services.AddScoped<ProjectCrossModuleMutationCoordinator>();
        services.AddScoped<ProjectManagedStoragePhysicalIdentityPolicy>();
        services.AddScoped<ProjectManagedStorageDeletionPlanner>();
        services.AddScoped<ProjectManagedStorageDeletionService>();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(ProjectCrossModuleMutationProcessingOptions.Default);
        services.AddScoped<ProjectCrossModuleMutationProcessor>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<
            IProjectDeletionParticipant,
            ProjectWorkbenchDeletionParticipant>());
        services.AddScoped<ProjectWorkbenchCommandService>();
        services.AddScoped<ProjectWorkbenchCrossModuleMutationService>();
        services.AddScoped<ProjectWorkbenchLifecycleService>();
        services.AddScoped<ProjectWorkbenchRelationService>();
        services.AddScoped<ProjectStructureAssemblyService>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<
            IAgentContextContributor,
            AgentContext.ProjectStructureRuntimeGuidanceContributor>());
        services.AddScoped<ProjectStructureGanttMutationService>();
        services.AddSingleton<ProjectStructureGanttProjectionAdapter>();
        services.AddScoped<ProjectStructureProjectionMaintenanceService>();
        services.AddScoped<IProjectStructureProjectionContributor, ProjectHierarchyProjectionContributor>();
        services.AddScoped<IProjectStructureProjectionContributor, ProjectResourceProjectionContributor>();
        services.AddScoped<IProjectStructureProjectionContributor, PromptGalleryProjectionContributor>();
        services.AddScoped<IProjectStructureProjectionContributor, TestPlanProjectionContributor>();
        services.AddScoped<ProjectStructureProcessRunRecordProjector>();
        services.AddScoped<IProjectStructureProjectionContributor, ProjectStructureProcessProjectionContributor>();
        services.AddScoped<ProjectAssetStorageService>();
        services.AddScoped(serviceProvider =>
            ActivatorUtilities.CreateInstance<ProjectWorkbenchService>(
                serviceProvider,
                serviceProvider.GetRequiredService<ProjectAssetStorageService>()));
        services.AddScoped<IProjectNodeAssignmentPolicyBridge, ProjectNodeAssignmentPolicyBridge>();
        services.AddScoped<IProjectNodeScopeBridge, ProjectNodeScopeBridge>();
        services.Replace(ServiceDescriptor.Scoped<IProjectNodeDetailsBridge, ProjectNodeDetailsBridge>());
        services.AddScoped<ProjectStructureLeaseService>();
        services.AddScoped<ProjectStructureAnalyticsService>();
        services.AddSingleton<ProjectPlanSummaryCalculator>();
        services.AddScoped<ProjectPlanAnalyticsQueryService>();
        services.AddScoped<ProjectManagerSummaryScopeResolver>();
        services.AddScoped<ProjectManagerSummaryQueryService>();
        services.AddScoped<ProjectManagerSummaryStateStore>();
        services.AddScoped<ProjectStructureAgentAuthorizationService>();
        services.AddScoped<ProjectStructureAgentProjectCreationCoordinator>();
        services.AddScoped<ProjectStructureChecklistService>();
        services.AddScoped<ProjectStructureImportService>();
        services.AddScoped<ProjectStructureWorkItemAssigneeService>();
        services.AddScoped<ProjectStructureTaskResourceService>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<
            IProjectStructureTaskResourceCostStrategy,
            ProjectStructurePersonTaskResourceCostStrategy>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<
            IProjectStructureTaskResourceCostStrategy,
            ProjectStructureWorkflowTaskResourceCostStrategy>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<
            IProjectStructureTaskResourceCostStrategy,
            ProjectStructureProcessTaskResourceCostStrategy>());
        services.AddScoped<ProjectStructureTaskResourceCostService>();
        services.AddScoped<ProjectStructureTaskEstimateRefreshService>();
        services.AddScoped<
            ProjectStructureWorkItemAssignmentRevisionService>();
        services.Replace(ServiceDescriptor.Scoped<
            IProjectWorkItemAssignmentMutationBridge>(
            serviceProvider => serviceProvider.GetRequiredService<
                ProjectStructureWorkItemAssignmentRevisionService>()));
        services.AddScoped<ProjectStructureTaskEditCompensationService>();
        services.AddScoped<ProjectStructureTaskApplicationService>();
        services.AddScoped<ProjectStructureTaskPricingCommitService>();
        services.AddScoped<ProjectStructureTaskPricingPersistenceService>();
        services.AddScoped<ProjectStructureTaskResourceAttachmentService>();
        services.AddScoped<ProjectStructureGanttRowOrderService>();
        services.AddScoped<ProjectStructureTaskCreationService>();
        services.AddScoped<ProjectStructureTaskDetailsService>();
        services.AddScoped<ProjectStructureGanttTaskEditCoordinator>();
        services.AddScoped<ProjectStructureCanvasTaskDialogCoordinator>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<
            IProjectAssetContentGenerator,
            ProjectTextAssetContentGenerator>());
        services.AddScoped<ProjectAssetContentGeneratorResolver>();
        services.AddScoped<ProjectAssetCreationService>();
        services.AddScoped<ProjectStructureTextAssetCreationCoordinator>();
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
        services.TryAddSingleton<ProjectStructureWorkflowLaunchIntentFactory>();
        services.AddScoped<ProjectStructureWorkflowNodeService>();
        services.TryAddScoped<IWorkspacePathResolutionService>(serviceProvider =>
        {
            var workspaceRoot = serviceProvider.GetRequiredService<IWorkspacePathResolver>().ResolveWorkspaceRoot();
            var profile = serviceProvider.GetRequiredService<IDatabaseProfileRuntimeAccessor>().ResolveCurrentProfile();
            var scope = WorkspaceScopeDescriptor.Organization(profile.Profile.Id.ToString("N"));
            return new WorkspacePathResolutionService(
                workspaceRoot,
                serviceProvider.GetRequiredService<IPhysicalFileSystemPathPolicyFactory>(),
                scope,
                serviceProvider.GetRequiredService<IExternalTargetPathRegistry>());
        });
        services.AddScoped<ProjectStructureSourceWorkspacePathResolver>();
        services.AddScoped<ProjectStructureSubprojectTransferCoordinator>();
        services.AddScoped<ProjectStructureBatchDeletionCoordinator>();
        services.AddScoped<ProjectStructureAssetContentReader>();
        services.AddScoped<ProjectStructureAgentService>();
        services.AddScoped<ProjectStructureAgentNodeCopyCoordinator>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAgentRuntimeToolProvider, ProjectStructureAgentRuntimeToolProvider>());
        services.AddScoped<IProjectStructureRuntimeGateway, WorkbenchProjectStructureRuntimeGateway>();
        services.AddScoped<IProjectStructureSourceSnapshotProvider, WorkbenchProjectStructureSourceSnapshotProvider>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IMemorySourceGatewayAdapter, WorkbenchProjectStructureMemorySourceGatewayAdapter>());
        services.AddScoped<ProjectMemoryIngestionService>();
        services.AddScoped<IProjectStructureLocalFileOpener, ProjectStructureLocalFileOpener>();
        services.AddScoped<IProjectStructureDotNetProjectTargetResolver, ProjectStructureDotNetProjectTargetResolver>();
        services.AddScoped<IProjectStructureRuntimeLauncher, ProjectStructureRuntimeLauncher>();
        services.AddScoped<ProjectStructureRuntimeNodeMetadataBoundary>();
        services.AddScoped<IProjectStructureNodeFileScopeProvider, ProjectStructureFileScopeResolver>();
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IFileToolsStorageBindingSource, ProjectStructureFileScopeResolver>());
        services.AddScoped<ProjectStructureKnownFileInteractionCoordinator>();
        services.AddScoped<IProjectStructureCurrentNodeResolver, ProjectStructureCurrentNodeResolver>();
        services.AddScoped<ProjectStructureLocalFileActionCoordinator>();
        services.AddScoped<ProjectStructureFileActionCoordinator>();
        services.AddScoped<IProjectWorkbenchSeedService>(serviceProvider => serviceProvider.GetRequiredService<ProjectWorkbenchService>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<
            IProjectTransferTargetStateParticipant,
            WorkbenchProjectTransferTargetStateParticipant>());
        services.AddScoped<ProjectTransferTargetStateGuard>();
        services.AddScoped<IDatabaseTransferHandler, ProjectsDatabaseTransferHandler>();
        services.AddScoped<IProjectPackageService, ProjectPackageService>();
        return services;
    }
}

public static class WorkbenchModuleAssemblyMarker;
