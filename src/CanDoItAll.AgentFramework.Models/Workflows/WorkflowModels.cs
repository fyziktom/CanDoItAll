using System.Text.Json.Serialization;

namespace CanDoItAll.AgentFramework.Models;

[JsonConverter(typeof(WorkflowIdJsonConverter))]
public readonly record struct WorkflowId
{
    public WorkflowId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Workflow id cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static WorkflowId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

[JsonConverter(typeof(WorkflowVersionIdJsonConverter))]
public readonly record struct WorkflowVersionId
{
    public WorkflowVersionId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Workflow version id cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static WorkflowVersionId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

[JsonConverter(typeof(WorkflowNodeIdJsonConverter))]
public readonly record struct WorkflowNodeId
{
    public WorkflowNodeId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Workflow node id cannot be empty.", nameof(value));
        }

        Value = value.Trim();
    }

    public string Value { get; }

    public override string ToString() => Value;
}

[JsonConverter(typeof(WorkflowEdgeIdJsonConverter))]
public readonly record struct WorkflowEdgeId
{
    public WorkflowEdgeId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Workflow edge id cannot be empty.", nameof(value));
        }

        Value = value.Trim();
    }

    public string Value { get; }

    public override string ToString() => Value;
}

[JsonConverter(typeof(WorkflowPortIdJsonConverter))]
public readonly record struct WorkflowPortId
{
    public WorkflowPortId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Workflow port id cannot be empty.", nameof(value));
        }

        Value = value.Trim();
    }

    public string Value { get; }

    public override string ToString() => Value;
}

