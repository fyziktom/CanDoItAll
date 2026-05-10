using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed class WorkflowProcessExecutorBridge(IWorkflowRuntimeManager runtimeManager) : IWorkflowProcessExecutorBridge
{
    public Task<WorkflowRunSnapshot> StartForProcessAssignmentAsync(
        WorkflowDefinition definition,
        WorkflowRunStartRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(request);

        return runtimeManager.StartAsync(definition, request, cancellationToken);
    }
}
