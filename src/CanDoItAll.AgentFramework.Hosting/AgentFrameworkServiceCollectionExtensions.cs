using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
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
