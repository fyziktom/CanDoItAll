using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.AgentFramework.Voice;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.AgentFramework.Hosting;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tools.Documents;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Modules.AgentFramework;

public static class AgentFrameworkModuleServiceCollectionExtensions
{
    public static IServiceCollection AddAgentFrameworkModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var backgroundWorkersEnabled = LocalRuntimeHostedWorkerPolicy.AreBackgroundHostedWorkersEnabled(
            configuration[LocalRuntimeHostedWorkerPolicy.LaneKindConfigurationKey],
            configuration["LaneKind"]);

        services.AddOptions<ProcessMockAgentOptions>()
            .Bind(configuration.GetSection(ProcessMockAgentOptions.SectionName));
        services.AddOptions<WorkflowExampleCatalogSeedOptions>()
            .Bind(configuration.GetSection(WorkflowExampleCatalogSeedOptions.SectionName));
        services.AddAgentFrameworkVoice();
        services.AddSingleton<IProviderProfileService, ProviderProfileService>();
        services.AddSingleton<ICapabilityProofService, CapabilityProofService>();
        services.AddSingleton<IAgentProviderCredentialResolver, SecretStoreAgentProviderCredentialResolver>();
        services.AddScoped<ISandboxWorkspaceStore>(serviceProvider =>
        {
            var (workspaceRoot, scope) = ResolveCurrentWorkspaceScope(serviceProvider);
            return new FileSandboxWorkspaceStore(workspaceRoot, scope);
        });
        services.TryAddScoped<IWorkspaceFileService>(serviceProvider =>
        {
            var (workspaceRoot, scope) = ResolveCurrentWorkspaceScope(serviceProvider);
            return new WorkspaceFileService(workspaceRoot, scope);
        });
        services.TryAddScoped<IPluginWorkspaceFiles, PluginWorkspaceFiles>();
        services.TryAddScoped<IWorkspacePathResolutionService>(serviceProvider =>
        {
            var (workspaceRoot, scope) = ResolveCurrentWorkspaceScope(serviceProvider);
            return new WorkspacePathResolutionService(workspaceRoot, scope);
        });
        services.TryAddScoped<MafAgentRuntime>(serviceProvider =>
        {
            var (workspaceRoot, scope) = ResolveCurrentWorkspaceScope(serviceProvider);
            return new MafAgentRuntime(workspaceRoot, serviceProvider, scope);
        });
        services.TryAddScoped<IAgentRuntime>(serviceProvider => serviceProvider.GetRequiredService<MafAgentRuntime>());
        services.TryAddScoped<ISandboxWorkspaceExecutionRunStore>(serviceProvider =>
            (ISandboxWorkspaceExecutionRunStore)serviceProvider.GetRequiredService<ISandboxWorkspaceStore>());
        services.AddScoped<IProviderProfileRegistry, WorkspaceBackedAgentProviderProfileRegistry>();
        services.AddScoped<ICanDoItAllAgentWorkspaceFactory, CanDoItAllAgentWorkspaceFactory>();
        services.AddScoped<CanDoItAllAgentWorkspaceFactory>(serviceProvider =>
            (CanDoItAllAgentWorkspaceFactory)serviceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>());
        services.AddScoped<IAgentFrameworkWorkspaceService, CurrentProfileAgentFrameworkWorkspaceService>();
        services.AddScoped<IAgentFrameworkOrganizationCatalogRepairService, AgentFrameworkOrganizationCatalogRepairService>();
        services.AddScoped<AgentFrameworkCatalogWarmupService>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAgentRuntimeToolProvider, ImageGenerationAgentRuntimeToolProvider>());
        services.TryAddScoped(serviceProvider => new WorkflowTemplatePackLoader(
            serviceProvider.GetRequiredService<IWorkflowExecutorCatalog>()));
        services.AddScoped<WorkflowExampleCatalogSeedService>();
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
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IWorkflowExecutor, WorkspaceFileWorkflowExecutor>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IWorkflowExecutor, JsonTransformWorkflowExecutor>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IWorkflowExecutor, MarkdownRenderWorkflowExecutor>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IWorkflowExecutor, SourceIngestionWorkflowExecutor>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IWorkflowExecutor, HttpFetchWorkflowExecutor>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IWorkflowExecutor, DelayWorkflowExecutor>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IWorkflowExecutor, HumanApprovalWorkflowExecutor>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IWorkflowExecutor, SpreadsheetWorkflowExecutor>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IWorkflowExecutor, ProjectStructureWorkflowExecutor>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IWorkflowExecutor, ImageGenerationWorkflowExecutor>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IWorkflowExecutorDescriptorSource, BuiltInWorkflowExecutorDescriptorSource>());
        foreach (var descriptor in BuiltInWorkflowExecutorDescriptors.Planned)
        {
            services.AddScoped<IWorkflowExecutor>(_ => new PlannedWorkflowExecutor(descriptor));
        }

        services.TryAddScoped<IWorkflowExecutorCatalog>(serviceProvider =>
            WorkflowExecutorCatalog.FromDescriptorSources(serviceProvider.GetServices<IWorkflowExecutorDescriptorSource>()));
        services.TryAddScoped<IWorkflowExecutorExecutionObserver, CompositeWorkflowExecutorExecutionObserver>();
        services.TryAddScoped<IWorkflowExecutorApprovalGate, WorkflowExternalRequestApprovalGate>();
        services.TryAddScoped<IWorkflowExecutorInvoker, WorkflowExecutorInvoker>();
        services.TryAddScoped<IWorkflowLlmComponentInvoker, MafWorkflowLlmComponentInvoker>();
        services.TryAddScoped<IWorkflowDefinitionValidator>(serviceProvider => new WorkflowDefinitionValidator(
            serviceProvider.GetRequiredService<IWorkflowExecutorCatalog>()));
        services.TryAddSingleton<IWorkflowRuntimeBackendCatalog>(_ => new WorkflowRuntimeBackendCatalog());
        services.TryAddScoped<PersistentWorkflowCatalogService>();
        services.TryAddScoped<IWorkflowCatalogService>(serviceProvider => serviceProvider.GetRequiredService<PersistentWorkflowCatalogService>());
        services.TryAddScoped<IWorkflowComponentLibraryService>(serviceProvider => serviceProvider.GetRequiredService<PersistentWorkflowCatalogService>());
        services.TryAddScoped<IWorkflowSettingsService>(serviceProvider => serviceProvider.GetRequiredService<PersistentWorkflowCatalogService>());
        services.TryAddScoped<PersistentWorkflowRunStore>();
        services.TryAddScoped<IWorkflowRunStore>(serviceProvider => serviceProvider.GetRequiredService<PersistentWorkflowRunStore>());
        services.TryAddScoped<IWorkflowArtifactStore>(serviceProvider => serviceProvider.GetRequiredService<PersistentWorkflowRunStore>());
        services.TryAddScoped<IWorkflowExternalRequestStore>(serviceProvider => serviceProvider.GetRequiredService<PersistentWorkflowRunStore>());
        services.TryAddScoped<IWorkflowCheckpointStore>(serviceProvider => serviceProvider.GetRequiredService<PersistentWorkflowRunStore>());
        services.TryAddScoped<IWorkflowArtifactContentStore>(serviceProvider =>
        {
            var (workspaceRoot, scope) = ResolveCurrentWorkspaceScope(serviceProvider);
            return new FileWorkflowArtifactContentStore(workspaceRoot, scope);
        });
        services.TryAddScoped<IWorkflowCheckpointFactory, WorkflowCheckpointFactory>();
        services.TryAddScoped<IWorkflowPayloadPolicyService, WorkflowPayloadPolicyService>();
        services.TryAddScoped<IWorkflowRuntimeEvidenceSourceProvider, WorkflowRuntimeEvidenceSourceProvider>();
        services.TryAddSingleton<IWorkflowEventSink, NullWorkflowEventSink>();
        services.TryAddScoped<MafWorkflowCompiler>();
        services.TryAddScoped<IWorkflowMafCompiler>(serviceProvider => serviceProvider.GetRequiredService<MafWorkflowCompiler>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IWorkflowExecutionBackend, MafInProcessWorkflowExecutionBackend>());
        services.TryAddScoped<IWorkflowRuntimeManager, WorkflowRuntimeManager>();
        services.TryAddScoped<IWorkflowProcessExecutorBridge, WorkflowProcessExecutorBridge>();
        services.TryAddScoped<IWorkflowTestRunner, WorkflowTestRunner>();

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
