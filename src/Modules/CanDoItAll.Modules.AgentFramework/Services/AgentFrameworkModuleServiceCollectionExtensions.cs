using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Capabilities.Access;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Mcp;
using CanDoItAll.AgentFramework.Mcp.Abstractions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.AgentFramework.Tools;
using CanDoItAll.AgentFramework.Tools.Abstractions;
using CanDoItAll.AgentFramework.Voice;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.AgentFramework.Hosting;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tools.Documents;
using CanDoItAll.AgentFramework.Workflows.Templates;
using CanDoItAll.Memory.Application;
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
        services.AddMafProviderRuntimeServices();
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
        services.TryAddScoped<IWorkspaceProcessHost, LocalWorkspaceProcessHost>();
        services.TryAddScoped<IWorkspaceCommandExecutionService>(serviceProvider =>
        {
            var (workspaceRoot, scope) = ResolveCurrentWorkspaceScope(serviceProvider);
            return new WorkspaceCommandExecutionService(
                workspaceRoot,
                serviceProvider.GetRequiredService<IWorkspaceProcessHost>(),
                scope);
        });
        services.TryAddScoped<IWorkspaceDocumentMarkdownConverter, ManagedCodeMarkItDownDocumentMarkdownConverter>();
        services.TryAddScoped<IWorkspaceArtifactToolService>(serviceProvider =>
        {
            var (workspaceRoot, scope) = ResolveCurrentWorkspaceScope(serviceProvider);
            return new WorkspaceArtifactToolService(
                workspaceRoot,
                serviceProvider.GetRequiredService<IWorkspaceCommandExecutionService>(),
                serviceProvider.GetRequiredService<IWorkspaceDocumentMarkdownConverter>(),
                scope);
        });
        services.TryAddScoped<MafAgentRuntime>(serviceProvider =>
        {
            var (workspaceRoot, scope) = ResolveCurrentWorkspaceScope(serviceProvider);
            return new MafAgentRuntime(workspaceRoot, serviceProvider, scope);
        });
        services.TryAddScoped<IAgentRuntime>(serviceProvider => serviceProvider.GetRequiredService<MafAgentRuntime>());
        services.TryAddScoped<ISandboxWorkspaceExecutionRunStore>(serviceProvider =>
            (ISandboxWorkspaceExecutionRunStore)serviceProvider.GetRequiredService<ISandboxWorkspaceStore>());
        services.TryAddSingleton<IAgentExecutionCancellationRegistry, AgentExecutionCancellationRegistry>();
        services.AddScoped<IProviderProfileRegistry, WorkspaceBackedAgentProviderProfileRegistry>();
        services.TryAddScoped<AgentReferenceDataCache>();
        services.TryAddScoped<IAgentReferenceDataCacheInvalidator>(serviceProvider =>
            serviceProvider.GetRequiredService<AgentReferenceDataCache>());
        services.TryAddScoped<IAgentReferenceDataProvider, WorkspaceBackedAgentReferenceDataProvider>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ICanDoItAllAgentWorkspaceFactory, CanDoItAllAgentWorkspaceFactory>();
        services.AddScoped<CanDoItAllAgentWorkspaceFactory>(serviceProvider =>
            (CanDoItAllAgentWorkspaceFactory)serviceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>());
        services.AddScoped<IAgentFrameworkWorkspaceService, CurrentProfileAgentFrameworkWorkspaceService>();
        services.AddScoped<IAgentChatAttachmentStagingService, AgentChatAttachmentStagingService>();
        services.AddScoped<IAgentFrameworkOrganizationCatalogRepairService, AgentFrameworkOrganizationCatalogRepairService>();
        services.AddScoped<AgentFrameworkCatalogWarmupService>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAgentRuntimeToolProvider, ImageGenerationAgentRuntimeToolProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAgentRuntimeToolProvider, MemoryAgentRuntimeToolProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IWorkflowExecutorDescriptorSource, MemoryWorkflowExecutorDescriptorSource>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IWorkflowExecutor, MemoryWorkflowExecutor>());
        services.AddWorkflowTemplateServices();
        services.AddScoped<WorkflowExampleCatalogSeedService>();
        services.AddScoped<ProcessMockAgentCatalogService>();
        services.AddScoped<AgentFrameworkExecutionRecoveryService>();
        services.AddScoped<ScenarioHarnessService>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAgentContextContributor, MemoryAgentContextContributor>());
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
        services.TryAddScoped<IWorkflowComponentLibraryService>(serviceProvider => serviceProvider.GetRequiredService<PersistentWorkflowCatalogService>());
        services.TryAddScoped<IWorkflowSettingsService>(serviceProvider => serviceProvider.GetRequiredService<PersistentWorkflowCatalogService>());
        services.TryAddScoped<PersistentWorkflowRunStore>();
        services.TryAddScoped<IWorkflowRunStore>(serviceProvider => serviceProvider.GetRequiredService<PersistentWorkflowRunStore>());
        services.TryAddScoped<IWorkflowArtifactStore>(serviceProvider => serviceProvider.GetRequiredService<PersistentWorkflowRunStore>());
        services.TryAddScoped<IWorkflowExternalRequestStore>(serviceProvider => serviceProvider.GetRequiredService<PersistentWorkflowRunStore>());
        services.TryAddScoped<IWorkflowCheckpointStore>(serviceProvider => serviceProvider.GetRequiredService<PersistentWorkflowRunStore>());
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
