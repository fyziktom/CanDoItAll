using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class WorkflowHitlRecoveryCompositionIntegrationTests
{
    [Fact]
    public async Task ProductionModule_ResolvesPersistentHitlStoresAndExactlyOneExecutorDecorator()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("workflow-hitl-composition");
        var profile = testEnvironment.CreateInMemoryProfile("composition");
        var configuration = TestApplicationBootstrap.BuildConfiguration(
            profile,
            new Dictionary<string, string?>
            {
                [LocalRuntimeHostedWorkerPolicy.LaneKindConfigurationKey] =
                    LocalRuntimeHostedWorkerPolicy.McpToolHostLaneKind
            });
        var services = new ServiceCollection();
        var environment = testEnvironment.CreateHostEnvironment(
            nameof(WorkflowHitlRecoveryCompositionIntegrationTests));

        TestApplicationBootstrap.ConfigureDefaultServices(services, configuration, environment);

        AssertSingleScopedRegistration<IWorkflowBackendCheckpointPayloadStore>(services);
        AssertSingleScopedRegistration<IWorkflowExternalRequestBoundaryStore>(services);
        AssertSingleScopedRegistration<IWorkflowExternalResponseOperationStore>(services);
        AssertSingleScopedRegistration<IWorkflowResumeBoundaryStore>(services);
        AssertSingleScopedRegistration<IWorkflowExecutorInvocationDeduplicationStore>(services);
        AssertSingleScopedRegistration<IWorkflowExecutorInvoker>(services);
        AssertSingleScopedRegistration<WorkflowExecutorInvoker>(services);

        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true
        });
        await TestApplicationBootstrap.InitializeSchemaAsync(
            provider,
            TestSchemaBootstrapModules.Full);
        await using var scope = provider.CreateAsyncScope();
        var scopedProvider = scope.ServiceProvider;

        AssertPersistentAlias<
            IWorkflowBackendCheckpointPayloadStore,
            PersistentWorkflowBackendCheckpointPayloadStore>(scopedProvider);
        AssertPersistentAlias<
            IWorkflowExternalRequestBoundaryStore,
            PersistentWorkflowExternalRequestBoundaryStore>(scopedProvider);
        AssertPersistentAlias<
            IWorkflowExternalResponseOperationStore,
            PersistentWorkflowExternalResponseOperationStore>(scopedProvider);
        AssertPersistentAlias<
            IWorkflowResumeBoundaryStore,
            PersistentWorkflowResumeBoundaryStore>(scopedProvider);
        AssertPersistentAlias<
            IWorkflowExecutorInvocationDeduplicationStore,
            PersistentWorkflowExecutorInvocationDeduplicationStore>(scopedProvider);

        var concreteInvokers = scopedProvider.GetServices<WorkflowExecutorInvoker>().ToArray();
        var interfaceInvokers = scopedProvider.GetServices<IWorkflowExecutorInvoker>().ToArray();

        Assert.IsType<WorkflowExecutorInvoker>(Assert.Single(concreteInvokers));
        Assert.IsType<DeduplicatingWorkflowExecutorInvoker>(Assert.Single(interfaceInvokers));
    }

    private static void AssertPersistentAlias<TService, TImplementation>(
        IServiceProvider serviceProvider)
        where TService : class
        where TImplementation : class, TService
    {
        var implementation = serviceProvider.GetRequiredService<TImplementation>();
        var service = serviceProvider.GetRequiredService<TService>();

        Assert.IsType<TImplementation>(service);
        Assert.Same(implementation, service);
    }

    private static void AssertSingleScopedRegistration<TService>(IServiceCollection services)
    {
        ServiceDescriptor descriptor = Assert.Single(
            services,
            candidate => candidate.ServiceType == typeof(TService));

        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }
}