[JsonConverter(typeof(WorkflowComponentIdJsonConverter))]
public readonly record struct WorkflowComponentId
{
    public WorkflowComponentId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Workflow component id cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static WorkflowComponentId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

[JsonConverter(typeof(WorkflowRunIdJsonConverter))]
public readonly record struct WorkflowRunId
{
    public WorkflowRunId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Workflow run id cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static WorkflowRunId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

[JsonConverter(typeof(WorkflowExternalRequestIdJsonConverter))]
public readonly record struct WorkflowExternalRequestId
{
    public WorkflowExternalRequestId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Workflow external request id cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static WorkflowExternalRequestId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

[JsonConverter(typeof(WorkflowArtifactIdJsonConverter))]
public readonly record struct WorkflowArtifactId
{
    public WorkflowArtifactId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Workflow artifact id cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static WorkflowArtifactId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public enum WorkflowLifecycleStatus
{
    Draft,
    Active,
    Suspended,
    Archived
}

public enum WorkflowNodeKind
{
    Start,
    LlmCall,
    Triage,
    StrictLogic,
    Executor,
    Artifact,
    HumanInput,
    AgentStep,
    Subworkflow,
    End
}

public enum WorkflowEdgeKind
{
    Direct,
    Conditional,
    FanOut,
    FanIn
}

public enum WorkflowPortDirection
{
    Input,
    Output
}

public enum WorkflowValueShapeKind
{
    Text,
    Json,
    Object,
    Boolean,
    Number,
    FileReference,
    ArtifactReference
}

public enum WorkflowModality
{
    Text,
    Vision,
    Audio,
    Image,
    Multimodal
}

public enum WorkflowRuntimeBackendKind
{
    InProcess,
    DurableTask,
    AzureFunctions
}

public enum WorkflowRunState
{
    NotStarted,
    Running,
    WaitingForInput,
    Idle,
    Completed,
    Failed,
    Cancelled
}

public enum WorkflowEventKind
{
    Started,
    ExecutorInvoked,
    ExecutorCompleted,
    ExecutorFailed,
    SuperStep,
    Output,
    Warning,
    Error,
    WaitingForInput,
    Completed,
    Cancelled,
    Unknown
}

public enum WorkflowArtifactKind
{
    Text,
    Json,
    File,
    Image,
    Binary,
    ToolReceipt
}

public enum WorkflowExternalRequestKind
{
    HumanInput,
    Approval,
    ToolApproval
}

public enum WorkflowExecutorKind
{
    Human,
    AiAgent,
    Workflow
}

public enum WorkflowValidationIssueCode
{
    MissingName,
    MissingStartNode,
    MissingEndNode,
    EmptyGraph,
    DuplicateNodeId,
    DuplicateEdgeId,
    DisconnectedNode,
    UnknownEdgeEndpoint,
    InvalidComponentReference,
    InvalidExecutorReference,
    InvalidExecutorSettings,
    InvalidExecutionPolicy,
    InvalidProviderModel,
    InvalidWorkflowSettings,
    UnsupportedRuntimeBackend,
    UnsupportedModality,
    UnsupportedNodeKind,
    ShapeMismatch
}

public sealed record WorkflowValueShape(
    WorkflowValueShapeKind Kind,
    string SchemaJson,
    string Description)
{
    public static WorkflowValueShape Text { get; } = new(
        WorkflowValueShapeKind.Text,
        string.Empty,
        "Plain text");
}

public sealed record WorkflowPort(
    WorkflowPortId Id,
    string Name,
    WorkflowPortDirection Direction,
    WorkflowValueShape Shape,
    bool Required);

public sealed record WorkflowModelSettings(
    double? Temperature,
    int? MaxOutputTokens,
    bool RequireJsonOutput,
    string ResponseFormatJsonSchema);

public sealed record LlmCallComponent(
    WorkflowComponentId Id,
    string Name,
    Guid? ProviderProfileId,
    string Model,
    WorkflowModality Modality,
    WorkflowModelSettings ModelSettings,
    string Instructions,
    WorkflowValueShape InputShape,
    WorkflowValueShape ResultShape,
    AgentPermissionsPolicy Permissions,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record WorkflowNodeSettings(
    WorkflowComponentId? ComponentId,
    Guid? AgentId,
    WorkflowId? SubworkflowId,
    WorkflowExternalRequestKind? ExternalRequestKind,
    string Instructions,
    WorkflowValueShape? InputShape,
    WorkflowValueShape? ResultShape)
{
    public WorkflowExecutorId? ExecutorId { get; init; }

    public string ExecutorSettingsJson { get; init; } = string.Empty;

    public WorkflowExecutorExecutionPolicy? ExecutionPolicy { get; init; }
}

public sealed record WorkflowNode(
    WorkflowNodeId Id,
    WorkflowNodeKind Kind,
    string Name,
    IReadOnlyList<WorkflowPort> Ports,
    WorkflowNodeSettings Settings,
    double CanvasX = 0,
    double CanvasY = 0);

public sealed record WorkflowEdge(
    WorkflowEdgeId Id,
    WorkflowNodeId SourceNodeId,
    WorkflowPortId? SourcePortId,
    WorkflowNodeId TargetNodeId,
    WorkflowPortId? TargetPortId,
    WorkflowEdgeKind Kind,
    string ConditionExpression);

public sealed record WorkflowGraph(
    WorkflowNodeId StartNodeId,
    IReadOnlyList<WorkflowNode> Nodes,
    IReadOnlyList<WorkflowEdge> Edges);

public sealed record WorkflowRuntimePolicy(
    WorkflowRuntimeBackendKind PreferredBackend,
    bool AllowInProcessPreviewRuns,
    bool RequireDurableProductionRuns,
    bool ExposeAzureFunctionsStatusEndpoint,
    bool ExposeAzureFunctionsMcpTool);

public sealed record WorkflowDefinition(
    WorkflowId Id,
    WorkflowVersionId VersionId,
    string Name,
    string Description,
    WorkflowLifecycleStatus Status,
    WorkflowGraph Graph,
    WorkflowRuntimePolicy RuntimePolicy,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record WorkflowValidationIssue(
    WorkflowValidationIssueCode Code,
    string Message,
    WorkflowNodeId? NodeId = null,
    WorkflowEdgeId? EdgeId = null);

public sealed record WorkflowValidationResult(IReadOnlyList<WorkflowValidationIssue> Issues)
{
    public bool Succeeded => Issues.Count == 0;

    public static WorkflowValidationResult Success { get; } = new([]);
}

public sealed record WorkflowRuntimeBackendDescriptor(
    WorkflowRuntimeBackendKind Kind,
    string Name,
    bool IsDurable,
    bool SupportsStreaming,
    bool SupportsExternalRequests,
    bool SupportsDashboardObservability,
    string OperationalNotes);

public sealed record WorkflowRunStartRequest(
    WorkflowId WorkflowId,
    WorkflowVersionId VersionId,
    string InputJson,
    WorkflowRuntimeBackendKind? RequestedBackend,
    Guid? SourceProcessRunId,
    Guid? SourceProcessAssignmentId);

public sealed record WorkflowRunSnapshot(
    WorkflowRunId RunId,
    WorkflowId WorkflowId,
    WorkflowVersionId VersionId,
    WorkflowRunState State,
    WorkflowRuntimeBackendKind Backend,
    string BackendRunId,
    string Summary,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record WorkflowEventRecord(
    Guid Id,
    WorkflowRunId RunId,
    WorkflowEventKind Kind,
    WorkflowNodeId? NodeId,
    string Message,
    string PayloadJson,
    DateTimeOffset CreatedAtUtc);

public sealed record WorkflowExternalRequestRecord(
    WorkflowExternalRequestId Id,
    WorkflowRunId RunId,
    WorkflowExternalRequestKind Kind,
    WorkflowNodeId NodeId,
    string EventName,
    string RequestJson,
    string ResponseJson,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? RespondedAtUtc);

public sealed record WorkflowArtifactRecord(
    WorkflowArtifactId Id,
    WorkflowRunId RunId,
    WorkflowArtifactKind Kind,
    WorkflowNodeId? NodeId,
    string Name,
    string ContentType,
    string StoragePath,
    string Summary,
    DateTimeOffset CreatedAtUtc);

public sealed record WorkflowNodeInput(string PayloadJson);

public sealed record WorkflowNodeExecutionResult(
    WorkflowNodeId NodeId,
    string PayloadJson,
    WorkflowValueShape ResultShape);
