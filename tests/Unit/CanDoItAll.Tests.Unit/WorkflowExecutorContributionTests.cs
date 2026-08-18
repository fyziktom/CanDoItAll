using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class WorkflowExecutorContributionTests
{
    [Fact]
    public void CoreServicesResolveStandardContributionAndCompatibilityAliasWithoutDuplicates()
    {
        var services = new ServiceCollection();
        services.AddScoped<ScopedExecutorDependency>();
        services.AddWorkflowExecutorContribution<ScopedWorkflowExecutor>(
            BuiltInWorkflowExecutorDescriptors.JsonTransform,
            ServiceLifetime.Scoped);
        services.AddWorkflowExecutorCoreServices();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true
        });
        ScopedWorkflowExecutor firstExecutor;
        using (var scope = provider.CreateScope())
        {
            firstExecutor = scope.ServiceProvider.GetRequiredService<ScopedWorkflowExecutor>();
            var contribution = Assert.Single(
                scope.ServiceProvider.GetRequiredService<IEnumerable<IWorkflowExecutorContribution>>());
            var compatibilityExecutor = Assert.Single(
                scope.ServiceProvider.GetRequiredService<IEnumerable<IWorkflowExecutor>>());
            var catalog = scope.ServiceProvider.GetRequiredService<IWorkflowExecutorCatalog>();

            Assert.Equal(BuiltInWorkflowExecutorDescriptors.JsonTransform, contribution.Descriptor);
            Assert.Equal(BuiltInWorkflowExecutorDescriptors.JsonTransform, compatibilityExecutor.Descriptor);
            Assert.Equal(BuiltInWorkflowExecutorDescriptors.JsonTransform, catalog.GetRequiredExecutor(WorkflowExecutorIds.JsonTransform));
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<IWorkflowExecutorInvoker>());
            Assert.Same(firstExecutor, scope.ServiceProvider.GetRequiredService<ScopedWorkflowExecutor>());
        }

        using var secondScope = provider.CreateScope();
        var secondExecutor = secondScope.ServiceProvider.GetRequiredService<ScopedWorkflowExecutor>();
        Assert.NotSame(firstExecutor, secondExecutor);
        Assert.NotEqual(firstExecutor.Dependency.InstanceId, secondExecutor.Dependency.InstanceId);
    }

    [Fact]
    public void CatalogResolutionDoesNotActivateWorkflowExecutorImplementations()
    {
        var services = new ServiceCollection();
        services.AddWorkflowExecutorContribution<ThrowingWorkflowExecutor>(
            BuiltInWorkflowExecutorDescriptors.JsonTransform,
            ServiceLifetime.Scoped);
        services.AddWorkflowExecutorCoreServices();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var catalog = scope.ServiceProvider.GetRequiredService<IWorkflowExecutorCatalog>();

        Assert.Equal(
            BuiltInWorkflowExecutorDescriptors.JsonTransform,
            catalog.GetRequiredExecutor(WorkflowExecutorIds.JsonTransform));
        Assert.Throws<InvalidOperationException>(
            scope.ServiceProvider.GetRequiredService<IWorkflowExecutorInvoker>);
    }

    [Fact]
    public void CoreServicesRejectDuplicateContributionIds()
    {
        var services = new ServiceCollection();
        services.AddWorkflowExecutorContribution<FirstDuplicateWorkflowExecutor>(
            BuiltInWorkflowExecutorDescriptors.JsonTransform,
            ServiceLifetime.Scoped);
        services.AddWorkflowExecutorContribution<SecondDuplicateWorkflowExecutor>(
            BuiltInWorkflowExecutorDescriptors.JsonTransform,
            ServiceLifetime.Scoped);
        services.AddWorkflowExecutorCoreServices();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var exception = Assert.Throws<InvalidOperationException>(
            scope.ServiceProvider.GetRequiredService<IWorkflowExecutorCatalog>);

        Assert.Contains("duplicate descriptor", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(WorkflowExecutorIds.JsonTransform.Value, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CoreServicesRejectLegacyImplementationThatDuplicatesContributionAlias()
    {
        var services = new ServiceCollection();
        services.AddWorkflowExecutorContribution<FirstDuplicateWorkflowExecutor>(
            BuiltInWorkflowExecutorDescriptors.JsonTransform,
            ServiceLifetime.Scoped);
        services.AddScoped<IWorkflowExecutor, LegacyDuplicateWorkflowExecutor>();
        services.AddWorkflowExecutorCoreServices();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var exception = Assert.Throws<InvalidOperationException>(
            scope.ServiceProvider.GetRequiredService<IWorkflowExecutorInvoker>);

        Assert.Contains("duplicate implementation", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(WorkflowExecutorIds.JsonTransform.Value, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CoreServicesRejectRunnableLegacyDescriptorWithoutImplementation()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IWorkflowExecutorDescriptorSource>(
            new StaticDescriptorSource(BuiltInWorkflowExecutorDescriptors.JsonTransform));
        services.AddWorkflowExecutorCoreServices();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var exception = Assert.Throws<InvalidOperationException>(
            scope.ServiceProvider.GetRequiredService<IWorkflowExecutorInvoker>);

        Assert.Contains("missing runnable implementation", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(WorkflowExecutorIds.JsonTransform.Value, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CoreServicesRejectMismatchedLegacyDescriptorAndImplementation()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IWorkflowExecutorDescriptorSource>(
            new StaticDescriptorSource(BuiltInWorkflowExecutorDescriptors.JsonTransform));
        services.AddScoped<IWorkflowExecutor, MismatchedLegacyWorkflowExecutor>();
        services.AddWorkflowExecutorCoreServices();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var exception = Assert.Throws<InvalidOperationException>(
            scope.ServiceProvider.GetRequiredService<IWorkflowExecutorInvoker>);

        Assert.Contains("does not match", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(WorkflowExecutorIds.JsonTransform.Value, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CoreServicesRejectImplementationWithoutAuthoritativeDescriptor()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IWorkflowExecutorDescriptorSource>(
            new StaticDescriptorSource(BuiltInWorkflowExecutorDescriptors.JsonTransform));
        services.AddScoped<IWorkflowExecutor, UnknownLegacyWorkflowExecutor>();
        services.AddWorkflowExecutorCoreServices();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var exception = Assert.Throws<InvalidOperationException>(
            scope.ServiceProvider.GetRequiredService<IWorkflowExecutorInvoker>);

        Assert.Contains("does not match", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(WorkflowExecutorIds.MarkdownRender.Value, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DescriptorOnlyContributionPreservesPlannedExecutorWithoutImplementation()
    {
        var services = new ServiceCollection();
        var planned = Assert.Single(BuiltInWorkflowExecutorDescriptors.Planned);
        services.AddWorkflowExecutorDescriptorContribution(planned);
        services.AddWorkflowExecutorCoreServices();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var catalog = scope.ServiceProvider.GetRequiredService<IWorkflowExecutorCatalog>();
        var descriptor = catalog.GetRequiredExecutor(planned.Id);

        Assert.Equal(planned, descriptor);
        Assert.False(descriptor.CanExecute);
        Assert.Empty(scope.ServiceProvider.GetRequiredService<IEnumerable<IWorkflowExecutor>>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IWorkflowExecutorInvoker>());
    }

    public sealed class ScopedExecutorDependency
    {
        public Guid InstanceId { get; } = Guid.NewGuid();
    }

    public sealed class ScopedWorkflowExecutor(ScopedExecutorDependency dependency) : IWorkflowExecutor
    {
        public ScopedExecutorDependency Dependency { get; } = dependency;

        public WorkflowExecutorDescriptor Descriptor => BuiltInWorkflowExecutorDescriptors.JsonTransform;

        public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
            WorkflowExecutorExecutionContext context,
            WorkflowNodeInput input,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    public sealed class ThrowingWorkflowExecutor : IWorkflowExecutor
    {
        public ThrowingWorkflowExecutor()
        {
            throw new InvalidOperationException("Executor activation must be deferred until invoker resolution.");
        }

        public WorkflowExecutorDescriptor Descriptor => BuiltInWorkflowExecutorDescriptors.JsonTransform;

        public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
            WorkflowExecutorExecutionContext context,
            WorkflowNodeInput input,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    public sealed class FirstDuplicateWorkflowExecutor : IWorkflowExecutor
    {
        public WorkflowExecutorDescriptor Descriptor => BuiltInWorkflowExecutorDescriptors.JsonTransform;

        public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
            WorkflowExecutorExecutionContext context,
            WorkflowNodeInput input,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    public sealed class SecondDuplicateWorkflowExecutor : IWorkflowExecutor
    {
        public WorkflowExecutorDescriptor Descriptor => BuiltInWorkflowExecutorDescriptors.JsonTransform;

        public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
            WorkflowExecutorExecutionContext context,
            WorkflowNodeInput input,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    public sealed class LegacyDuplicateWorkflowExecutor : IWorkflowExecutor
    {
        public WorkflowExecutorDescriptor Descriptor => BuiltInWorkflowExecutorDescriptors.JsonTransform;

        public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
            WorkflowExecutorExecutionContext context,
            WorkflowNodeInput input,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    public sealed class MismatchedLegacyWorkflowExecutor : IWorkflowExecutor
    {
        public WorkflowExecutorDescriptor Descriptor { get; } = BuiltInWorkflowExecutorDescriptors.JsonTransform with
        {
            Name = "Mismatched JSON transform"
        };

        public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
            WorkflowExecutorExecutionContext context,
            WorkflowNodeInput input,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    public sealed class UnknownLegacyWorkflowExecutor : IWorkflowExecutor
    {
        public WorkflowExecutorDescriptor Descriptor => BuiltInWorkflowExecutorDescriptors.MarkdownRender;

        public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
            WorkflowExecutorExecutionContext context,
            WorkflowNodeInput input,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class StaticDescriptorSource(WorkflowExecutorDescriptor descriptor) : IWorkflowExecutorDescriptorSource
    {
        public IEnumerable<WorkflowExecutorDescriptor> ListExecutorDescriptors()
        {
            yield return descriptor;
        }
    }
}
