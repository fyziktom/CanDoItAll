using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed class WorkflowExecutorCatalog : IWorkflowExecutorCatalog
{
    private readonly IReadOnlyList<WorkflowExecutorDescriptor> descriptors;
    private readonly IReadOnlyDictionary<WorkflowExecutorId, WorkflowExecutorDescriptor> descriptorsById;

    public WorkflowExecutorCatalog(IEnumerable<IWorkflowExecutor> executors)
        : this(ResolveDescriptors(executors))
    {
    }

    private WorkflowExecutorCatalog(IEnumerable<WorkflowExecutorDescriptor> descriptors)
    {
        ArgumentNullException.ThrowIfNull(descriptors);

        var resolvedDescriptors = descriptors.ToArray();
        var duplicateIds = resolvedDescriptors
            .GroupBy(descriptor => descriptor.Id)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key.Value)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (duplicateIds.Length > 0)
        {
            throw new InvalidOperationException($"Workflow executor catalog contains duplicate executor id(s): {string.Join(", ", duplicateIds)}.");
        }

        this.descriptors = resolvedDescriptors
            .OrderBy(descriptor => descriptor.Category)
            .ThenBy(descriptor => descriptor.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        descriptorsById = resolvedDescriptors.ToDictionary(descriptor => descriptor.Id);
    }

    public static WorkflowExecutorCatalog FromDescriptors(IEnumerable<WorkflowExecutorDescriptor> descriptors)
        => new(descriptors);

    public static WorkflowExecutorCatalog FromDescriptorSources(IEnumerable<IWorkflowExecutorDescriptorSource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        return new WorkflowExecutorCatalog(sources.SelectMany(source => source.ListExecutorDescriptors()));
    }

    public IReadOnlyList<WorkflowExecutorDescriptor> ListExecutors() => descriptors;

    public bool TryGetExecutor(WorkflowExecutorId executorId, out WorkflowExecutorDescriptor descriptor)
        => descriptorsById.TryGetValue(executorId, out descriptor!);

    public WorkflowExecutorDescriptor GetRequiredExecutor(WorkflowExecutorId executorId)
    {
        if (TryGetExecutor(executorId, out var descriptor))
        {
            return descriptor;
        }

        throw new InvalidOperationException($"Workflow executor '{executorId}' is not registered.");
    }

    private static IEnumerable<WorkflowExecutorDescriptor> ResolveDescriptors(IEnumerable<IWorkflowExecutor> executors)
    {
        ArgumentNullException.ThrowIfNull(executors);
        return executors.Select(executor => executor.Descriptor);
    }
}
