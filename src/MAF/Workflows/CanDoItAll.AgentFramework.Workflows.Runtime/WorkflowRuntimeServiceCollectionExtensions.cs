using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.FileSystem;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.AgentFramework.Core;

public static class WorkflowRuntimeServiceCollectionExtensions
{
    public static IServiceCollection AddWorkflowRuntimeServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<IWorkflowExecutorApprovalGate, WorkflowExternalRequestApprovalGate>();
        services.TryAddSingleton<IWorkflowCheckpointFactory, WorkflowCheckpointFactory>();
        services.TryAddSingleton<IWorkflowEventSink, NullWorkflowEventSink>();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IWorkflowActiveRunRegistry, WorkflowActiveRunRegistry>();
        services.TryAddScoped<IWorkflowRuntimeManager, WorkflowRuntimeManager>();

        return services;
    }

    public static IServiceCollection AddInMemoryWorkflowRuntimeStores(
        this IServiceCollection services,
        string workspaceRoot,
        WorkspaceScopeDescriptor? workspaceScope = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);

        var normalizedWorkspaceRoot = Path.GetFullPath(workspaceRoot);
        var resolvedScope = workspaceScope ?? WorkspaceScopeDescriptor.Sandbox;

        services.TryAddSingleton<InMemoryWorkflowRunStore>();
        services.TryAddSingleton<InMemoryWorkflowUsageObservationStore>();
        services.TryAddSingleton<IWorkflowRunStore>(serviceProvider => serviceProvider.GetRequiredService<InMemoryWorkflowRunStore>());
        services.TryAddSingleton<IWorkflowOverviewStore>(serviceProvider => serviceProvider.GetRequiredService<InMemoryWorkflowRunStore>());
        services.TryAddSingleton<IWorkflowDashboardActivityStore>(serviceProvider => serviceProvider.GetRequiredService<InMemoryWorkflowRunStore>());
        services.TryAddSingleton<IWorkflowUsageObservationStore>(serviceProvider => serviceProvider.GetRequiredService<InMemoryWorkflowUsageObservationStore>());
        services.TryAddSingleton<IWorkflowArtifactStore>(serviceProvider => serviceProvider.GetRequiredService<InMemoryWorkflowRunStore>());
        services.TryAddSingleton<IWorkflowExternalRequestStore>(serviceProvider => serviceProvider.GetRequiredService<InMemoryWorkflowRunStore>());
        services.TryAddSingleton<IWorkflowCheckpointStore>(serviceProvider => serviceProvider.GetRequiredService<InMemoryWorkflowRunStore>());
        services.TryAddScoped<IWorkflowArtifactContentStore>(serviceProvider =>
        {
            IPhysicalFileSystemPathPolicyFactory pathPolicyFactory =
                serviceProvider.GetRequiredService<IPhysicalFileSystemPathPolicyFactory>();
            return new FileWorkflowArtifactContentStore(
                resolvedScope,
                new WorkspacePathResolutionService(
                    normalizedWorkspaceRoot,
                    pathPolicyFactory,
                    resolvedScope),
                new WorkspaceFileService(
                    normalizedWorkspaceRoot,
                    pathPolicyFactory,
                    resolvedScope));
        });

        return services;
    }

    public static IServiceCollection AddFileWorkflowArtifactContentStore(
        this IServiceCollection services,
        Func<IServiceProvider, (string WorkspaceRoot, WorkspaceScopeDescriptor Scope)> scopeFactory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(scopeFactory);

        services.TryAddScoped<IWorkflowArtifactContentStore>(serviceProvider =>
        {
            var (workspaceRoot, scope) = scopeFactory(serviceProvider);
            return new FileWorkflowArtifactContentStore(
                scope,
                serviceProvider.GetRequiredService<IWorkspacePathResolutionService>(),
                serviceProvider.GetRequiredService<IWorkspaceFileService>());
        });

        return services;
    }
}
