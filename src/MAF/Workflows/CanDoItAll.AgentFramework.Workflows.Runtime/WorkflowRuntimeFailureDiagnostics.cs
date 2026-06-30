using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.AgentFramework.Core;

public static class WorkflowRuntimeFailureDiagnosticMapper
{
    public const string ExceptionDataKey = "CanDoItAll.WorkflowRuntimeFailureDiagnostics";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static InvalidOperationException CreateBackendUnavailableException(
        WorkflowDefinition definition,
        WorkflowRuntimeBackendKind backend,
        string message)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return Attach(
            new InvalidOperationException(message),
            BackendUnavailable(definition, backend, message));
    }

    public static InvalidOperationException CreateDurableBackendRequiredException(
        WorkflowDefinition definition,
        WorkflowRuntimeBackendKind backend,
        string message)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return Attach(
            new InvalidOperationException(message),
            new WorkflowFailureDiagnosticEnvelope(
                WorkflowFailureKind.Runtime,
                WorkflowFailureRetryability.RetryableAfterRepair,
                message,
                "Select a durable runtime backend or explicitly allow in-process preview runs for this workflow.",
                WorkflowExecutorRedaction.RedactText(message),
                CreateCorrelationId(),
                definition.Id,
                definition.VersionId,
                runId: null,
                nodeId: null,
                executorId: null,
                RuntimeBackendSource(backend),
                DateTimeOffset.UtcNow));
    }

    public static WorkflowFailureDiagnosticEnvelope BackendUnavailable(
        WorkflowDefinition definition,
        WorkflowRuntimeBackendKind backend,
        string message)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return new WorkflowFailureDiagnosticEnvelope(
            WorkflowFailureKind.Runtime,
            WorkflowFailureRetryability.RetryableAfterRepair,
            message,
            "Register the requested workflow runtime backend or choose a registered backend before starting the workflow.",
            WorkflowExecutorRedaction.RedactText(message),
            CreateCorrelationId(),
            definition.Id,
            definition.VersionId,
            runId: null,
            nodeId: null,
            executorId: null,
            RuntimeBackendSource(backend),
            DateTimeOffset.UtcNow);
    }

    public static WorkflowFailureDiagnosticEnvelope Cancelled(
        WorkflowRunSnapshot run,
        DateTimeOffset occurredAtUtc)
    {
        ArgumentNullException.ThrowIfNull(run);

        return new WorkflowFailureDiagnosticEnvelope(
            WorkflowFailureKind.Cancellation,
            WorkflowFailureRetryability.NotRetryable,
            "Workflow run was cancelled.",
            "Start a new workflow run if execution is still required.",
            "Workflow runtime cancellation was requested.",
            CreateCorrelationId(),
            run.WorkflowId,
            run.VersionId,
            run.RunId,
            nodeId: null,
            executorId: null,
            RuntimeBackendSource(run.Backend),
            occurredAtUtc);
    }

    public static WorkflowFailureDiagnosticEnvelope ApprovalDenied(
        WorkflowRunSnapshot run,
        WorkflowExternalRequestRecord request,
        string message,
        DateTimeOffset occurredAtUtc)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return new WorkflowFailureDiagnosticEnvelope(
            WorkflowFailureKind.Approval,
            WorkflowFailureRetryability.RetryableAfterRepair,
            message,
            "Review the denial reason and restart the workflow only after the requested approval can be granted.",
            WorkflowExecutorRedaction.RedactText(message),
            CreateCorrelationId(),
            run.WorkflowId,
            run.VersionId,
            run.RunId,
            request.NodeId,
            executorId: null,
            new WorkflowFailureSourceContext(
                WorkflowFailureSourceKind.Workflow,
                request.Id.Value.ToString("D"),
                request.EventName,
                PluginId: string.Empty,
                PackageId: string.Empty,
                ExecutorType: string.Empty,
                ToolName: string.Empty,
                Operation: "external-request-response",
                TemplateKey: string.Empty,
                TemplateFile: string.Empty,
                run.Backend),
            occurredAtUtc);
    }

    public static IReadOnlyList<WorkflowFailureDiagnosticEnvelope> GetDiagnostics(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception.Data[ExceptionDataKey] is IReadOnlyList<WorkflowFailureDiagnosticEnvelope> diagnostics
            ? diagnostics
            : [];
    }

    public static string Serialize(WorkflowFailureDiagnosticEnvelope diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);

        return JsonSerializer.Serialize(diagnostic, JsonOptions);
    }

    private static InvalidOperationException Attach(
        InvalidOperationException exception,
        WorkflowFailureDiagnosticEnvelope diagnostic)
    {
        exception.Data[ExceptionDataKey] = new[] { diagnostic };
        return exception;
    }

    private static WorkflowFailureSourceContext RuntimeBackendSource(WorkflowRuntimeBackendKind backend)
        => new(
            WorkflowFailureSourceKind.RuntimeBackend,
            backend.ToString(),
            backend.ToString(),
            PluginId: string.Empty,
            PackageId: string.Empty,
            ExecutorType: string.Empty,
            ToolName: string.Empty,
            Operation: "workflow-runtime",
            TemplateKey: string.Empty,
            TemplateFile: string.Empty,
            backend);

    private static string CreateCorrelationId()
        => $"workflow-runtime-{Guid.NewGuid():N}";
}
