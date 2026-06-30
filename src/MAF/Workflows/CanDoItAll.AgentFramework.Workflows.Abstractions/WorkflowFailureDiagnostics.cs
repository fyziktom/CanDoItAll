using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Workflows.Abstractions;

public enum WorkflowFailureKind
{
    Validation,
    Runtime,
    Executor,
    Plugin,
    ExternalTool,
    Template,
    Persistence,
    Artifact,
    Approval,
    Timeout,
    Cancellation,
    Unknown
}

public enum WorkflowFailureRetryability
{
    Unknown,
    NotRetryable,
    Retryable,
    RetryableAfterRepair
}

public enum WorkflowFailureSourceKind
{
    Workflow,
    Node,
    Executor,
    Plugin,
    ExternalTool,
    Template,
    RuntimeBackend,
    Persistence,
    Api,
    Ui,
    Workbench
}

public sealed record WorkflowFailureSourceContext(
    WorkflowFailureSourceKind Kind,
    string SourceId,
    string SourceName,
    string PluginId,
    string PackageId,
    string ExecutorType,
    string ToolName,
    string Operation,
    string TemplateKey,
    string TemplateFile,
    WorkflowRuntimeBackendKind? BackendKind)
{
    public static WorkflowFailureSourceContext ForWorkflow() => new(
        WorkflowFailureSourceKind.Workflow,
        SourceId: string.Empty,
        SourceName: string.Empty,
        PluginId: string.Empty,
        PackageId: string.Empty,
        ExecutorType: string.Empty,
        ToolName: string.Empty,
        Operation: string.Empty,
        TemplateKey: string.Empty,
        TemplateFile: string.Empty,
        BackendKind: null);

    public static WorkflowFailureSourceContext ForExecutor(
        WorkflowExecutorId executorId,
        string executorType = "")
        => new(
            WorkflowFailureSourceKind.Executor,
            executorId.Value,
            SourceName: string.Empty,
            PluginId: string.Empty,
            PackageId: string.Empty,
            executorType,
            ToolName: string.Empty,
            Operation: string.Empty,
            TemplateKey: string.Empty,
            TemplateFile: string.Empty,
            BackendKind: null);

    public static WorkflowFailureSourceContext ForTemplate(
        string templateKey,
        string templateFile)
        => new(
            WorkflowFailureSourceKind.Template,
            templateKey,
            SourceName: string.Empty,
            PluginId: string.Empty,
            PackageId: string.Empty,
            ExecutorType: string.Empty,
            ToolName: string.Empty,
            Operation: string.Empty,
            templateKey,
            templateFile,
            BackendKind: null);
}

public sealed record WorkflowFailureDiagnosticEnvelope
{
    [JsonConstructor]
    public WorkflowFailureDiagnosticEnvelope(
        WorkflowFailureKind kind,
        WorkflowFailureRetryability retryability,
        string message,
        string repairHint,
        string redactedTechnicalDetail,
        string correlationId,
        WorkflowId? workflowId,
        WorkflowVersionId? workflowVersionId,
        WorkflowRunId? runId,
        WorkflowNodeId? nodeId,
        WorkflowExecutorId? executorId,
        WorkflowFailureSourceContext? source,
        DateTimeOffset occurredAtUtc)
    {
        Kind = kind;
        Retryability = retryability;
        Message = NormalizeRequired(message, nameof(message));
        RepairHint = NormalizeRequired(repairHint, nameof(repairHint));
        RedactedTechnicalDetail = NormalizeOptional(redactedTechnicalDetail);
        CorrelationId = NormalizeRequired(correlationId, nameof(correlationId));
        WorkflowId = workflowId;
        WorkflowVersionId = workflowVersionId;
        RunId = runId;
        NodeId = nodeId;
        ExecutorId = executorId;
        Source = source ?? WorkflowFailureSourceContext.ForWorkflow();
        OccurredAtUtc = occurredAtUtc;
    }

    public WorkflowFailureKind Kind { get; init; }

    public WorkflowFailureRetryability Retryability { get; init; }

    public string Message { get; init; }

    public string RepairHint { get; init; }

    public string RedactedTechnicalDetail { get; init; }

    public string CorrelationId { get; init; }

    public WorkflowId? WorkflowId { get; init; }

    public WorkflowVersionId? WorkflowVersionId { get; init; }

    public WorkflowRunId? RunId { get; init; }

    public WorkflowNodeId? NodeId { get; init; }

    public WorkflowExecutorId? ExecutorId { get; init; }

    public WorkflowFailureSourceContext Source { get; init; }

    public DateTimeOffset OccurredAtUtc { get; init; }

    public bool HasNodeContext => NodeId is not null;

    public bool HasExecutorContext => ExecutorId is not null;

    public static WorkflowFailureDiagnosticEnvelope Validation(
        string message,
        string repairHint,
        string correlationId,
        WorkflowId? workflowId = null,
        WorkflowNodeId? nodeId = null)
        => new(
            WorkflowFailureKind.Validation,
            WorkflowFailureRetryability.RetryableAfterRepair,
            message,
            repairHint,
            redactedTechnicalDetail: string.Empty,
            correlationId,
            workflowId,
            workflowVersionId: null,
            runId: null,
            nodeId,
            executorId: null,
            WorkflowFailureSourceContext.ForWorkflow(),
            DateTimeOffset.UtcNow);

    private static string NormalizeRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{parameterName} cannot be empty.", parameterName);
        }

        return value.Trim();
    }

    private static string NormalizeOptional(string value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}
