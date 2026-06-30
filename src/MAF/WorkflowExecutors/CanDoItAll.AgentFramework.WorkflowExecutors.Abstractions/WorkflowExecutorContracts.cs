using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public interface IWorkflowExecutorCatalog
{
    IReadOnlyList<WorkflowExecutorDescriptor> ListExecutors();

    bool TryGetExecutor(WorkflowExecutorId executorId, out WorkflowExecutorDescriptor descriptor);

    WorkflowExecutorDescriptor GetRequiredExecutor(WorkflowExecutorId executorId);
}

public interface IWorkflowExecutorDescriptorSource
{
    IEnumerable<WorkflowExecutorDescriptor> ListExecutorDescriptors();
}

public interface IWorkflowExecutor
{
    WorkflowExecutorDescriptor Descriptor { get; }

    ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
        WorkflowExecutorExecutionContext context,
        WorkflowNodeInput input,
        CancellationToken cancellationToken = default);
}

public interface IWorkflowExecutorInvoker
{
    ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
        WorkflowDefinition definition,
        WorkflowNode node,
        WorkflowNodeInput input,
        CancellationToken cancellationToken = default);
}

public interface IWorkflowExecutorApprovalGate
{
    ValueTask<WorkflowExecutorApprovalDecision> RequestApprovalAsync(
        WorkflowExecutorApprovalRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record WorkflowExecutorApprovalRequest(
    WorkflowDefinition Definition,
    WorkflowNode Node,
    WorkflowExecutorDescriptor Descriptor,
    string RedactedSettingsSummary);

public sealed record WorkflowExecutorApprovalDecision(
    bool Approved,
    string Message);

public interface IWorkflowLlmComponentInvoker
{
    ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
        WorkflowDefinition definition,
        WorkflowNode node,
        LlmCallComponent component,
        WorkflowNodeInput input,
        CancellationToken cancellationToken = default);
}

public sealed record WorkflowExecutorExecutionContext(
    WorkflowDefinition Definition,
    WorkflowNode Node,
    WorkflowExecutorDescriptor Descriptor,
    string SettingsJson,
    WorkflowExecutorExecutionPolicy Policy)
{
    public WorkflowRunId? RunId { get; init; }

    public string PluginConnectionId { get; init; } = string.Empty;

    public string RedactedSettingsSummary { get; init; } = string.Empty;
}
