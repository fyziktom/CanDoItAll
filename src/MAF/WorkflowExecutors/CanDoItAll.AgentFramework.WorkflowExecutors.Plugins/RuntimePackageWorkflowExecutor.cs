using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.WorkflowExecutors.Plugins;

public sealed class RuntimePackageWorkflowExecutor(
    IWorkflowExecutor inner,
    WorkflowExecutorDescriptor descriptor) : IWorkflowExecutor
{
    public WorkflowExecutorDescriptor Descriptor { get; } = descriptor;

    public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
        WorkflowExecutorExecutionContext context,
        WorkflowNodeInput input,
        CancellationToken cancellationToken = default)
        => inner.ExecuteAsync(context, input, cancellationToken);
}
