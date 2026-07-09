using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.AgentFramework.Voice;
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
        services.TryAddSingleton<IPluginWorkspaceFiles, PluginWorkspaceFiles>();
        services.TryAddSingleton<IWorkspacePathResolutionService>(_ => new WorkspacePathResolutionService(normalizedWorkspaceRoot, resolvedScope));
        services.TryAddSingleton<IWorkspaceProcessHost, LocalWorkspaceProcessHost>();
        services.TryAddSingleton<IWorkspaceCommandExecutionService>(serviceProvider => new WorkspaceCommandExecutionService(
            normalizedWorkspaceRoot,
            serviceProvider.GetRequiredService<IWorkspaceProcessHost>(),
            resolvedScope,
            serviceProvider.GetServices<IWorkspaceCommandReceiptLifecycleFactExtractor>()));
        services.TryAddSingleton<IWorkspaceDocumentMarkdownConverter, ManagedCodeMarkItDownDocumentMarkdownConverter>();
        services.TryAddSingleton<IWorkspaceArtifactToolService>(serviceProvider => new WorkspaceArtifactToolService(
            normalizedWorkspaceRoot,
            serviceProvider.GetRequiredService<IWorkspaceCommandExecutionService>(),
            serviceProvider.GetRequiredService<IWorkspaceDocumentMarkdownConverter>(),
            resolvedScope));
        services.TryAddSingleton<IAgentProviderCredentialResolver, EnvironmentVariableAgentProviderCredentialResolver>();
        services.AddMafProviderRuntimeServices();
        services.TryAddSingleton<IProviderProfileService, ProviderProfileService>();
        services.TryAddScoped<AgentReferenceDataCache>();
        services.TryAddScoped<IAgentReferenceDataCacheInvalidator>(serviceProvider =>
            serviceProvider.GetRequiredService<AgentReferenceDataCache>());
        services.TryAddScoped<IAgentReferenceDataProvider, WorkspaceBackedAgentReferenceDataProvider>();
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
        services.TryAddSingleton<IAgentExecutionCancellationRegistry, AgentExecutionCancellationRegistry>();
        services.AddAgentFrameworkA2AHosting();
        services.AddAgentFrameworkVoice();
        services.TryAddSingleton<MafAgentRuntime>(serviceProvider => new MafAgentRuntime(normalizedWorkspaceRoot, serviceProvider, resolvedScope));
        services.TryAddSingleton<IAgentRuntime>(serviceProvider => serviceProvider.GetRequiredService<MafAgentRuntime>());
        services.TryAddSingleton<ICapabilityProofService, CapabilityProofService>();
        services.TryAddSingleton<IProviderDiagnosticsService>(serviceProvider => new ProviderDiagnosticsService(
            serviceProvider.GetRequiredService<IAgentRuntime>()));
        services.TryAddSingleton<ISpreadsheetDocumentService, ClosedXmlSpreadsheetDocumentService>();
        services.TryAddSingleton<IProjectStructureRuntimeGateway, UnavailableProjectStructureRuntimeGateway>();
        services.AddMafWorkflowAdapterServices(ServiceLifetime.Singleton);
        services.AddInMemoryWorkflowCatalogServices();
        services.AddInMemoryWorkflowRuntimeStores(normalizedWorkspaceRoot, resolvedScope);
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
