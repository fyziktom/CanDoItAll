using CanDoItAll.Memory.SourceGateway;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Core.Execution;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Memory.Application;
using CanDoItAll.Modules.Processes.AgentChat;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Processes.Templates;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.DataProtection;

namespace CanDoItAll.Modules.Processes;

public static class ProcessesModuleServiceCollectionExtensions
{
    public static IServiceCollection AddProcessesModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddDataProtection();
        services.TryAddSingleton<IExternalTargetPathRegistryFactory, ExternalTargetPathRegistryFactory>();
        services.TryAddScoped<IExternalTargetPathRegistry, ExternalTargetPathRegistry>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<
            IAgentExecutionSourceAuthorityProvider,
            ProcessesExecutionAuthorityProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<
            IAgentExecutionSourceAuthorityProvider,
            LiveProcessesExecutionAuthorityProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<
            IProjectTransferTargetStateParticipant,
            ProcessesProjectTransferTargetStateParticipant>());
        var backgroundWorkersEnabled = LocalRuntimeHostedWorkerPolicy.AreBackgroundHostedWorkersEnabled(
            configuration[LocalRuntimeHostedWorkerPolicy.LaneKindConfigurationKey],
            configuration["LaneKind"]);

        services.AddDbContext<ProcessPersistenceDbContext>(ConfigureProcessPersistenceDbContext);
        services.AddDbContextFactory<ProcessPersistenceDbContext>(
            ConfigureProcessPersistenceDbContext,
            ServiceLifetime.Scoped);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<IWorkspaceExecutionRunProcessLeaseCleaner>(serviceProvider =>
        {
            var executionRunStore =
                serviceProvider.GetService<ISandboxWorkspaceExecutionRunStore>();
            var cleanupScopeFactory = serviceProvider
                .GetService<IWorkspaceExecutionRunProcessLeaseCleanupScopeFactory>();
            var workspacePathResolver = serviceProvider
                .GetService<IWorkspacePathResolver>();
            var databaseProfileRuntimeAccessor = serviceProvider
                .GetService<IDatabaseProfileRuntimeAccessor>();
            var physicalPathPolicyFactory = serviceProvider
                .GetService<IPhysicalFileSystemPathPolicyFactory>();
            return executionRunStore is not null &&
                   cleanupScopeFactory is not null &&
                   workspacePathResolver is not null &&
                   databaseProfileRuntimeAccessor is not null &&
                   physicalPathPolicyFactory is not null
                ? new WorkspaceExecutionRunProcessLeaseCleaner(
                    executionRunStore,
                    new WorkspaceExecutionScope(
                        workspacePathResolver.ResolveWorkspaceRoot(),
                        WorkspaceScopeDescriptor.Organization(
                            databaseProfileRuntimeAccessor
                                .ResolveCurrentProfile()
                                .Profile.Id
                                .ToString("N")),
                        rootCaseSensitivity: physicalPathPolicyFactory
                            .Create(workspacePathResolver.ResolveWorkspaceRoot())
                            .CaseSensitivity),
                    cleanupScopeFactory)
                : UnavailableWorkspaceExecutionRunProcessLeaseCleaner.Instance;
        });
        services.TryAddSingleton<IProcessProjectionClock, SystemProcessProjectionClock>();
        services.TryAddSingleton(ProcessProjectionJsonCodec.Default);
        services.TryAddSingleton<ProcessTemplatePackLoader>();
        services.TryAddScoped<EfProcessRuntimeUnitOfWork>();
        services.TryAddScoped<IProcessRuntimeUnitOfWork>(serviceProvider => serviceProvider.GetRequiredService<EfProcessRuntimeUnitOfWork>());
        services.TryAddScoped<IProcessRuntimeStateStore>(serviceProvider => serviceProvider.GetRequiredService<EfProcessRuntimeUnitOfWork>());
        services.TryAddScoped<IProcessRuntimeActivityStore>(serviceProvider => serviceProvider.GetRequiredService<EfProcessRuntimeUnitOfWork>());
        services.TryAddScoped<IProcessRuntimeRunHierarchyStore>(serviceProvider => serviceProvider.GetRequiredService<EfProcessRuntimeUnitOfWork>());
        services.TryAddScoped<IProcessIdempotencyStore>(serviceProvider => serviceProvider.GetRequiredService<EfProcessRuntimeUnitOfWork>());
        services.TryAddScoped<EfProcessRuntimeEventStore>();
        services.TryAddScoped<IProcessRuntimeEventStore>(serviceProvider => serviceProvider.GetRequiredService<EfProcessRuntimeEventStore>());
        services.TryAddScoped<IProcessRuntimeEventReplayStore>(serviceProvider => serviceProvider.GetRequiredService<EfProcessRuntimeEventStore>());
        services.TryAddScoped<IProcessOutboxWriter, EfProcessOutboxStore>();
        services.TryAddScoped<IProcessArtifactLedgerStore, EfProcessArtifactLedgerStore>();
        services.TryAddScoped<IProcessProjectionStore, EfProcessProjectionStore>();
        services.TryAddScoped<IProcessRunRecordStore, EfProcessRunRecordStore>();
        services.TryAddScoped<IProcessRunRecordReader, ProcessRunRecordReader>();
        services.TryAddScoped<IProcessRunRecordBackfillSource, EfProcessRunRecordBackfillSource>();
        services.TryAddScoped<IProcessInstancePlanStore, EfProcessInstancePlanStore>();
        services.TryAddScoped<IProcessRuntimeStepAssignmentStore, EfProcessRuntimeStepAssignmentStore>();
        services.TryAddScoped<IProcessRunFileScopeProvider, ProcessRunFileScopeProvider>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IFileToolsStorageBindingSource, ProcessRunFileScopeProvider>());
        services.TryAddScoped<ProcessRunFilesCoordinator>();
        services.TryAddScoped<IProcessHistoricalRunCostReader, EfProcessHistoricalRunCostReader>();
        services.TryAddScoped<IProcessRuntimeProjector, ProcessRuntimeProjectionProjector>();
        services.TryAddScoped<ProcessRuntimeProjectionCatchupService>();
        services.TryAddScoped<ProcessRuntimeBranchSignalApplicationService>();
        services.TryAddScoped<IProcessRuntimeBranchSignalRouter>(serviceProvider => serviceProvider.GetRequiredService<ProcessRuntimeBranchSignalApplicationService>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IProcessPromptCompositionDriver, AgentFrameworkProcessStepBriefBuilder>());
        services.TryAddScoped<IProcessStepBriefBuilder, DriverProcessStepBriefBuilder>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IProcessExecutionMetadataContribution, BrowserExecutionMetadataContribution>());
        services.TryAddSingleton<ProcessExecutionMetadataComposer>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IProcessRuntimeToolPreflightContribution, BrowserRuntimeToolPreflightContribution>());
        services.TryAddSingleton<ProcessRuntimeToolPreflightContributionCatalog>();
        services.AddProcessHostCapabilityRuntime();
        services.TryAddScoped<IProcessRuntimeToolPreflightService, ProcessRuntimeToolPreflightService>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IWorkspaceCommandReceiptLifecycleFactExtractor, DotNetWorkspaceCommandReceiptLifecycleFactExtractor>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IProcessLaunchVariableContributor, WorkspaceProductTargetAliasLaunchVariableContributor>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IProcessLaunchVariableContributor, WorkspaceProductTargetFilesystemStateLaunchVariableContributor>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IProcessLaunchVariableContributor, ProcessAcceptanceCriteriaLaunchVariableContributor>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IProcessLaunchVariableContributor, DotNetProductBaselineLaunchVariableContributor>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IProcessLaunchVariableContributor, DotNetProcessLaunchVariableContributor>());
        services.TryAddScoped<ProcessLaunchVariablePreparationService>();
        services.TryAddScoped<DotNetExistingSolutionVerifier>();
        services.TryAddScoped<WorkspaceManagedScriptPlanExecutor>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IProcessRuntimeOwnedStepExecutor, DotNetSolutionSetupRuntimeExecutor>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IProcessToolReceiptPolicyContribution, GenericWorkspaceToolReceiptPolicyContribution>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IProcessToolReceiptPolicyContribution, BrowserInteractionToolReceiptPolicyContribution>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IProcessToolReceiptPolicyContribution, DotNetToolReceiptPolicyContribution>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IProcessRuntimeToolPlanGuard, DotNetSolutionSetupRuntimeToolPlanGuard>());
        services.TryAddScoped<ProcessProductFilesystemInspector>();
        services.TryAddScoped<ProcessProductCompletionPathGate>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IProcessCompletionGateContribution, WorkspaceProductSourceInspectionCompletionGateContribution>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IProcessCompletionGateContribution, WorkspaceProductFilesystemCompletionGateContribution>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IProcessCompletionGateContribution, DotNetSolutionContextCompletionGateContribution>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IProcessCompletionGateContribution, BrowserRuntimeLifecycleCompletionGateContribution>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IProcessCompletionGateContribution, BrowserInteractiveAcceptanceCompletionGateContribution>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IProcessCompletionDefectEvidenceContribution, BrowserConsoleDefectEvidenceContribution>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IProcessCompletionDefectEvidenceContribution, BrowserObservedDefectEvidenceContribution>());
        services.TryAddSingleton<ProcessCompletionDefectEvidenceCatalog>();
        services.TryAddSingleton<ProcessToolReceiptPolicyCatalog>();
        services.TryAddSingleton<ProcessSubprocessContractResolver>();
        services.TryAddScoped<ProcessToolReceiptEvidenceGate>();
        services.TryAddScoped<ProcessCompletionGateFactory>();
        services.TryAddScoped(serviceProvider => serviceProvider
            .GetRequiredService<ProcessCompletionGateFactory>()
            .CreateCompletionGateEvaluator());
        services.TryAddScoped<ProcessExecutionResultConverter>();
        services.TryAddScoped<IParentSubprocessArtifactBridge, ParentSubprocessArtifactBridge>();
        services.TryAddScoped<ProcessCompletionIssueResultFactory>();
        services.TryAddScoped<ProcessManagedArtifactService>();
        services.TryAddScoped<ProcessOutcomeGroundingValidator>();
        services.TryAddScoped<ProcessStepCompletionCoordinator>();
        services.TryAddScoped<ProcessRuntimeOwnedStepCoordinator>();
        services.TryAddScoped<ProcessSubprocessCoordinator>();
        services.TryAddScoped<ProcessParentSubprocessArtifactContextHydrator>();
        services.TryAddScoped<IProcessWorkflowStepExecutor, WorkflowProcessStepExecutor>();
        services.TryAddScoped<AgentFrameworkProcessStepExecutor>();
        services.TryAddScoped<IAgentFrameworkProcessStepExecutor>(serviceProvider => serviceProvider.GetRequiredService<AgentFrameworkProcessStepExecutor>());
        services.TryAddScoped<AgentFrameworkProcessExecutionAdapter>();
        services.TryAddScoped<IProcessExecutionAdapter>(serviceProvider => serviceProvider.GetRequiredService<AgentFrameworkProcessExecutionAdapter>());
        services.TryAddScoped<IProcessStepExecutionDriver>(serviceProvider => serviceProvider.GetRequiredService<AgentFrameworkProcessExecutionAdapter>());
        services.TryAddSingleton(new StandardProcessAdapterCompositionRegistration(
            AgentFrameworkProcessExecutionAdapter.DriverDescriptor));
        services.TryAddScoped<IProcessExecutionObservationReader, AgentFrameworkProcessExecutionObservationReader>();
        services.TryAddScoped<AgentFrameworkProcessRuntimeUsageTelemetryReader>();
        services.TryAddScoped<IProcessRuntimeUsageTelemetryReader, WorkflowAwareProcessRuntimeUsageTelemetryReader>();
        services.TryAddScoped<ProcessRunRecordAssembler>();
        services.TryAddScoped<ProcessRunRecordBackfillProcessor>();
        services.TryAddScoped<ProcessRunRecordQueryService>();
        services.TryAddScoped<ProcessRunManagerAgentSelector>();
        services.TryAddScoped<IProcessRunNarrativeGenerator, AgentFrameworkProcessRunNarrativeGenerator>();
        services.TryAddScoped<ProcessRunRecordBatchProcessor>();
        services.TryAddScoped<AgentFrameworkProcessExecutionClaimRecoveryCoordinator>();
        services.TryAddScoped<AgentFrameworkProcessExecutionClaimRecoveryReconciler>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAgentExecutionRecoveryObserver, AgentFrameworkProcessExecutionRecoveryObserver>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IProcessRuntimeRunCancellationObserver, AgentFrameworkProcessRuntimeCancellationObserver>());
        services.TryAddScoped<IProcessLaunchDriverCatalogProvider, StandardProcessLaunchDriverCatalogProvider>();
        services.TryAddScoped<IProcessLaunchExecutorResolver, AgentFrameworkProcessLaunchExecutorResolver>();
        services.TryAddSingleton<ILaunchVariableTemplateResolver, LaunchVariableTemplateResolver>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IProcessRecoveryAdviceProvider, GenericProcessRecoveryAdviceProvider>());
        services.TryAddSingleton<IProcessStepRecoveryInstructionBuilder, ProcessStepRecoveryInstructionBuilder>();
        services.TryAddScoped<IProcessRuntimeStepAssignmentRepairService, AgentFrameworkProcessRuntimeStepAssignmentRepairService>();
        services.TryAddScoped<IProcessLaunchArtifactInitializer, WorkspaceProcessLaunchArtifactInitializer>();
        services.TryAddScoped<IProcessRuntimeStrategyFactoryResolver, StandardProcessRuntimeStrategyFactoryResolver>();
        services.TryAddScoped<IProcessRuntimeEvidenceSourceProvider, ProcessRuntimeEvidenceSourceProvider>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IMemorySourceGatewayAdapter, ProcessRuntimeMemorySourceGatewayAdapter>());
        // SB13: generic execution/recovery policy contracts implemented by Processes. Stateless (pure functions
        // over injected read-only services), so singleton registration is safe; an absent Processes module leaves
        // these lists empty and generic Core stays in its fail-closed no-override/no-recovery default.
        services.TryAddEnumerable(ServiceDescriptor.Singleton<
            IAgentExecutionOutcomeRecoveryPolicy,
            ProcessAgentExecutionOutcomeRecoveryPolicy>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<
            IToolInvocationPolicyContextContributor,
            ProcessToolInvocationPolicyContextContributor>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<
            IAgentExecutionProviderSelectionPolicy,
            ProcessExecutionProviderSelectionPolicy>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<
            IAgentExecutionRunCriticalityPolicy,
            ProcessExecutionRunCriticalityPolicy>());
        services.Configure<ProcessRuntimeDispatchQueueOptions>(
            configuration.GetSection(ProcessRuntimeDispatchQueueOptions.ConfigurationSectionName));
        services.AddOptions<ProcessRuntimeDispatchOptions>()
            .Bind(configuration.GetSection(ProcessRuntimeDispatchOptions.ConfigurationSectionName))
            .Validate(
                ProcessRuntimeDispatchOptions.IsValid,
                "Process runtime dispatch options are invalid.")
            .ValidateOnStart();
        services.TryAddSingleton<ProcessRuntimeDispatchOptions>(serviceProvider =>
            serviceProvider.GetRequiredService<IOptions<ProcessRuntimeDispatchOptions>>().Value);
        services.AddOptions<ProcessRunRecordProcessingOptions>()
            .Bind(configuration.GetSection(ProcessRunRecordProcessingOptions.ConfigurationSectionName))
            .Validate(
                ProcessRunRecordProcessingOptions.IsValid,
                "Process run record processing options are invalid.")
            .ValidateOnStart();
        services.TryAddSingleton<ProcessRuntimeDispatchQueue>();
        services.TryAddSingleton<IProcessRuntimeDispatchQueue>(serviceProvider => serviceProvider.GetRequiredService<ProcessRuntimeDispatchQueue>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, ProcessRuntimeDispatchQueueWorker>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, AgentFrameworkProcessExecutionClaimRecoveryWorker>());
        if (backgroundWorkersEnabled)
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, ProcessRuntimeProjectionReplayBackgroundWorker>());
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, ProcessRunRecordBackgroundWorker>());
        }

        services.TryAddScoped<ProcessLaunchApplicationService>();
        services.TryAddScoped<ProcessRuntimeDispatchApplicationService>();
        services.TryAddScoped<ProcessRuntimeOperatorApplicationService>();
        services.TryAddScoped<IProcessBlockedRunRecoveryCommandExecutor, ProcessBlockedRunRecoveryCommandExecutor>();
        services.TryAddScoped<IProcessBlockedRunRecoveryPolicyCatalog, ProcessBlockedRunRecoveryPolicyCatalog>();
        services.TryAddScoped<IProcessBlockedRunRecoveryCoordinator, ProcessBlockedRunRecoveryCoordinator>();
        services.TryAddScoped<ProcessRuntimeProjectionQueryService>();
        services.TryAddScoped<IProcessDashboardActivityQueryService, ProcessDashboardActivityQueryService>();
        services.TryAddScoped<ProcessDefinitionCatalogProjectionService>();
        services.TryAddScoped<ProcessDefinitionEditorProjectionService>();
        services.TryAddScoped<ProcessDefinitionRoleEditorProjectionService>();
        services.TryAddScoped<ProcessDefinitionCanvasEditorProjectionService>();
        services.TryAddScoped<ProcessDefinitionStepEditorProjectionService>();
        services.TryAddScoped<ProcessTemplateCatalogProjectionService>();
        services.TryAddScoped<ProcessWorkspaceShellProjectionService>();
        services.TryAddScoped<IProcessWorkspaceProjectionClient, ProcessWorkspaceProjectionClient>();
        services.TryAddSingleton<ProcessWorkspaceMockProjectionFactory>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IShellNavigationContributor, ProcessesShellNavigationContributor>());
        services.AddSingleton<ProcessModuleRewriteState>(ProcessModuleRewriteState.Enabled);
        return services;
    }

    public static IServiceCollection AddProcessHostCapabilityRuntime(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<
            IProcessHostCapabilitySource,
            ManagedRuntimeProcessHostCapabilitySource>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<
            IProcessHostCapabilitySource,
            StandardProcessAdapterHostCapabilitySource>());
        services.TryAddScoped<
            IProcessHostCapabilitySnapshotProvider,
            ProcessHostCapabilitySnapshotProvider>();

        return services;
    }

    private static void ConfigureProcessPersistenceDbContext(
        IServiceProvider serviceProvider,
        DbContextOptionsBuilder options)
    {
        var profile = serviceProvider.GetRequiredService<ICanonicalRuntimeDatabase>().Profile;
        AppDbContextOptionsConfigurator.ConfigureModelCacheKey(options);

        switch (profile.Profile.ProviderKind)
        {
            case DatabaseProviderKind.InMemory:
                options.UseInMemoryDatabase(string.IsNullOrWhiteSpace(profile.ConnectionString)
                    ? $"processes-{profile.Profile.Id:D}"
                    : profile.ConnectionString);
                break;

            case DatabaseProviderKind.PostgreSql:
                options.UseNpgsql(
                    profile.ConnectionString,
                    builder => builder.MigrationsAssembly("CanDoItAll.Migrations.PostgreSql"));
                break;

            default:
                throw new InvalidOperationException($"Unsupported process database provider '{profile.Profile.ProviderKind}'.");
        }
    }
}

public sealed record ProcessModuleRewriteState(bool IsEnabled)
{
    public static ProcessModuleRewriteState Disabled { get; } = new(false);

    public static ProcessModuleRewriteState Enabled { get; } = new(true);
}

public static class ProcessesModuleAssemblyMarker;

internal sealed class UnavailableWorkspaceExecutionRunProcessLeaseCleaner
    : IWorkspaceExecutionRunProcessLeaseCleaner
{
    public static UnavailableWorkspaceExecutionRunProcessLeaseCleaner Instance { get; } = new();

    private UnavailableWorkspaceExecutionRunProcessLeaseCleaner()
    {
    }

    public Task<WorkspaceExecutionRunProcessCleanupResult> CleanupAsync(Guid executionRunId)
        => Task.FromResult(new WorkspaceExecutionRunProcessCleanupResult(
            executionRunId,
            [],
            [
                new WorkspaceExecutionRunProcessCleanupFailure(
                    string.Empty,
                    "ExecutionRun workspace process cleanup is unavailable because no workspace command execution service is registered.")
            ]));
}
