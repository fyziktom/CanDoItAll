using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Tools.Documents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.AgentFramework.Hosting;

public static class AgentFrameworkServiceCollectionExtensions
{
    public static IServiceCollection AddAgentFrameworkCore(
        this IServiceCollection services,
        string workspaceRoot,
        WorkspaceScopeDescriptor? workspaceScope = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);

        var normalizedWorkspaceRoot = Path.GetFullPath(workspaceRoot);
        var resolvedScope = workspaceScope ?? WorkspaceScopeDescriptor.Sandbox;

        services.TryAddSingleton<ISandboxWorkspaceStore>(_ => new FileSandboxWorkspaceStore(normalizedWorkspaceRoot, resolvedScope));
        services.TryAddSingleton<IAgentPackageService>(_ => new ZipAgentPackageService(normalizedWorkspaceRoot, resolvedScope));
        services.TryAddSingleton<IWorkspaceFileService>(_ => new WorkspaceFileService(normalizedWorkspaceRoot, resolvedScope));
        services.TryAddSingleton<IWorkspacePathResolutionService>(_ => new WorkspacePathResolutionService(normalizedWorkspaceRoot, resolvedScope));
        services.TryAddSingleton<IWorkspaceProcessHost, LocalWorkspaceProcessHost>();
        services.TryAddSingleton<IWorkspaceCommandExecutionService>(serviceProvider => new WorkspaceCommandExecutionService(
            normalizedWorkspaceRoot,
            serviceProvider.GetRequiredService<IWorkspaceProcessHost>(),
            resolvedScope));
        services.TryAddSingleton<IWorkspaceArtifactToolService>(serviceProvider => new WorkspaceArtifactToolService(
            normalizedWorkspaceRoot,
            serviceProvider.GetRequiredService<IWorkspaceCommandExecutionService>(),
            resolvedScope));
        services.TryAddSingleton<IAgentProviderCredentialResolver, EnvironmentVariableAgentProviderCredentialResolver>();
        services.TryAddSingleton<IProviderProfileService, ProviderProfileService>();
        services.TryAddSingleton<IProviderProfileRegistry>(serviceProvider => new WorkspaceBackedProviderProfileRegistry(
            serviceProvider.GetRequiredService<ISandboxWorkspaceStore>(),
            serviceProvider.GetRequiredService<IProviderProfileService>()));
        services.TryAddSingleton<IAgentExecutionCheckpointBridge>(serviceProvider => new WorkflowBackedAgentExecutionCheckpointBridge(
            serviceProvider.GetRequiredService<ISandboxWorkspaceStore>(),
            normalizedWorkspaceRoot,
            resolvedScope));
        services.TryAddSingleton<IAgentExecutionGovernanceBridge>(serviceProvider => new DurableAgentExecutionGovernanceBridge(
            serviceProvider.GetRequiredService<IAgentExecutionCheckpointBridge>()));
        services.TryAddSingleton<BufferedAgentExecutionEventSink>();
        services.TryAddSingleton<IAgentExecutionEventSink>(serviceProvider => serviceProvider.GetRequiredService<BufferedAgentExecutionEventSink>());
        services.AddAgentFrameworkA2AHosting();
        services.TryAddSingleton<MafAgentRuntime>(serviceProvider => new MafAgentRuntime(normalizedWorkspaceRoot, serviceProvider, resolvedScope));
        services.TryAddSingleton<IAgentRuntime>(serviceProvider => serviceProvider.GetRequiredService<MafAgentRuntime>());
        services.TryAddSingleton<ICapabilityProofService, CapabilityProofService>();
        services.TryAddSingleton<IProviderDiagnosticsService>(serviceProvider => new ProviderDiagnosticsService(
            serviceProvider.GetRequiredService<IAgentRuntime>()));
        services.TryAddSingleton<ISpreadsheetDocumentService, ClosedXmlSpreadsheetDocumentService>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IWorkflowExecutor, WorkspaceFileWorkflowExecutor>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IWorkflowExecutor, HttpFetchWorkflowExecutor>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IWorkflowExecutor, SpreadsheetWorkflowExecutor>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IWorkflowExecutor, ProjectStructureWorkflowExecutor>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IWorkflowExecutor, ImageGenerationWorkflowExecutor>());
        foreach (var descriptor in BuiltInWorkflowExecutorDescriptors.Planned)
        {
            services.AddSingleton<IWorkflowExecutor>(new PlannedWorkflowExecutor(descriptor));
        }

        services.TryAddSingleton<IWorkflowExecutorCatalog, WorkflowExecutorCatalog>();
        services.TryAddSingleton<IWorkflowExecutorInvoker, WorkflowExecutorInvoker>();
        services.TryAddSingleton<IWorkflowLlmComponentInvoker, MafWorkflowLlmComponentInvoker>();
        services.TryAddSingleton<IWorkflowDefinitionValidator, WorkflowDefinitionValidator>();
        services.TryAddSingleton<IWorkflowRuntimeBackendCatalog, WorkflowRuntimeBackendCatalog>();
        services.TryAddSingleton<InMemoryWorkflowCatalogStore>();
        services.TryAddScoped(serviceProvider => new InMemoryWorkflowCatalogService(
            serviceProvider.GetRequiredService<InMemoryWorkflowCatalogStore>(),
            serviceProvider.GetRequiredService<IWorkflowDefinitionValidator>(),
            serviceProvider.GetRequiredService<IProviderProfileRegistry>(),
            serviceProvider.GetRequiredService<IProviderProfileService>()));
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
        services.TryAddScoped<IAgentFrameworkWorkspaceService, AgentFrameworkWorkspaceService>();

        return services;
    }

    public static IServiceCollection AddAgentFrameworkIntegrated(
        this IServiceCollection services,
        string workspaceRoot,
        WorkspaceScopeDescriptor? workspaceScope = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);

        return services.AddAgentFrameworkCore(workspaceRoot, workspaceScope);
    }
}
