using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed class WorkflowExecutorRuntimeAvailabilityCatalog(
    IWorkflowExecutorCatalog catalog,
    IEnumerable<IWorkflowExecutor> executors)
    : IWorkflowExecutorRuntimeAvailabilityCatalog
{
    public async Task<IReadOnlyList<WorkflowExecutorDescriptor>> ListExecutorsAsync(
        CancellationToken cancellationToken = default)
    {
        Dictionary<WorkflowExecutorId, IWorkflowExecutorAvailabilityEvaluator> evaluatorsById = executors
            .OfType<IWorkflowExecutorAvailabilityEvaluator>()
            .ToDictionary(evaluator => evaluator.ExecutorId);
        var descriptors = new List<WorkflowExecutorDescriptor>();
        foreach (WorkflowExecutorDescriptor descriptor in catalog.ListExecutors())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!evaluatorsById.TryGetValue(descriptor.Id, out IWorkflowExecutorAvailabilityEvaluator? evaluator))
            {
                descriptors.Add(descriptor);
                continue;
            }

            WorkflowExecutorAvailabilityDescriptor availability =
                await evaluator.EvaluateAvailabilityAsync(cancellationToken);
            descriptors.Add(descriptor with { Availability = availability });
        }

        return descriptors;
    }
}
