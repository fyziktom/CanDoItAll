using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.AgentFramework.Hosting;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;
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
        services.AddSingleton<IProviderProfileService, ProviderProfileService>();
        services.AddSingleton<ICapabilityProofService, CapabilityProofService>();
        services.AddSingleton<IAgentProviderCredentialResolver, SecretStoreAgentProviderCredentialResolver>();
        services.AddScoped<ISandboxWorkspaceStore>(serviceProvider =>
        {
            var workspaceRoot = serviceProvider.GetRequiredService<IWorkspacePathResolver>().ResolveWorkspaceRoot();
            var profile = serviceProvider.GetRequiredService<IDatabaseProfileRuntimeAccessor>().ResolveCurrentProfile();
            var scope = WorkspaceScopeDescriptor.Organization(profile.Profile.Id.ToString("N"));
            return new FileSandboxWorkspaceStore(workspaceRoot, scope);
        });
        services.TryAddScoped<ISandboxWorkspaceExecutionRunStore>(serviceProvider =>
            (ISandboxWorkspaceExecutionRunStore)serviceProvider.GetRequiredService<ISandboxWorkspaceStore>());
        services.AddScoped<IProviderProfileRegistry, WorkspaceBackedAgentProviderProfileRegistry>();
        services.AddScoped<ICanDoItAllAgentWorkspaceFactory, CanDoItAllAgentWorkspaceFactory>();
        services.AddScoped<CanDoItAllAgentWorkspaceFactory>(serviceProvider =>
            (CanDoItAllAgentWorkspaceFactory)serviceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>());
        services.AddScoped<IAgentFrameworkWorkspaceService, CurrentProfileAgentFrameworkWorkspaceService>();
        services.AddScoped<IAgentFrameworkOrganizationCatalogRepairService, AgentFrameworkOrganizationCatalogRepairService>();
        services.AddScoped<AgentFrameworkCatalogWarmupService>();
        services.AddScoped<ProcessMockAgentCatalogService>();
        services.AddScoped<AgentFrameworkExecutionRecoveryService>();
        services.AddScoped<ScenarioHarnessService>();
        services.AddScoped<IDatabaseTransferHandler, AiAgentsDatabaseTransferHandler>();
        services.AddScoped<IProviderRuntimeGateway, AgentFrameworkProviderRuntimeGateway>();
        services.AddScoped<IAiTechnicalAgentBridge, AgentFrameworkAiTechnicalAgentBridge>();
        services.TryAddSingleton<IWorkflowDefinitionValidator, WorkflowDefinitionValidator>();
        services.TryAddSingleton<IWorkflowRuntimeBackendCatalog, WorkflowRuntimeBackendCatalog>();
        services.TryAddSingleton<InMemoryWorkflowCatalogStore>();
        services.TryAddScoped<InMemoryWorkflowCatalogService>();
        services.TryAddScoped<IWorkflowCatalogService>(serviceProvider => serviceProvider.GetRequiredService<InMemoryWorkflowCatalogService>());
        services.TryAddScoped<IWorkflowComponentLibraryService>(serviceProvider => serviceProvider.GetRequiredService<InMemoryWorkflowCatalogService>());
        services.TryAddScoped<IWorkflowSettingsService>(serviceProvider => serviceProvider.GetRequiredService<InMemoryWorkflowCatalogService>());
        services.TryAddSingleton<InMemoryWorkflowRunStore>();
        services.TryAddSingleton<IWorkflowRunStore>(serviceProvider => serviceProvider.GetRequiredService<InMemoryWorkflowRunStore>());
        services.TryAddSingleton<IWorkflowArtifactStore>(serviceProvider => serviceProvider.GetRequiredService<InMemoryWorkflowRunStore>());
        services.TryAddSingleton<IWorkflowExternalRequestStore>(serviceProvider => serviceProvider.GetRequiredService<InMemoryWorkflowRunStore>());
        services.TryAddSingleton<IWorkflowEventSink, NullWorkflowEventSink>();
        services.TryAddSingleton<MafWorkflowCompiler>();
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
}

public static class AgentFrameworkModuleAssemblyMarker;
