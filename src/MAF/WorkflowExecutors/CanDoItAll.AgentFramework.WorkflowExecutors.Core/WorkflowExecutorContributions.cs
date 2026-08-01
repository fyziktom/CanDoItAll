using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.AgentFramework.Core;

public static class WorkflowExecutorContributionServiceCollectionExtensions
{
    public static IServiceCollection AddWorkflowExecutorContribution<TExecutor>(
        this IServiceCollection services,
        WorkflowExecutorDescriptor descriptor,
        ServiceLifetime lifetime)
        where TExecutor : class, IWorkflowExecutor
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(descriptor);
        if (!descriptor.CanExecute)
        {
            throw new ArgumentException(
                $"Workflow executor contribution '{descriptor.Id}' must be runnable.",
                nameof(descriptor));
        }

        var registration = services
            .Where(service => service.ServiceType == typeof(WorkflowExecutorDescriptorRegistration<TExecutor>))
            .Select(service => service.ImplementationInstance)
            .OfType<WorkflowExecutorDescriptorRegistration<TExecutor>>()
            .SingleOrDefault();
        if (registration is not null)
        {
            if (registration.Descriptor != descriptor)
            {
                throw new InvalidOperationException(
                    $"Workflow executor type '{typeof(TExecutor).FullName}' is already registered with different metadata.");
            }

            return services;
        }

        services.TryAdd(ServiceDescriptor.Describe(typeof(TExecutor), typeof(TExecutor), lifetime));
        services.AddSingleton(new WorkflowExecutorDescriptorRegistration<TExecutor>(descriptor));
        services.TryAddEnumerable(ServiceDescriptor.Describe(
            typeof(IWorkflowExecutor),
            typeof(WorkflowExecutorImplementation<TExecutor>),
            lifetime));
        services.AddSingleton<IWorkflowExecutorContribution>(
            new DescriptorOnlyWorkflowExecutorContribution(descriptor));

        return services;
    }

    public static IServiceCollection AddWorkflowExecutorDescriptorContribution(
        this IServiceCollection services,
        WorkflowExecutorDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(descriptor);

        var existing = services
            .Where(service => service.ServiceType == typeof(IWorkflowExecutorContribution))
            .Select(service => service.ImplementationInstance)
            .OfType<DescriptorOnlyWorkflowExecutorContribution>()
            .FirstOrDefault(contribution => contribution.Descriptor.Id == descriptor.Id);
        if (existing is not null)
        {
            if (existing.Descriptor != descriptor)
            {
                throw new InvalidOperationException($"Workflow executor descriptor contribution '{descriptor.Id}' is already registered with different metadata.");
            }

            return services;
        }

        services.AddSingleton<IWorkflowExecutorContribution>(
            new DescriptorOnlyWorkflowExecutorContribution(descriptor));
        return services;
    }
}

internal sealed class WorkflowExecutorDescriptorRegistration<TExecutor>(
    WorkflowExecutorDescriptor descriptor)
    where TExecutor : class, IWorkflowExecutor
{
    public WorkflowExecutorDescriptor Descriptor { get; } = descriptor;
}

internal sealed class WorkflowExecutorImplementation<TExecutor> : IWorkflowExecutor
    where TExecutor : class, IWorkflowExecutor
{
    private readonly TExecutor executor;

    public WorkflowExecutorImplementation(
        TExecutor executor,
        WorkflowExecutorDescriptorRegistration<TExecutor> registration)
    {
        ArgumentNullException.ThrowIfNull(executor);
        ArgumentNullException.ThrowIfNull(registration);

        this.executor = executor;
        Descriptor = registration.Descriptor;
        var implementationDescriptor = executor.Descriptor with
        {
            Availability = Descriptor.Availability
        };
        if (implementationDescriptor != Descriptor)
        {
            throw new InvalidOperationException(
                $"Workflow executor implementation descriptor does not match the authoritative contribution descriptor for id '{Descriptor.Id}'.");
        }
    }

    public WorkflowExecutorDescriptor Descriptor { get; }

    public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
        WorkflowExecutorExecutionContext context,
        WorkflowNodeInput input,
        CancellationToken cancellationToken = default)
    {
        var availability = executor.Descriptor.Availability;
        if (!availability.IsRunnable)
        {
            throw WorkflowExecutorFailureDiagnosticMapper.CreateUnavailableException(
                context.Definition,
                context.Node,
                executor.Descriptor);
        }

        return executor.ExecuteAsync(context, input, cancellationToken);
    }
}

