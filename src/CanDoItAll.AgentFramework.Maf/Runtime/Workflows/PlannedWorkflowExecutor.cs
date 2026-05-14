using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Maf;

public sealed class PlannedWorkflowExecutor(WorkflowExecutorDescriptor descriptor) : IWorkflowExecutor
{
    public WorkflowExecutorDescriptor Descriptor { get; } = descriptor;

    public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
        WorkflowExecutorExecutionContext context,
        WorkflowNodeInput input,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException($"Workflow executor '{Descriptor.Id}' is planned but not implemented in this bundle.");
    }
}

