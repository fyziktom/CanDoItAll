using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.AgentFramework.Core;

public static class WorkflowExecutorFailureDiagnosticMapper
{
    public const string ExceptionDataKey = "CanDoItAll.WorkflowExecutorFailureDiagnostics";

    public static InvalidOperationException CreateMissingExecutorIdException(
        WorkflowDefinition definition,
        WorkflowNode node)
        => Attach(
            new InvalidOperationException($"Workflow executor node '{node.Id}' does not specify an executor id."),
            CreateDiagnostic(
                definition,
                node,
                executorId: null,
                descriptor: null,
                WorkflowFailureKind.Executor,
                WorkflowFailureRetryability.RetryableAfterRepair,
                $"Workflow executor node '{node.Id}' does not specify an executor id.",
                "Select a registered workflow executor for the node before running the workflow.",
                $"Workflow executor node '{node.Id}' does not specify an executor id."));

    public static InvalidOperationException CreateExecutorNotRegisteredException(
        WorkflowDefinition definition,
        WorkflowNode node,
        WorkflowExecutorId executorId)
        => Attach(
            new InvalidOperationException($"Workflow executor '{executorId}' is not registered for node '{node.Id}'."),
            CreateDiagnostic(
                definition,
                node,
                executorId,
                descriptor: null,
                WorkflowFailureKind.Executor,
                WorkflowFailureRetryability.RetryableAfterRepair,
                $"Workflow executor '{executorId}' is not registered for node '{node.Id}'.",
                "Register the executor descriptor source in this host or choose a registered executor.",
                $"Workflow executor '{executorId}' is not registered for node '{node.Id}'."));

    public static WorkflowExecutorUnavailableException CreateUnavailableException(
        WorkflowDefinition definition,
        WorkflowNode node,
        WorkflowExecutorDescriptor descriptor)
    {
        var message = $"Workflow executor '{descriptor.Id}' is not runnable for node '{node.Id}': {descriptor.Availability.Message}";
        return Attach(
            new WorkflowExecutorUnavailableException(
                node.Id,
                descriptor.Id,
                descriptor.Availability,
                message),
            CreateDiagnostic(
                definition,
                node,
                descriptor.Id,
                descriptor,
                WorkflowFailureKind.Executor,
                WorkflowFailureRetryability.RetryableAfterRepair,
                message,
                "Resolve the executor availability issue or choose an available executor.",
                message));
    }

    public static InvalidOperationException CreateMissingImplementationException(
        WorkflowDefinition definition,
        WorkflowNode node,
        WorkflowExecutorDescriptor descriptor)
        => Attach(
            new InvalidOperationException($"Workflow executor '{descriptor.Id}' has a descriptor but no implementation."),
            CreateDiagnostic(
                definition,
                node,
                descriptor.Id,
                descriptor,
                WorkflowFailureKind.Executor,
                WorkflowFailureRetryability.RetryableAfterRepair,
                $"Workflow executor '{descriptor.Id}' has a descriptor but no implementation.",
                "Register an executor implementation for this descriptor or remove the descriptor source.",
                $"Workflow executor '{descriptor.Id}' has a descriptor but no implementation."));

    public static InvalidOperationException CreateInvalidPolicyException(
        WorkflowNodeId nodeId,
        WorkflowExecutorId executorId)
    {
        var message =
            $"Workflow executor policy is invalid for executor '{executorId}' on node '{nodeId}'. " +
            $"TimeoutSeconds must be {WorkflowExecutorPolicyLimits.MinTimeoutSeconds}-{WorkflowExecutorPolicyLimits.MaxTimeoutSeconds}, " +
            $"MaxRetryAttempts must be {WorkflowExecutorPolicyLimits.MinRetryAttempts}-{WorkflowExecutorPolicyLimits.MaxRetryAttempts}, " +
            $"and RetryDelayMilliseconds must be {WorkflowExecutorPolicyLimits.MinRetryDelayMilliseconds}-{WorkflowExecutorPolicyLimits.MaxRetryDelayMilliseconds}.";
        return Attach(
            new InvalidOperationException(message),
            CreatePolicyDiagnostic(nodeId, executorId, message, "Set timeout and retry values within the executor policy limits."));
    }