internal sealed class DescriptorOnlyWorkflowExecutorContribution(
    WorkflowExecutorDescriptor descriptor) : IWorkflowExecutorContribution
{
    public WorkflowExecutorDescriptor Descriptor { get; } = descriptor;
}

internal sealed class WorkflowExecutorContributionSet
{
    public WorkflowExecutorContributionSet(
        IEnumerable<IWorkflowExecutorContribution> contributions,
        IEnumerable<IWorkflowExecutorDescriptorSource> descriptorSources)
    {
        ArgumentNullException.ThrowIfNull(contributions);
        ArgumentNullException.ThrowIfNull(descriptorSources);

        var descriptorArray = contributions
            .Select(contribution => contribution.Descriptor)
            .Concat(descriptorSources.SelectMany(source => source.ListExecutorDescriptors()))
            .ToArray();

        ThrowIfDuplicateDescriptors(descriptorArray);
        Descriptors = descriptorArray;
    }

    public IReadOnlyList<WorkflowExecutorDescriptor> Descriptors { get; }

    public IReadOnlyList<IWorkflowExecutor> ValidateImplementations(
        IEnumerable<IWorkflowExecutor> implementations)
    {
        ArgumentNullException.ThrowIfNull(implementations);

        var implementationArray = implementations.ToArray();
        ThrowIfDuplicateImplementations(implementationArray);
        ThrowIfImplementationDescriptorDoesNotMatch(Descriptors, implementationArray);
        ThrowIfRunnableImplementationIsMissing(Descriptors, implementationArray);
        return implementationArray;
    }

    private static void ThrowIfDuplicateDescriptors(
        IReadOnlyCollection<WorkflowExecutorDescriptor> descriptors)
    {
        var duplicateIds = descriptors
            .GroupBy(descriptor => descriptor.Id)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key.Value)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (duplicateIds.Length > 0)
        {
            throw new InvalidOperationException($"Workflow executor contribution set contains duplicate descriptor id(s): {string.Join(", ", duplicateIds)}.");
        }
    }

    private static void ThrowIfDuplicateImplementations(
        IReadOnlyCollection<IWorkflowExecutor> implementations)
    {
        var duplicateIds = implementations
            .GroupBy(implementation => implementation.Descriptor.Id)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key.Value)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (duplicateIds.Length > 0)
        {
            throw new InvalidOperationException($"Workflow executor contribution set contains duplicate implementation id(s): {string.Join(", ", duplicateIds)}.");
        }
    }

    private static void ThrowIfImplementationDescriptorDoesNotMatch(
        IReadOnlyCollection<WorkflowExecutorDescriptor> descriptors,
        IReadOnlyCollection<IWorkflowExecutor> implementations)
    {
        var descriptorsById = descriptors.ToDictionary(descriptor => descriptor.Id);
        var mismatchedIds = implementations
            .Where(implementation =>
                !descriptorsById.TryGetValue(implementation.Descriptor.Id, out var descriptor) ||
                descriptor != implementation.Descriptor)
            .Select(implementation => implementation.Descriptor.Id.Value)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (mismatchedIds.Length > 0)
        {
            throw new InvalidOperationException($"Workflow executor implementation descriptor does not match the authoritative contribution descriptor for id(s): {string.Join(", ", mismatchedIds)}.");
        }
    }

    private static void ThrowIfRunnableImplementationIsMissing(
        IReadOnlyCollection<WorkflowExecutorDescriptor> descriptors,
        IReadOnlyCollection<IWorkflowExecutor> implementations)
    {
        var implementationIds = implementations
            .Select(implementation => implementation.Descriptor.Id)
            .ToHashSet();
        var missingIds = descriptors
            .Where(descriptor => descriptor.CanExecute && !implementationIds.Contains(descriptor.Id))
            .Select(descriptor => descriptor.Id.Value)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (missingIds.Length > 0)
        {
            throw new InvalidOperationException($"Workflow executor contribution set is missing runnable implementation(s): {string.Join(", ", missingIds)}.");
        }
    }
}
