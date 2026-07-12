using CanDoItAll.AgentFramework.Workflows.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.AgentFramework.Core;

public static class WorkflowCoreServiceCollectionExtensions
{
    public static IServiceCollection AddWorkflowCoreServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<IWorkflowDefinitionValidator>(serviceProvider => new WorkflowDefinitionValidator(
            serviceProvider.GetRequiredService<IWorkflowExecutorCatalog>()));
        services.TryAddSingleton<IWorkflowRuntimeBackendCatalog>(_ => new WorkflowRuntimeBackendCatalog());
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IWorkflowLaunchIdempotencyStore, InMemoryWorkflowLaunchIdempotencyStore>();
        services.TryAddScoped<IWorkflowRunLauncher, WorkflowRuntimeManagerRunLauncher>();
        services.TryAddScoped<IWorkflowLaunchService, WorkflowLaunchService>();
        services.TryAddScoped<IWorkflowUsageAnalyticsStore, WorkflowUsageAnalyticsStore>();
        services.TryAddScoped<IWorkflowAnalyticsQueryService, WorkflowAnalyticsQueryService>();
        services.TryAddScoped<IWorkflowPayloadPolicyService, WorkflowPayloadPolicyService>();
        services.TryAddScoped<IWorkflowTestRunner, WorkflowTestRunner>();

        return services;
    }

    public static IServiceCollection AddInMemoryWorkflowCatalogServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<InMemoryWorkflowCatalogStore>();
        services.TryAddScoped(serviceProvider => new InMemoryWorkflowCatalogService(
            serviceProvider.GetRequiredService<InMemoryWorkflowCatalogStore>(),
            serviceProvider.GetRequiredService<IWorkflowDefinitionValidator>(),
            serviceProvider.GetRequiredService<IProviderProfileRegistry>(),
            serviceProvider.GetRequiredService<IProviderProfileService>(),
            serviceProvider.GetRequiredService<IWorkflowRuntimeBackendCatalog>()));
        services.TryAddScoped<IWorkflowCatalogService>(serviceProvider => serviceProvider.GetRequiredService<InMemoryWorkflowCatalogService>());
        services.TryAddScoped<IWorkflowComponentLibraryService>(serviceProvider => serviceProvider.GetRequiredService<InMemoryWorkflowCatalogService>());
        services.TryAddScoped<IWorkflowSettingsService>(serviceProvider => serviceProvider.GetRequiredService<InMemoryWorkflowCatalogService>());

        return services;
    }
}
