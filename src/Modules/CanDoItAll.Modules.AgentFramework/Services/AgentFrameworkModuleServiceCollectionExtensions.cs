using CanDoItAll.Memory.SourceGateway;
using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Capabilities.Access;
using CanDoItAll.AgentFramework.Components;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.AgentFramework.Memory.DependencyInjection;
using CanDoItAll.AgentFramework.Mcp;
using CanDoItAll.AgentFramework.Mcp.Abstractions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.AgentFramework.Providers;
using CanDoItAll.AgentFramework.Runtime.Abstractions;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.AgentFramework.Tools;
using CanDoItAll.AgentFramework.Tools.Abstractions;
using CanDoItAll.AgentFramework.Voice;
using CanDoItAll.AgentFramework.Usage;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Git;
using CanDoItAll.Modules.AgentFramework.Hosting;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.Conversations.Shell;
using CanDoItAll.SharedKernel;
using CanDoItAll.SharedKernel.Streaming;
using CanDoItAll.Tools.Documents;
using CanDoItAll.AgentFramework.Workflows.Templates;
using CanDoItAll.Memory.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Modules.AgentFramework;

public static class AgentFrameworkModuleServiceCollectionExtensions
{
    public static IServiceCollection AddAgentFrameworkModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddConversationShell();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<
            IProjectTransferTargetStateParticipant,
            AgentFrameworkProjectTransferTargetStateParticipant>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<
            IProjectDeletionParticipant,
            AgentProjectStructureAccessDeletionParticipant>());
        ArgumentNullException.ThrowIfNull(configuration);
        var backgroundWorkersEnabled = LocalRuntimeHostedWorkerPolicy.AreBackgroundHostedWorkersEnabled(
            configuration[LocalRuntimeHostedWorkerPolicy.LaneKindConfigurationKey],
            configuration["LaneKind"]);

        services.AddOptions<ProcessMockAgentOptions>()
            .Bind(configuration.GetSection(ProcessMockAgentOptions.SectionName));
        services.AddOptions<WorkflowExampleCatalogSeedOptions>()
            .Bind(configuration.GetSection(WorkflowExampleCatalogSeedOptions.SectionName));
        services.AddAgentFrameworkVoice();
        services.AddAgentFrameworkMemory();
        services.AddSingleton<IProviderProfileService, ProviderProfileService>();
        services.AddSingleton<ICapabilityProofService, CapabilityProofService>();
        services.AddSingleton<IAgentProviderCredentialResolver, SecretStoreAgentProviderCredentialResolver>();
        services.AddMafProviderRuntimeServices();
        services.AddDataProtection();
        services.TryAddSingleton<IExternalTargetPathRegistryFactory, ExternalTargetPathRegistryFactory>();
        services.TryAddScoped<IExternalTargetPathRegistry, ExternalTargetPathRegistry>();
        services.TryAddSingleton<IPhysicalFileSystemPathPolicyFactory, PhysicalFileSystemPathPolicyFactory>();
        services.TryAddScoped<IExternalProcessRunner, WorkspaceExternalProcessRunner>();
        services.TryAddScoped<IGitCommandExecutor, WorkspaceGitCommandExecutor>();
        services.TryAddScoped<IExternalProcessToolInvoker, ExternalProcessToolInvoker>();
        services.TryAddScoped<IExternalHttpToolInvoker, ExternalHttpToolInvoker>();
        services.TryAddScoped<IToolSetupTestService, ToolSetupTestService>();
        services.TryAddScoped<IMcpClientFactory, LocalStdioMcpClientFactory>();
        services.TryAddScoped<IMcpSetupTestService, McpSetupTestService>();
        services.TryAddScoped<ICapabilityAccessPolicyEvaluator, CapabilityAccessPolicyEvaluator>();
        services.TryAddScoped<IAgentCapabilitySetupFlowService, AgentCapabilitySetupFlowService>();
        services.AddScoped<ISandboxWorkspaceStore>(serviceProvider =>
        {
            var (workspaceRoot, scope) = ResolveCurrentWorkspaceScope(serviceProvider);
            return new FileSandboxWorkspaceStore(workspaceRoot, scope);
        });
        services.TryAddScoped<IAgentUsageTotalsQueryService, AgentUsageTotalsQueryService>();
        services.TryAddScoped<IWorkspaceFileService>(serviceProvider =>
        {
            var (workspaceRoot, scope) = ResolveCurrentWorkspaceScope(serviceProvider);
            return new WorkspaceFileService(
                workspaceRoot,
                serviceProvider.GetRequiredService<IPhysicalFileSystemPathPolicyFactory>(),
                scope,
                serviceProvider.GetRequiredService<IExternalTargetPathRegistry>());
        });
        services.TryAddScoped<WorkspaceFileInspectionScopeFactory>(serviceProvider =>
        {
            var (workspaceRoot, scope) = ResolveCurrentWorkspaceScope(serviceProvider);
            return new WorkspaceFileInspectionScopeFactory(
                workspaceRoot,
                scope,
                serviceProvider.GetRequiredService<IPhysicalFileSystemPathPolicyFactory>(),
                serviceProvider.GetRequiredService<IExternalTargetPathRegistryFactory>());
        });
        services.TryAddScoped<IPluginWorkspaceFiles, PluginWorkspaceFiles>();
        services.TryAddScoped<IWorkspacePathResolutionService>(serviceProvider =>
        {
            var (workspaceRoot, scope) = ResolveCurrentWorkspaceScope(serviceProvider);
            return new WorkspacePathResolutionService(
                workspaceRoot,
                serviceProvider.GetRequiredService<IPhysicalFileSystemPathPolicyFactory>(),
                scope,
                serviceProvider.GetRequiredService<IExternalTargetPathRegistry>());
        });
        services.TryAddScoped<IWorkspaceProcessHost, LocalWorkspaceProcessHost>();
        services.TryAddScoped<IWorkspaceLongRunningProcessHost>(serviceProvider =>
            serviceProvider.GetRequiredService<IWorkspaceProcessHost>() as IWorkspaceLongRunningProcessHost
            ?? throw new InvalidOperationException(
                "The configured workspace process host does not implement managed process sessions."));
        services.TryAddScoped<IWorkspaceCommandExecutionService>(serviceProvider =>
        {
            var (workspaceRoot, scope) = ResolveCurrentWorkspaceScope(serviceProvider);
            return new WorkspaceCommandExecutionService(
                workspaceRoot,
                serviceProvider.GetRequiredService<IWorkspaceProcessHost>(),
                serviceProvider.GetRequiredService<IPhysicalFileSystemPathPolicyFactory>(),
                scope,
                serviceProvider.GetServices<IWorkspaceCommandReceiptLifecycleFactExtractor>(),
                serviceProvider.GetRequiredService<IExternalTargetPathRegistry>());
        });
        services.TryAddScoped<IWorkspaceExecutionRunProcessLeaseCleanupScopeFactory>(serviceProvider =>
            new WorkspaceExecutionRunProcessLeaseCleanupScopeFactory(
                serviceProvider.GetServices<IWorkspaceCommandReceiptLifecycleFactExtractor>().ToList(),
                serviceProvider.GetRequiredService<IPhysicalFileSystemPathPolicyFactory>(),
                serviceProvider.GetRequiredService<IExternalTargetPathRegistryFactory>()));
        services.TryAddScoped<IWorkspaceExecutionRunProcessLeaseCleaner>(serviceProvider =>
        {
            var (workspaceRoot, scope) = ResolveCurrentWorkspaceScope(serviceProvider);
            IPhysicalFileSystemPathPolicyFactory pathPolicyFactory =
                serviceProvider.GetRequiredService<IPhysicalFileSystemPathPolicyFactory>();
            return new WorkspaceExecutionRunProcessLeaseCleaner(
                serviceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>(),
                new WorkspaceExecutionScope(
                    workspaceRoot,
                    scope,
                    rootCaseSensitivity: pathPolicyFactory.Create(workspaceRoot).CaseSensitivity),
                serviceProvider.GetRequiredService<IWorkspaceExecutionRunProcessLeaseCleanupScopeFactory>());
        });
        services.TryAddScoped<IWorkspaceDocumentMarkdownConverter, ManagedCodeMarkItDownDocumentMarkdownConverter>();
        services.TryAddScoped<IWorkspaceImageOperationService>(serviceProvider =>
        {
            var (workspaceRoot, scope) = ResolveCurrentWorkspaceScope(serviceProvider);
            return new WorkspaceImageOperationService(
                workspaceRoot,
                serviceProvider.GetRequiredService<IPhysicalFileSystemPathPolicyFactory>(),
                scope,
                serviceProvider.GetRequiredService<IExternalTargetPathRegistry>());
        });
        services.TryAddScoped<IWorkspaceArtifactToolService>(serviceProvider =>
        {
            var (workspaceRoot, scope) = ResolveCurrentWorkspaceScope(serviceProvider);
            return new WorkspaceArtifactToolService(
                workspaceRoot,
                serviceProvider.GetRequiredService<IWorkspaceCommandExecutionService>(),
                serviceProvider.GetRequiredService<IWorkspaceDocumentMarkdownConverter>(),
                serviceProvider.GetRequiredService<IPhysicalFileSystemPathPolicyFactory>(),
                scope,
                serviceProvider.GetRequiredService<IWorkspaceImageOperationService>(),
                serviceProvider.GetRequiredService<IExternalTargetPathRegistry>());
        });
        services.TryAddScoped<IWorkspaceRuntimeServicesFactory>(serviceProvider => new WorkspaceRuntimeServicesFactory(
            serviceProvider.GetServices<IWorkspaceCommandReceiptLifecycleFactExtractor>().ToList(),
            serviceProvider.GetService<IWorkspaceDocumentMarkdownConverter>() ?? new ManagedCodeMarkItDownDocumentMarkdownConverter(),
            serviceProvider.GetRequiredService<IPhysicalFileSystemPathPolicyFactory>(),
            serviceProvider.GetRequiredService<IExternalTargetPathRegistryFactory>()));
        services.TryAddScoped<MafAgentRuntime>(serviceProvider =>
        {
            var (workspaceRoot, scope) = ResolveCurrentWorkspaceScope(serviceProvider);
            return new MafAgentRuntime(workspaceRoot, serviceProvider, scope);
        });
        // SB18: the four narrow runtime ports resolve directly to the native MAF adapters exposed
        // by the same MafAgentRuntime composition (one adapter set per runtime scope). No broad
        // runtime interface or compatibility facade is constructed by production registrations.
        services.TryAddScoped<IAgentExecutionRuntime>(serviceProvider =>
            serviceProvider.GetRequiredService<MafAgentRuntime>().ExecutionPort);
        services.TryAddScoped<IAgentContinuationRuntime>(serviceProvider =>
            serviceProvider.GetRequiredService<MafAgentRuntime>().ContinuationPort);
        services.TryAddScoped<IProviderDiagnosticsRuntime>(serviceProvider =>
            serviceProvider.GetRequiredService<MafAgentRuntime>().DiagnosticsPort);
        services.TryAddScoped<IProviderModelAdministrationRuntime>(serviceProvider =>
            serviceProvider.GetRequiredService<MafAgentRuntime>().ModelAdministrationPort);
        services.TryAddScoped<ISandboxWorkspaceExecutionRunStore>(serviceProvider =>
            (ISandboxWorkspaceExecutionRunStore)serviceProvider.GetRequiredService<ISandboxWorkspaceStore>());
        services.TryAddScoped<ISandboxWorkspaceExecutionStore>(serviceProvider =>
            serviceProvider.GetRequiredService<ISandboxWorkspaceStore>());
        services.TryAddScoped<ISandboxWorkspaceCatalogStore>(serviceProvider =>
            serviceProvider.GetRequiredService<ISandboxWorkspaceStore>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IProviderUsageProjectionSource,
            AgentProviderUsageProjectionSource>());
        services.TryAddScoped<ProviderUsageQueryService>();
        services.TryAddScoped<IAgentRecruitingEvidenceStore>(serviceProvider =>
            (IAgentRecruitingEvidenceStore)serviceProvider.GetRequiredService<ISandboxWorkspaceStore>());
        services.TryAddScoped<IAgentRecruitingEvidenceService, AgentRecruitingEvidenceService>();
        services.TryAddSingleton<IAgentExecutionCancellationRegistry, AgentExecutionCancellationRegistry>();
        services.AddScoped<WorkspaceAgentProviderProfileMapper>();
        services.AddScoped<
            IProviderRuntimeProfileSnapshotLoader,
            DatabaseProviderRuntimeProfileSnapshotLoader>();
        services.AddSingleton<
            CanonicalProviderRuntimeProfileSnapshotService>();
        services.AddSingleton<IProviderRuntimeProfileSource>(
            serviceProvider => serviceProvider.GetRequiredService<
                CanonicalProviderRuntimeProfileSnapshotService>());
        services.AddSingleton<IProviderRuntimeProfileSnapshotSource>(
            serviceProvider => serviceProvider.GetRequiredService<
                CanonicalProviderRuntimeProfileSnapshotService>());
        services.AddSingleton<IProviderRuntimeProfileSnapshotInitializer>(
            serviceProvider => serviceProvider.GetRequiredService<
                CanonicalProviderRuntimeProfileSnapshotService>());
        services.AddSingleton<IProviderRuntimeProfileSnapshotUpdater>(
            serviceProvider => serviceProvider.GetRequiredService<
                CanonicalProviderRuntimeProfileSnapshotService>());
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<
                IWorkspaceProviderProfileCommitObserver,
                AgentFrameworkProviderRuntimeSnapshotCommitObserver>());
        services.AddScoped<WorkspaceBackedAgentProviderProfileRegistry>();
        services.AddScoped<IProviderProfileRegistry>(serviceProvider =>
            serviceProvider.GetRequiredService<WorkspaceBackedAgentProviderProfileRegistry>());
        services.TryAddSingleton<AgentReferenceDataInvalidationHub>();
        services.TryAddSingleton<IAgentReferenceDataCacheInvalidator>(serviceProvider =>
            serviceProvider.GetRequiredService<AgentReferenceDataInvalidationHub>());
        services.TryAddScoped<AgentReferenceDataCache>();
        services.TryAddScoped<IAgentReferenceDataProvider, WorkspaceBackedAgentReferenceDataProvider>();
        services.TryAddSingleton(AgentExecutionPreparationCachePolicy.Default);
        services.TryAddScoped<AgentExecutionPreparationCache>();
        services.TryAddScoped<IAgentExecutionPreparationCache>(serviceProvider =>
            serviceProvider.GetRequiredService<AgentExecutionPreparationCache>());
        services.TryAddScoped<
            IAgentExecutionProfileGenerationSource,
            DatabaseRuntimeAgentExecutionProfileGenerationSource>();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<
            PartitionedSequencedStream<AgentExecutionActivityStreamId, AgentExecutionActivity>>(
            serviceProvider => new PartitionedSequencedStream<
                AgentExecutionActivityStreamId,
                AgentExecutionActivity>(
                PartitionedSequencedStreamPolicy.Default,
                serviceProvider.GetRequiredService<TimeProvider>()));
        services.TryAddSingleton<AgentExecutionActivityCoordinator>();
        services.TryAddSingleton<IAgentExecutionActivityCoordinator>(serviceProvider =>
            serviceProvider.GetRequiredService<AgentExecutionActivityCoordinator>());
        services.AddScoped<CurrentProfileAgentExecutionActivityReader>();
        services.AddScoped<IAgentExecutionActivityReader>(serviceProvider =>
            serviceProvider.GetRequiredService<CurrentProfileAgentExecutionActivityReader>());
        services.AddScoped<ICurrentProfileAgentExecutionActivityReader>(serviceProvider =>
            serviceProvider.GetRequiredService<CurrentProfileAgentExecutionActivityReader>());
        services.AddScoped<AgentChatContextRegistry>();
        services.AddScoped<IAgentChatContextRegistry>(serviceProvider =>
            serviceProvider.GetRequiredService<AgentChatContextRegistry>());
        services.AddScoped<AgentChatExecutionNotificationHub>();
        services.AddScoped<IAgentChatExecutionNotificationHub>(serviceProvider =>
            serviceProvider.GetRequiredService<AgentChatExecutionNotificationHub>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<
            IAgentExecutionSourceAuthorityProvider,
            AgentFrameworkAgentsExecutionAuthorityProvider>());
        services.AddScoped<IAgentExecutionAuthorityResolver, CanonicalAgentExecutionAuthorityResolver>();
        services.AddScoped<IAgentConversationContextService, AgentConversationContextService>();
        services.AddScoped<IAgentTurnContextCaptureService, AgentTurnContextCaptureService>();
        services.AddScoped<IAgentChatExecutionOrchestrator, AgentChatExecutionOrchestrator>();
        services.AddScoped<ActiveAgentChatRegistry>();
        services.AddScoped<IActiveAgentChatRegistry>(serviceProvider =>
            serviceProvider.GetRequiredService<ActiveAgentChatRegistry>());
        services.AddScoped<AgentChatPreparationPool>();
        services.AddScoped<IAgentChatPreparationPool>(serviceProvider =>
            serviceProvider.GetRequiredService<AgentChatPreparationPool>());
        services.AddScoped<IFloatingAgentChatSettingsService, FloatingAgentChatSettingsService>();
        services.AddScoped<FloatingAgentChatCoordinator>();
        services.AddScoped<IFloatingAgentChatCoordinator>(serviceProvider =>
            serviceProvider.GetRequiredService<FloatingAgentChatCoordinator>());
        services.AddScoped<AgentChatLauncherCompatibilityFacade>();
        services.AddScoped<IAgentChatLauncher>(serviceProvider =>
            serviceProvider.GetRequiredService<AgentChatLauncherCompatibilityFacade>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<
            IConversationShellContributor,
            AgentConversationShellContributor>());
        services.Replace(
            ServiceDescriptor.Scoped<IPromptGalleryCuratorLauncher, AgentFrameworkPromptGalleryCuratorLauncher>());
        services.AddScoped<ICanDoItAllAgentWorkspaceFactory, CanDoItAllAgentWorkspaceFactory>();
        services.AddScoped<CanDoItAllAgentWorkspaceFactory>(serviceProvider =>
            (CanDoItAllAgentWorkspaceFactory)serviceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>());
        services.AddScoped<CurrentProfileAgentFrameworkWorkspaceService>();
        services.AddScoped<IAgentFrameworkWorkspaceService>(serviceProvider =>
            serviceProvider.GetRequiredService<CurrentProfileAgentFrameworkWorkspaceService>());
        services.AddScoped<IAgentExecutionReportReader>(serviceProvider =>
            serviceProvider.GetRequiredService<CurrentProfileAgentFrameworkWorkspaceService>());
        services.AddScoped<IAgentFrameworkWorkspaceActivityExecutionService>(serviceProvider =>
            serviceProvider.GetRequiredService<CurrentProfileAgentFrameworkWorkspaceService>());
        services.AddScoped<IAgentChatAttachmentStagingService, AgentChatAttachmentStagingService>();
        services.AddScoped<IAgentFrameworkOrganizationCatalogRepairService, AgentFrameworkOrganizationCatalogRepairService>();
        services.AddScoped<AgentFrameworkCatalogWarmupService>();
        services.TryAddScoped<AgentAvatarGenerationService>();
        services.TryAddScoped<IAvatarGenerationGateway, AgentAvatarGenerationGateway>();
        services.TryAddScoped<HrAgentAdministrationService>();
        services.TryAddScoped<HrAgentAvatarGenerationService>();
        services.TryAddScoped<HrAgentUsageAnalyticsService>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<
            IProjectStructureTaskResourceCostStrategy,
            ProjectStructureAgentTaskResourceCostStrategy>());
        services.TryAddScoped<HrAgentProcessReviewService>();
        services.TryAddScoped<HrAgentRuntimeAuthorizationService>();
        services.TryAddScoped<PromptsCuratorAgentRuntimeAuthorizationService>();
        services.TryAddScoped<WorkflowCuratorAgentRuntimeAuthorizationService>();
        services.TryAddScoped<WorkflowAgentRuntimeAuthorizationService>();
        services.TryAddScoped<CapabilityCuratorAgentRuntimeAuthorizationService>();
        services.TryAddSingleton<CapabilityCuratorSetupAttestationStore>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAgentRuntimeToolProvider, ImageGenerationAgentRuntimeToolProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAgentRuntimeToolProvider, WorkflowAgentRuntimeToolProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAgentRuntimeToolProvider, PromptGalleryAgentRuntimeToolProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAgentRuntimeToolProvider, PromptsCuratorAgentRuntimeToolProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAgentRuntimeToolProvider, WorkflowCuratorAgentRuntimeToolProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAgentRuntimeToolProvider, CapabilityCuratorAgentRuntimeToolProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAgentRuntimeToolProvider, HrAgentRuntimeToolProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISettingsRendererSource, WorkflowSettingsRendererSource>());
        services.AddWorkflowTemplateServices();
        services.AddScoped<WorkflowExampleCatalogSeedService>();
        services.AddScoped<WorkflowPromptGalleryMigrationService>();
        services.AddHostedService<WorkflowPromptGalleryMigrationHostedService>();
        services.AddScoped<ProcessMockAgentCatalogService>();
        services.AddScoped<AgentFrameworkExecutionRecoveryService>();
        services.AddScoped<ScenarioHarnessService>();
        services.AddScoped<IDatabaseTransferHandler, AiAgentsDatabaseTransferHandler>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IShellNavigationContributor, AgentFrameworkShellNavigationContributor>());
        services.AddScoped<IProviderRuntimeGateway, AgentFrameworkProviderRuntimeGateway>();
        services.AddScoped<IAiTechnicalAgentBridge, AgentFrameworkAiTechnicalAgentBridge>();
        services.TryAddScoped<IPluginStorageGateway, PluginStorageGateway>();
        services.TryAddScoped<IProjectStructureRuntimeGateway, UnavailableProjectStructureRuntimeGateway>();
        services.TryAddScoped<ISpreadsheetDocumentService, ClosedXmlSpreadsheetDocumentService>();
        services.AddMafWorkflowAdapterServices(ServiceLifetime.Scoped);
        services.TryAddScoped<PersistentWorkflowCatalogService>();
        services.TryAddScoped<IWorkflowCatalogService>(serviceProvider => serviceProvider.GetRequiredService<PersistentWorkflowCatalogService>());
        services.TryAddScoped<IWorkflowCatalogSearchService>(serviceProvider => serviceProvider.GetRequiredService<PersistentWorkflowCatalogService>());
        services.TryAddScoped<IWorkflowCatalogLookupService>(serviceProvider => serviceProvider.GetRequiredService<PersistentWorkflowCatalogService>());
        services.TryAddScoped<IWorkflowComponentLibraryService>(serviceProvider => serviceProvider.GetRequiredService<PersistentWorkflowCatalogService>());
        services.TryAddScoped<IWorkflowSettingsService>(serviceProvider => serviceProvider.GetRequiredService<PersistentWorkflowCatalogService>());
        services.TryAddScoped<PersistentWorkflowRunStore>();
        services.TryAddScoped<IWorkflowRunStore>(serviceProvider => serviceProvider.GetRequiredService<PersistentWorkflowRunStore>());
        services.TryAddScoped<IWorkflowOverviewStore>(serviceProvider => serviceProvider.GetRequiredService<PersistentWorkflowRunStore>());
        services.TryAddScoped<IWorkflowDashboardActivityStore>(serviceProvider => serviceProvider.GetRequiredService<PersistentWorkflowRunStore>());
        services.TryAddScoped<IWorkflowProjectStructureReportStore>(
            serviceProvider => serviceProvider.GetRequiredService<PersistentWorkflowRunStore>());
        services.TryAddScoped<IWorkflowArtifactStore>(serviceProvider => serviceProvider.GetRequiredService<PersistentWorkflowRunStore>());
        services.TryAddScoped<IWorkflowExternalRequestStore>(serviceProvider => serviceProvider.GetRequiredService<PersistentWorkflowRunStore>());
        services.TryAddScoped<IWorkflowCheckpointStore>(serviceProvider => serviceProvider.GetRequiredService<PersistentWorkflowRunStore>());
        services.TryAddScoped<PersistentWorkflowLaunchIdempotencyStore>();
        services.Replace(ServiceDescriptor.Scoped<IWorkflowLaunchIdempotencyStore>(serviceProvider =>
            serviceProvider.GetRequiredService<PersistentWorkflowLaunchIdempotencyStore>()));
        services.Replace(ServiceDescriptor.Scoped<IWorkflowLaunchIdempotencyQueryStore>(serviceProvider =>
            serviceProvider.GetRequiredService<PersistentWorkflowLaunchIdempotencyStore>()));
        services.TryAddScoped<PersistentWorkflowUsageObservationStore>();
        services.Replace(ServiceDescriptor.Scoped<IWorkflowUsageObservationStore>(serviceProvider =>
            serviceProvider.GetRequiredService<PersistentWorkflowUsageObservationStore>()));
        services.Replace(ServiceDescriptor.Scoped<IWorkflowUsageAnalyticsStore>(serviceProvider =>
            serviceProvider.GetRequiredService<PersistentWorkflowUsageObservationStore>()));
        services.AddFileWorkflowArtifactContentStore(ResolveCurrentWorkspaceScope);
        services.TryAddScoped<IWorkflowRuntimeEvidenceSourceProvider, WorkflowRuntimeEvidenceSourceProvider>();
        services.TryAddScoped<IProcessRuntimeEvidenceSourceProvider, UnavailableProcessRuntimeEvidenceSourceProvider>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IMemorySourceGatewayAdapter, WorkflowRuntimeMemorySourceGatewayAdapter>());

        if (backgroundWorkersEnabled)
        {
            services.AddHostedService<AgentFrameworkCatalogWarmupWorker>();
            services.AddHostedService<AgentFrameworkExecutionRecoveryWorker>();
        }

        return services;
    }

    private static (string WorkspaceRoot, WorkspaceScopeDescriptor Scope) ResolveCurrentWorkspaceScope(IServiceProvider serviceProvider)
    {
        var workspaceRoot = serviceProvider.GetRequiredService<IWorkspacePathResolver>().ResolveWorkspaceRoot();
        var profile = serviceProvider.GetRequiredService<IDatabaseProfileRuntimeAccessor>().ResolveCurrentProfile();
        return (workspaceRoot, WorkspaceScopeDescriptor.Organization(profile.Profile.Id.ToString("N")));
    }
}

public static class AgentFrameworkModuleAssemblyMarker;
