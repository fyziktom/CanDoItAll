using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
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

public interface IWorkflowExecutorAvailabilityEvaluator
{
    WorkflowExecutorId ExecutorId { get; }

    ValueTask<WorkflowExecutorAvailabilityDescriptor> EvaluateAvailabilityAsync(
        CancellationToken cancellationToken = default);
}

public interface IWorkflowExecutorRuntimeAvailabilityCatalog
{
    Task<IReadOnlyList<WorkflowExecutorDescriptor>> ListExecutorsAsync(
        CancellationToken cancellationToken = default);
}

public interface IWorkflowExecutorInvoker
{
    ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
        WorkflowDefinition definition,
        WorkflowNode node,
        WorkflowNodeInput input,
        CancellationToken cancellationToken = default);

    ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
        WorkflowDefinition definition,
        WorkflowNode node,
        WorkflowNodeInput input,
        WorkflowExecutorInvocationContext invocationContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invocationContext);
        return ExecuteAsync(definition, node, input, cancellationToken);
    }
}

public readonly record struct WorkflowExecutorApprovalRequestId
{
    [JsonConstructor]
    public WorkflowExecutorApprovalRequestId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException("Workflow executor approval request id cannot contain leading or trailing whitespace.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public static WorkflowExecutorApprovalRequestId New() => new(Guid.NewGuid().ToString("N"));

    public override string ToString() => Value;
}

public readonly record struct WorkflowExecutorApprovalToken
{
    [JsonConstructor]
    public WorkflowExecutorApprovalToken(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException("Workflow executor approval token cannot contain leading or trailing whitespace.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public static WorkflowExecutorApprovalToken New()
        => new(Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32)));

    public bool FixedTimeEquals(WorkflowExecutorApprovalToken other)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(Value);
        var presentedBytes = Encoding.UTF8.GetBytes(other.Value);
        return expectedBytes.Length == presentedBytes.Length &&
            CryptographicOperations.FixedTimeEquals(expectedBytes, presentedBytes);
    }

    public override string ToString() => Value;
}

public sealed record WorkflowExecutorApprovalAuthorization(
    WorkflowExecutorApprovalRequestId RequestId,
    WorkflowExecutorApprovalToken ExpectedToken,
    WorkflowExecutorApprovalToken PresentedToken,
    WorkflowRunId RunId,
    WorkflowId WorkflowId,
    WorkflowVersionId WorkflowVersionId,
    WorkflowNodeId NodeId,
    WorkflowExecutorId ExecutorId,
    WorkflowExecutorCapabilityFlags RequiredCapabilities,
    WorkflowExecutorApprovalRequirement ApprovalRequirement,
    WorkflowExecutorInputHash InputHash,
    WorkflowExternalResponseAuthorization ExternalResponseAuthorization,
    bool Approved,
    string Message);

public sealed record WorkflowExecutorInvocationContext
{
    public static WorkflowExecutorInvocationContext Empty { get; } = new();

    public WorkflowExecutorApprovalAuthorization? ApprovalAuthorization { get; init; }

    public WorkflowExternalResponseAuthorization? ExternalResponseAuthorization { get; init; }

    public WorkflowExternalRequestId? CausationRequestId { get; init; }

    public WorkflowExternalRequestVersion? CausationRequestVersion { get; init; }

    public WorkflowExternalResponseOperationId? CausationOperationId { get; init; }

    public WorkflowExecutorInvocationGeneration InvocationGeneration { get; init; } =
        WorkflowExecutorInvocationGeneration.Initial;

    public WorkflowExecutorInvocationIdempotencyKey? IdempotencyKey { get; init; }
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

    public WorkflowExternalRequestId? CausationRequestId { get; init; }

    public WorkflowExternalRequestVersion? CausationRequestVersion { get; init; }

    public WorkflowExternalResponseOperationId? CausationOperationId { get; init; }

    public WorkflowExecutorInvocationGeneration InvocationGeneration { get; init; } =
        WorkflowExecutorInvocationGeneration.Initial;

    public WorkflowExecutorInvocationIdempotencyKey? IdempotencyKey { get; init; }
}