    public static InvalidOperationException CreateUnsafeRetryPolicyException(
        WorkflowExecutorDescriptor descriptor,
        WorkflowExecutorExecutionPolicy policy,
        WorkflowNodeId nodeId)
    {
        var message = WorkflowExecutorSideEffectPolicy.CreateUnsafeRetryPolicyMessage(descriptor, policy, nodeId);
        return Attach(
            new InvalidOperationException(message),
            CreatePolicyDiagnostic(
                nodeId,
                descriptor.Id,
                message,
                "Set MaxRetryAttempts to 0 or use an executor with an idempotent external side-effect contract.",
                descriptor));
    }

    public static InvalidOperationException CreateApprovalGateMissingException(
        WorkflowDefinition definition,
        WorkflowNode node,
        WorkflowExecutorDescriptor descriptor)
    {
        var message = $"Workflow executor '{descriptor.Id}' on node '{node.Id}' requires approval, but no workflow executor approval gate is registered.";
        return Attach(
            new InvalidOperationException(message),
            CreateDiagnostic(
                definition,
                node,
                descriptor.Id,
                descriptor,
                WorkflowFailureKind.Approval,
                WorkflowFailureRetryability.RetryableAfterRepair,
                message,
                "Register a workflow executor approval gate or choose an executor policy that does not require approval.",
                message));
    }

    public static InvalidOperationException CreateApprovalDeniedException(
        WorkflowDefinition definition,
        WorkflowNode node,
        WorkflowExecutorDescriptor descriptor,
        string deniedMessage)
    {
        var redactedMessage = string.IsNullOrWhiteSpace(deniedMessage)
            ? "Approval was denied."
            : WorkflowExecutorRedaction.RedactText(deniedMessage);
        var message = $"Workflow executor '{descriptor.Id}' on node '{node.Id}' was not approved: {redactedMessage}";
        return Attach(
            new InvalidOperationException(message),
            CreateDiagnostic(
                definition,
                node,
                descriptor.Id,
                descriptor,
                WorkflowFailureKind.Approval,
                WorkflowFailureRetryability.RetryableAfterRepair,
                message,
                "Review the denial reason and retry only after approval can be granted.",
                message));
    }

    public static WorkflowExecutorInvocationException CreateInvocationException(
        WorkflowDefinition definition,
        WorkflowNode node,
        WorkflowExecutorDescriptor descriptor,
        WorkflowExecutorExecutionPolicy policy,
        Exception? innerException)
    {
        var message = $"Workflow executor '{descriptor.Id}' failed on node '{node.Id}' after {policy.MaxRetryAttempts + 1} attempt(s).";
        var exception = new WorkflowExecutorInvocationException(
            node.Id,
            descriptor.Id,
            policy.MaxRetryAttempts + 1,
            policy.TimeoutSeconds,
            message,
            innerException);
        IReadOnlyList<WorkflowFailureDiagnosticEnvelope> existingDiagnostics = innerException is null
            ? []
            : GetDiagnostics(innerException);
        exception.Data[ExceptionDataKey] = existingDiagnostics.Count > 0
            ? existingDiagnostics
            : new[]
            {
                FromExecutionFailure(
                    definition,
                    node,
                    descriptor,
                    innerException ?? exception,
                    policy)
            };
        return exception;
    }

    public static WorkflowFailureDiagnosticEnvelope FromExecutionFailure(
        WorkflowDefinition definition,
        WorkflowNode node,
        WorkflowExecutorDescriptor descriptor,
        Exception exception,
        WorkflowExecutorExecutionPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(exception);

        var (kind, retryability, repairHint) = Classify(exception, policy);
        return CreateDiagnostic(
            definition,
            node,
            descriptor.Id,
            descriptor,
            kind,
            retryability,
            exception.Message,
            repairHint,
            exception.ToString());
    }

    public static TException Attach<TException>(
        TException exception,
        WorkflowFailureDiagnosticEnvelope diagnostic)
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(diagnostic);

        exception.Data[ExceptionDataKey] = new[] { diagnostic };
        return exception;
    }

    public static IReadOnlyList<WorkflowFailureDiagnosticEnvelope> GetDiagnostics(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception.Data[ExceptionDataKey] is IReadOnlyList<WorkflowFailureDiagnosticEnvelope> diagnostics)
        {
            return diagnostics;
        }

        return exception.InnerException is null
            ? []
            : GetDiagnostics(exception.InnerException);
    }

    private static WorkflowFailureDiagnosticEnvelope CreatePolicyDiagnostic(
        WorkflowNodeId nodeId,
        WorkflowExecutorId executorId,
        string message,
        string repairHint,
        WorkflowExecutorDescriptor? descriptor = null)
        => new(
            WorkflowFailureKind.Executor,
            WorkflowFailureRetryability.RetryableAfterRepair,
            message,
            repairHint,
            WorkflowExecutorRedaction.RedactText(message),
            CreateCorrelationId(),
            workflowId: null,
            workflowVersionId: null,
            runId: null,
            nodeId,
            executorId,
            ExecutorSource(executorId, descriptor),
            DateTimeOffset.UtcNow);

    private static WorkflowFailureDiagnosticEnvelope CreateDiagnostic(
        WorkflowDefinition definition,
        WorkflowNode node,
        WorkflowExecutorId? executorId,
        WorkflowExecutorDescriptor? descriptor,
        WorkflowFailureKind kind,
        WorkflowFailureRetryability retryability,
        string message,
        string repairHint,
        string technicalDetail)
        => new(
            kind,
            retryability,
            WorkflowExecutorRedaction.RedactText(message),
            repairHint,
            WorkflowExecutorRedaction.RedactText(technicalDetail),
            CreateCorrelationId(),
            definition.Id,
            definition.VersionId,
            WorkflowExecutorExecutionAuditScope.CurrentRunId,
            node.Id,
            executorId,
            executorId is { } id ? ExecutorSource(id, descriptor) : NodeSource(node),
            DateTimeOffset.UtcNow);

    private static WorkflowFailureSourceContext ExecutorSource(
        WorkflowExecutorId executorId,
        WorkflowExecutorDescriptor? descriptor)
        => new(
            WorkflowFailureSourceKind.Executor,
            executorId.Value,
            descriptor?.Name ?? string.Empty,
            descriptor?.Source.PluginId ?? string.Empty,
            descriptor?.Source.PackageId ?? string.Empty,
            descriptor?.GetType().FullName ?? string.Empty,
            ToolName: string.Empty,
            Operation: "workflow-executor",
            TemplateKey: string.Empty,
            TemplateFile: string.Empty,
            BackendKind: null);

    private static WorkflowFailureSourceContext NodeSource(WorkflowNode node)
        => new(
            WorkflowFailureSourceKind.Node,
            node.Id.Value,
            node.Name,
            PluginId: string.Empty,
            PackageId: string.Empty,
            ExecutorType: string.Empty,
            ToolName: string.Empty,
            Operation: "workflow-executor",
            TemplateKey: string.Empty,
            TemplateFile: string.Empty,
            BackendKind: null);

    private static (WorkflowFailureKind Kind, WorkflowFailureRetryability Retryability, string RepairHint) Classify(
        Exception exception,
        WorkflowExecutorExecutionPolicy policy)
        => exception switch
        {
            TimeoutException => (
                WorkflowFailureKind.Timeout,
                policy.MaxRetryAttempts > 0 ? WorkflowFailureRetryability.Retryable : WorkflowFailureRetryability.Unknown,
                "Increase the executor timeout or retry after the dependency is healthy."),
            OperationCanceledException => (
                WorkflowFailureKind.Cancellation,
                WorkflowFailureRetryability.NotRetryable,
                "Start a new workflow run if execution is still required."),
            WorkflowExecutorPayloadTooLargeException => (
                WorkflowFailureKind.Executor,
                WorkflowFailureRetryability.RetryableAfterRepair,
                "Reduce the executor output payload or store large results as artifacts."),
            WorkflowExecutorUnavailableException => (
                WorkflowFailureKind.Executor,
                WorkflowFailureRetryability.RetryableAfterRepair,
                "Resolve the executor availability issue or choose an available executor."),
            InvalidOperationException => (
                WorkflowFailureKind.Executor,
                WorkflowFailureRetryability.RetryableAfterRepair,
                "Fix the executor settings, policy, or required host registration before retrying."),
            _ => (
                WorkflowFailureKind.Executor,
                WorkflowFailureRetryability.Unknown,
                "Inspect the executor failure details and retry only after the underlying issue is resolved.")
        };

    private static string CreateCorrelationId()
        => $"workflow-executor-{Guid.NewGuid():N}";
}
