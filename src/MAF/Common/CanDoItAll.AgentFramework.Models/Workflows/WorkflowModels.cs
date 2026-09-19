using System.Text.Json.Serialization;

namespace CanDoItAll.AgentFramework.Models;

/// <summary>
/// Identifier of a workflow definition, stable across all of its versions, written as a GUID string, for example
/// <c>3f2504e0-4f89-11d3-9a0c-0305e82c3301</c>. The workflow operations return it as <c>id</c> or
/// <c>workflowId</c>; Project Structure workflow nodes carry the same identifier. It must not be the empty GUID.
/// Readers also accept an object with a <c>value</c> member that holds the GUID; send the plain string.
/// </summary>
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

/// <summary>
/// Identifier of one stored version of a workflow definition, written as a GUID string. Every save, import and
/// status change of a workflow stores a new version with a new identifier; a run records the exact version it
/// executes. It must not be the empty GUID. Readers also accept an object with a <c>value</c> member that holds the
/// GUID; send the plain string.
/// </summary>
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

/// <summary>
/// Identifier of a node within one workflow definition graph, written as a non-empty string chosen by the author, for
/// example <c>review</c>; surrounding whitespace is removed. It must be unique within the graph and is not unique
/// across workflows. Readers also accept an object with a <c>value</c> member; send the plain string.
/// </summary>
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

/// <summary>
/// Identifier of an edge within one workflow definition graph, written as a non-empty string chosen by the author, for
/// example <c>review-to-end</c>; surrounding whitespace is removed. It must be unique within the graph. Readers also
/// accept an object with a <c>value</c> member; send the plain string.
/// </summary>
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

/// <summary>
/// Identifier of a port of a workflow node, written as a non-empty string chosen by the author, for example
/// <c>input</c>; surrounding whitespace is removed. Readers also accept an object with a <c>value</c> member; send the
/// plain string.
/// </summary>
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

/// <summary>
/// Identifier of an LLM call component, written as a GUID string. It is returned as <c>id</c> by
/// <c>GET /api/workflows/components</c> and referenced by LLM call nodes as <c>settings.componentId</c>. It must not
/// be the empty GUID. Readers also accept an object with a <c>value</c> member; send the plain string.
/// </summary>
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

/// <summary>
/// Identifier of a workflow run, one execution of one workflow version, written as a GUID string. The workflow run
/// operations return it as <c>runId</c>; Project Structure workflow nodes report the run they started. It is not an
/// agent execution run or process run identifier. Readers also accept an object with a <c>value</c> member; send the
/// plain string.
/// </summary>
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

/// <summary>
/// Identifier of a checkpoint recorded for a workflow run, written as a GUID string.
/// </summary>
[JsonConverter(typeof(WorkflowCheckpointIdJsonConverter))]
public readonly record struct WorkflowCheckpointId
{
    public WorkflowCheckpointId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Workflow checkpoint id cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static WorkflowCheckpointId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

/// <summary>
/// Identifier of an external request (a human-input or approval request raised by a waiting run), written as a GUID
/// string. Answer the request with <c>POST /api/workflows/external-requests/{requestId}/response</c>.
/// </summary>
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

/// <summary>
/// Identifier of an artifact recorded by a workflow run, written as a GUID string; read the artifact content with
/// <c>GET /api/workflows/runs/{runId}/artifacts/{artifactId}/content</c>.
/// </summary>
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

/// <summary>
/// Lifecycle status of a stored workflow definition version, as a JSON integer: 0 Draft (being edited; can be
/// test-run but not started in production), 1 Active (published; production starts without a version use the latest
/// Active version), 2 Suspended (temporarily withdrawn; while the current version is Suspended no version can be
/// started), 3 Archived (retired but kept; while the current version is Archived no version can be started).
/// </summary>
public enum WorkflowLifecycleStatus
{
    Draft,
    Active,
    Suspended,
    Archived
}

/// <summary>
/// Kind of a workflow node, as a JSON integer: 0 Start (entry node), 1 LlmCall (calls a model through its LLM call
/// component), 2 Triage, 3 StrictLogic, 4 Executor (runs the executor named by <c>settings.executorId</c>),
/// 5 Artifact, 6 HumanInput (makes the run wait for an external response), 7 AgentStep, 8 Subworkflow, 9 End (exit
/// node; a graph needs at least one). A node of any kind that names an executor runs it. Nodes of the other kinds
/// without an executor pass their input on unchanged; Artifact, AgentStep and Subworkflow nodes without an executor
/// are rejected in Active definitions.
/// </summary>
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

/// <summary>
/// Kind of a workflow edge, as a JSON integer: 0 Direct, 1 Conditional, 2 FanOut (one of several parallel targets of
/// the source node), 3 FanIn (joins parallel branches). How an edge is taken is defined by its <c>routing</c>.
/// </summary>
public enum WorkflowEdgeKind
{
    Direct,
    Conditional,
    FanOut,
    FanIn
}

/// <summary>
/// How an edge route decides whether it is taken, as a JSON integer: 0 Always (unconditional), 1 Predicate (taken
/// when its JSON path condition holds), 2 SwitchCase (one case of a switch; taken when the value at the JSON path
/// equals the expected value), 3 SwitchDefault (the switch branch taken when no case matches; at most one per source
/// node), 4 FanOutSelector (selects fan-out targets by condition). Switch routes cannot be mixed with other routes
/// leaving the same node.
/// </summary>
public enum WorkflowRouteKind
{
    Always,
    Predicate,
    SwitchCase,
    SwitchDefault,
    FanOutSelector
}

/// <summary>
/// Comparison of a route condition, as a JSON integer: 0 Exists, 1 DoesNotExist, 2 Equals, 3 NotEquals, 4 Contains
/// (substring of a string or element of an array), 5 StartsWith, 6 EndsWith (strings only), 7 GreaterThan,
/// 8 GreaterThanOrEqual, 9 LessThan, 10 LessThanOrEqual (numbers only), 11 IsTruthy, 12 IsFalsy. All except Exists,
/// DoesNotExist, IsTruthy and IsFalsy need an expected value.
/// </summary>
public enum WorkflowRouteOperator
{
    Exists,
    DoesNotExist,
    Equals,
    NotEquals,
    Contains,
    StartsWith,
    EndsWith,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
    IsTruthy,
    IsFalsy
}

/// <summary>
/// JSON type the expected value of a route condition must have, as a JSON integer: 0 String, 1 Number, 2 Boolean,
/// 3 Null, 4 Json (any JSON value).
/// </summary>
public enum WorkflowRouteValueKind
{
    String,
    Number,
    Boolean,
    Null,
    Json
}

/// <summary>
/// Direction of a node port, as a JSON integer: 0 Input, 1 Output.
/// </summary>
public enum WorkflowPortDirection
{
    Input,
    Output
}

/// <summary>
/// Kind of value a node consumes or produces, as a JSON integer: 0 Text, 1 Json, 2 Object, 3 Boolean, 4 Number,
/// 5 FileReference, 6 ArtifactReference. An edge must connect an output to an input of the same kind, unless the
/// input kind is Object, which accepts any kind.
/// </summary>
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

/// <summary>
/// Input modality of an LLM call component, as a JSON integer: 0 Text, 1 Vision, 2 Audio, 3 Image, 4 Multimodal.
/// Audio and Image are not supported by the current workflow runtime and fail validation; Vision and Multimodal need a
/// provider that supports vision.
/// </summary>
public enum WorkflowModality
{
    Text,
    Vision,
    Audio,
    Image,
    Multimodal
}

/// <summary>
/// Workflow runtime backend, as a JSON integer: 0 InProcess (runs inside this host; not durable), 1 DurableTask
/// (durable backend), 2 AzureFunctions (durable Azure Functions hosting). Only backends reported as registered and
/// runnable by <c>GET /api/workflows/runtime-backends</c> can run workflows; by default that is InProcess only.
/// </summary>
public enum WorkflowRuntimeBackendKind
{
    InProcess,
    DurableTask,
    AzureFunctions
}

/// <summary>
/// Availability of a runtime backend in this host, as a JSON integer: 0 Registered (can run workflows), 1 Planned
/// (known but not registered in this host), 2 Unavailable.
/// </summary>
public enum WorkflowRuntimeBackendAvailabilityKind
{
    Registered,
    Planned,
    Unavailable
}

/// <summary>
/// State of a workflow run, as a JSON integer: 0 NotStarted, 1 Running, 2 WaitingForInput (an external request must
/// be answered before the run continues), 3 Idle (the runtime reported the run idle without a final state),
/// 4 Completed, 5 Failed, 6 Cancelled. Completed, Failed and Cancelled are final.
/// </summary>
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

/// <summary>
/// Kind of a recorded workflow run event, as a JSON integer (server-sent notifications use the camel-case name
/// instead): 0 Started, 1 ExecutorInvoked, 2 ExecutorCompleted, 3 ExecutorFailed, 4 SuperStep (a runtime step
/// boundary), 5 Output, 6 Warning, 7 Error, 8 WaitingForInput, 9 Completed, 10 Cancelled, 11 Unknown, 12
/// ProviderReadEvidence (internal evidence; left out of the event lists).
/// </summary>
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
    Unknown,
    ProviderReadEvidence
}

/// <summary>
/// Kind of a workflow run artifact, as a JSON integer: 0 Text, 1 Json, 2 File (a workspace file written by the run),
/// 3 Image, 4 Binary, 5 ToolReceipt, 6 PreviewSimulation (output of a simulated node in a preview run).
/// </summary>
public enum WorkflowArtifactKind
{
    Text,
    Json,
    File,
    Image,
    Binary,
    ToolReceipt,
    PreviewSimulation
}

/// <summary>
/// Kind of an external request raised by a waiting run, as a JSON integer: 0 HumanInput (a value that satisfies the
/// request's response schema), 1 Approval and 2 ToolApproval (both answered with <c>approved</c> and an optional
/// <c>message</c>). On a HumanInput node it selects which kind of request the node raises; omitted or null means
/// HumanInput.
/// </summary>
public enum WorkflowExternalRequestKind
{
    HumanInput,
    Approval,
    ToolApproval
}

/// <summary>
/// Why a workflow checkpoint was recorded, as a JSON integer: 0 RuntimeBoundary, 1 SuperStep, 2 WaitingForInput,
/// 3 Completed, 4 Failed, 5 Cancelled.
/// </summary>
public enum WorkflowCheckpointKind
{
    RuntimeBoundary,
    SuperStep,
    WaitingForInput,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// What a workflow checkpoint holds, as a JSON integer: 0 MetadataOnly (bookkeeping; the run cannot resume from it),
/// 1 TrustedRuntimeState (runtime state the host can resume a waiting run from).
/// </summary>
public enum WorkflowCheckpointTrustBoundary
{
    MetadataOnly,
    TrustedRuntimeState
}

/// <summary>
/// Whether a run can resume from a checkpoint, as a JSON integer: 0 NotSupported, 1 BlockedByPolicy, 2 Available.
/// </summary>
public enum WorkflowResumeAvailability
{
    NotSupported,
    BlockedByPolicy,
    Available
}

public enum WorkflowExecutorKind
{
    Human,
    AiAgent,
    Workflow
}

/// <summary>
/// Stable code of a workflow validation issue, as a JSON integer: 0 MissingName, 1 MissingStartNode, 2
/// MissingEndNode, 3 EmptyGraph, 4 DuplicateNodeId, 5 DuplicateEdgeId, 6 DisconnectedNode (not reachable from the
/// start node), 7 UnknownEdgeEndpoint, 8 InvalidComponentReference, 9 InvalidExecutorReference, 10
/// InvalidExecutorSettings, 11 InvalidExecutionPolicy, 12 InvalidProviderModel, 13 InvalidWorkflowSettings, 14
/// InvalidRouteDefinition, 15 UnsupportedRuntimeBackend, 16 UnsupportedModality, 17 UnsupportedNodeKind, 18
/// ShapeMismatch. The start operations report the member name in error codes such as
/// <c>workflows.validation.MissingStartNode</c>.
/// </summary>
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
    InvalidRouteDefinition,
    UnsupportedRuntimeBackend,
    UnsupportedModality,
    UnsupportedNodeKind,
    ShapeMismatch
}

/// <summary>
/// Shape of a value that a node, component or executor consumes or produces. Validation compares the output shape of
/// an edge's source with the input shape of its target.
/// </summary>
/// <param name="Kind">
/// Kind of value, as a JSON integer: 0 Text, 1 Json, 2 Object, 3 Boolean, 4 Number, 5 FileReference,
/// 6 ArtifactReference. An input of kind Object accepts any output kind; other kinds must match.
/// </param>
/// <param name="SchemaJson">
/// Optional JSON Schema of the value, as a JSON string (for example <c>"{\"type\":\"object\"}"</c>); empty when the
/// shape has no schema. The result shape of a HumanInput node becomes the response schema of its external requests.
/// </param>
/// <param name="Description">Human-readable description of the value, for example <c>Plain text</c>.</param>
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

/// <summary>
/// Descriptive input or output port of a workflow node, stored with the definition for editors. The runtime routes
/// along edges between nodes; ports are not validated.
/// </summary>
/// <param name="Id">
/// Identifier of the port within its node; edges may name it in <c>sourcePortId</c> or <c>targetPortId</c>.
/// </param>
/// <param name="Name">Display name of the port.</param>
/// <param name="Direction">Direction of the port, as a JSON integer: 0 Input, 1 Output.</param>
/// <param name="Shape">Shape of the value that passes through the port.</param>
/// <param name="Required">True when the port is marked as required; informational.</param>
public sealed record WorkflowPort(
    WorkflowPortId Id,
    string Name,
    WorkflowPortDirection Direction,
    WorkflowValueShape Shape,
    bool Required);

/// <summary>
/// Model call settings of an LLM call component.
/// </summary>
/// <param name="Temperature">
/// Sampling temperature from 0 through 2, or null to use the provider default. A value outside the range fails
/// validation.
/// </param>
/// <param name="MaxOutputTokens">
/// Maximum number of output tokens, or null for the provider default. A supplied value must be greater than zero.
/// </param>
/// <param name="RequireJsonOutput">
/// True to require JSON output. The component then needs a Json result shape or a
/// <c>responseFormatJsonSchema</c>, and its provider must support structured output.
/// </param>
/// <param name="ResponseFormatJsonSchema">
/// Optional JSON Schema of the required output, as a JSON string; empty for none.
/// </param>
public sealed record WorkflowModelSettings(
    double? Temperature,
    int? MaxOutputTokens,
    bool RequireJsonOutput,
    string ResponseFormatJsonSchema);

/// <summary>
/// Reusable LLM call component: a model call (provider, model, instructions, input and result shapes, permissions)
/// that LLM call nodes reference by <c>settings.componentId</c>. Its instructions are stored as a Prompt Gallery
/// prompt version. Returned by the component operations; create or replace it with
/// <c>POST /api/workflows/components</c>.
/// </summary>
/// <param name="Id">Identifier of the component.</param>
/// <param name="Name">Display name, trimmed.</param>
/// <param name="ProviderProfileId">
/// Provider profile the component calls, from <c>providerProfileId</c> of <c>GET /api/workflows/provider-options</c>;
/// null leaves the provider to the node or the host default.
/// </param>
/// <param name="Model">Model name within the provider, for example one of the provider's <c>modelOptions</c>.</param>
/// <param name="Modality">
/// Input modality, as a JSON integer: 0 Text, 1 Vision, 2 Audio, 3 Image, 4 Multimodal. Audio and Image are not
/// supported by the current runtime.
/// </param>
/// <param name="ModelSettings">Temperature, output limit and JSON output settings.</param>
/// <param name="Instructions">
/// System instructions of the model call, taken from the bound Prompt Gallery prompt version.
/// </param>
/// <param name="InputShape">Shape of the value the component receives.</param>
/// <param name="ResultShape">Shape of the value the component produces.</param>
/// <param name="Permissions">Permissions granted to the model call, such as tools and secrets it may use.</param>
/// <param name="CreatedAtUtc">Time the component was first stored, as an instant with offset.</param>
/// <param name="UpdatedAtUtc">Time the component was last stored.</param>
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
    DateTimeOffset UpdatedAtUtc)
{
    /// <summary>
    /// Identifier of the Prompt Gallery item that holds the component's instructions; null for a component that was
    /// never bound, which a save binds.
    /// </summary>
    public Guid? PromptArtifactId { get; init; }

    /// <summary>
    /// Identifier of the Prompt Gallery prompt version whose content is the component's instructions.
    /// </summary>
    public Guid? PromptVersionId { get; init; }
}

/// <summary>
/// Kind-specific settings of a workflow node. Which members matter depends on the node kind; members that do not
/// apply are stored but ignored. Send null or empty values for them.
/// </summary>
/// <param name="ComponentId">
/// LLM call component the node calls; required for LlmCall nodes, null otherwise.
/// </param>
/// <param name="AgentId">
/// Agent the node refers to, for AgentStep nodes; stored with the definition but not executed by the current runtime.
/// </param>
/// <param name="SubworkflowId">
/// Workflow the node refers to, for Subworkflow nodes; stored with the definition but not executed by the current
/// runtime.
/// </param>
/// <param name="ExternalRequestKind">
/// For HumanInput nodes, the kind of external request the node raises, as a JSON integer: 0 HumanInput, 1 Approval,
/// 2 ToolApproval; null means HumanInput.
/// </param>
/// <param name="Instructions">
/// Instructions of the node: for LlmCall nodes the system instructions (empty is filled from the component when the
/// definition is saved); for HumanInput nodes the prompt shown to the responder.
/// </param>
/// <param name="InputShape">Shape of the value the node receives; null when not declared.</param>
/// <param name="ResultShape">
/// Shape of the value the node produces; null when not declared. For HumanInput nodes its schema is the response
/// schema.
/// </param>
public sealed record WorkflowNodeSettings(
    WorkflowComponentId? ComponentId,
    Guid? AgentId,
    WorkflowId? SubworkflowId,
    WorkflowExternalRequestKind? ExternalRequestKind,
    string Instructions,
    WorkflowValueShape? InputShape,
    WorkflowValueShape? ResultShape)
{
    /// <summary>
    /// For LlmCall nodes, the provider profile to call instead of the component's; null uses the component's
    /// provider (filled in when the definition is saved).
    /// </summary>
    public Guid? ProviderProfileId { get; init; }

    /// <summary>
    /// For LlmCall nodes, the model to call instead of the component's; empty uses the component's model (filled in
    /// when the definition is saved).
    /// </summary>
    public string Model { get; init; } = string.Empty;

    /// <summary>
    /// Executor the node runs, an identifier from <c>GET /api/workflows/executor-catalog</c> such as
    /// <c>storage.file</c>. Required for Executor nodes; a node of another kind that names an executor runs it too.
    /// </summary>
    public WorkflowExecutorId? ExecutorId { get; init; }

    /// <summary>
    /// Settings of the executor as a JSON string containing a JSON object that satisfies the executor's settings
    /// schema; empty uses the executor's default settings.
    /// </summary>
    public string ExecutorSettingsJson { get; init; } = string.Empty;

    /// <summary>
    /// Timeout and retry policy of the executor for this node; null uses the executor's default policy.
    /// </summary>
    public WorkflowExecutorExecutionPolicy? ExecutionPolicy { get; init; }
}

/// <summary>
/// One node of a workflow definition graph.
/// </summary>
/// <param name="Id">Identifier of the node, unique within the graph.</param>
/// <param name="Kind">
/// Kind of node, as a JSON integer: 0 Start, 1 LlmCall, 2 Triage, 3 StrictLogic, 4 Executor, 5 Artifact,
/// 6 HumanInput, 7 AgentStep, 8 Subworkflow, 9 End.
/// </param>
/// <param name="Name">Display name of the node.</param>
/// <param name="Ports">Descriptive ports of the node; may be empty.</param>
/// <param name="Settings">Kind-specific settings.</param>
/// <param name="CanvasX">Horizontal position on the editor canvas; layout only. Defaults to 0.</param>
/// <param name="CanvasY">Vertical position on the editor canvas; layout only. Defaults to 0.</param>
public sealed record WorkflowNode(
    WorkflowNodeId Id,
    WorkflowNodeKind Kind,
    string Name,
    IReadOnlyList<WorkflowPort> Ports,
    WorkflowNodeSettings Settings,
    double CanvasX = 0,
    double CanvasY = 0);

/// <summary>
/// Route of a workflow edge: when the edge is taken. Conditions are evaluated against the JSON output of the source
/// node with the built-in JSON routing language.
/// </summary>
/// <param name="Kind">
/// How the route decides, as a JSON integer: 0 Always, 1 Predicate, 2 SwitchCase, 3 SwitchDefault,
/// 4 FanOutSelector.
/// </param>
/// <param name="Label">Display label of the route; empty for none.</param>
/// <param name="JsonPath">
/// Path of the value to test in the source node's JSON output: <c>$</c> followed by <c>.property</c> and
/// <c>[index]</c> segments, for example <c>$.decision.status</c> or <c>$.items[0]</c>. Required for Predicate,
/// SwitchCase and FanOutSelector routes; must be empty for Always and SwitchDefault routes.
/// </param>
/// <param name="Operator">
/// Comparison, as a JSON integer: 0 Exists, 1 DoesNotExist, 2 Equals, 3 NotEquals, 4 Contains, 5 StartsWith,
/// 6 EndsWith, 7 GreaterThan, 8 GreaterThanOrEqual, 9 LessThan, 10 LessThanOrEqual, 11 IsTruthy, 12 IsFalsy.
/// SwitchCase routes use Equals.
/// </param>
/// <param name="ExpectedValueJson">
/// Expected value as JSON text in a string, for example <c>"\"approved\""</c>, <c>"5"</c> or <c>"true"</c>. Required
/// by every operator except Exists, DoesNotExist, IsTruthy and IsFalsy; must be empty for Always and SwitchDefault
/// routes.
/// </param>
/// <param name="ExpectedValueKind">
/// JSON type of the expected value, as a JSON integer: 0 String, 1 Number, 2 Boolean, 3 Null, 4 Json (any). It must
/// match the expected value; numeric comparisons need Number and StartsWith or EndsWith need String.
/// </param>
/// <param name="CaseSensitive">True to compare strings case-sensitively; false compares them ignoring case.</param>
/// <param name="FanOutTargetIndex">
/// Position of this target among the fan-out targets of the source node, from 0; only for FanOutSelector routes, and
/// unique among them. Null orders the target by edge order.
/// </param>
/// <param name="RoutingLanguage">
/// Language of the route: <c>built-in-json-v1</c>. <c>legacy-condition-expression</c> is accepted only for Always
/// routes, and <c>artl-v1</c> is reserved and rejected.
/// </param>
public sealed record WorkflowEdgeRouting(
    WorkflowRouteKind Kind,
    string Label,
    string JsonPath,
    WorkflowRouteOperator Operator,
    string ExpectedValueJson,
    WorkflowRouteValueKind ExpectedValueKind,
    bool CaseSensitive,
    int? FanOutTargetIndex,
    string RoutingLanguage)
{
    public static WorkflowEdgeRouting Always { get; } = new(
        WorkflowRouteKind.Always,
        Label: string.Empty,
        JsonPath: string.Empty,
        WorkflowRouteOperator.Exists,
        ExpectedValueJson: string.Empty,
        WorkflowRouteValueKind.Json,
        CaseSensitive: false,
        FanOutTargetIndex: null,
        WorkflowRoutingLanguages.BuiltInJsonV1);

    public static WorkflowEdgeRouting Predicate(
        string jsonPath,
        WorkflowRouteOperator @operator,
        string expectedValueJson,
        WorkflowRouteValueKind expectedValueKind,
        string label = "",
        bool caseSensitive = false)
        => new(
            WorkflowRouteKind.Predicate,
            label,
            jsonPath,
            @operator,
            expectedValueJson,
            expectedValueKind,
            caseSensitive,
            FanOutTargetIndex: null,
            WorkflowRoutingLanguages.BuiltInJsonV1);

    public static WorkflowEdgeRouting SwitchCase(
        string jsonPath,
        string expectedValueJson,
        WorkflowRouteValueKind expectedValueKind,
        string label = "",
        bool caseSensitive = false)
        => new(
            WorkflowRouteKind.SwitchCase,
            label,
            jsonPath,
            WorkflowRouteOperator.Equals,
            expectedValueJson,
            expectedValueKind,
            caseSensitive,
            FanOutTargetIndex: null,
            WorkflowRoutingLanguages.BuiltInJsonV1);

    public static WorkflowEdgeRouting SwitchDefault(string label = "")
        => new(
            WorkflowRouteKind.SwitchDefault,
            label,
            JsonPath: string.Empty,
            WorkflowRouteOperator.Exists,
            ExpectedValueJson: string.Empty,
            WorkflowRouteValueKind.Json,
            CaseSensitive: false,
            FanOutTargetIndex: null,
            WorkflowRoutingLanguages.BuiltInJsonV1);

    public static WorkflowEdgeRouting FanOutSelector(
        string jsonPath,
        WorkflowRouteOperator @operator,
        string expectedValueJson,
        WorkflowRouteValueKind expectedValueKind,
        int? targetIndex = null,
        string label = "",
        bool caseSensitive = false)
        => new(
            WorkflowRouteKind.FanOutSelector,
            label,
            jsonPath,
            @operator,
            expectedValueJson,
            expectedValueKind,
            caseSensitive,
            targetIndex,
            WorkflowRoutingLanguages.BuiltInJsonV1);
}

public static class WorkflowRoutingLanguages
{
    public const string BuiltInJsonV1 = "built-in-json-v1";
    public const string LegacyConditionExpression = "legacy-condition-expression";
    public const string ArtlV1 = "artl-v1";
}

/// <summary>
/// Directed connection between two nodes of a workflow definition graph.
/// </summary>
/// <param name="Id">Identifier of the edge, unique within the graph.</param>
/// <param name="SourceNodeId">Node the edge leaves; must exist in the graph.</param>
/// <param name="SourcePortId">Optional port of the source node; descriptive only, null for none.</param>
/// <param name="TargetNodeId">Node the edge enters; must exist in the graph.</param>
/// <param name="TargetPortId">Optional port of the target node; descriptive only, null for none.</param>
/// <param name="Kind">
/// Kind of edge, as a JSON integer: 0 Direct, 1 Conditional, 2 FanOut, 3 FanIn.
/// </param>
/// <param name="ConditionExpression">
/// Legacy condition text, used only as the route label when <c>routing.label</c> is empty; it is not evaluated. Send an
/// empty string.
/// </param>
public sealed record WorkflowEdge(
    WorkflowEdgeId Id,
    WorkflowNodeId SourceNodeId,
    WorkflowPortId? SourcePortId,
    WorkflowNodeId TargetNodeId,
    WorkflowPortId? TargetPortId,
    WorkflowEdgeKind Kind,
    string ConditionExpression)
{
    /// <summary>
    /// When the edge is taken. Omitted means an unconditional route in <c>built-in-json-v1</c>; null is rejected by
    /// validation.
    /// </summary>
    public WorkflowEdgeRouting Routing { get; init; } = WorkflowEdgeRouting.Always;
}

/// <summary>
/// Node graph of a workflow definition. It needs at least one node and one End node, a start node that exists, unique
/// node and edge identifiers, edges between existing nodes, and every node reachable from the start node.
/// </summary>
/// <param name="StartNodeId">Node where runs begin; must be one of <c>nodes</c>.</param>
/// <param name="Nodes">Every node of the workflow.</param>
/// <param name="Edges">Every edge of the workflow.</param>
public sealed record WorkflowGraph(
    WorkflowNodeId StartNodeId,
    IReadOnlyList<WorkflowNode> Nodes,
    IReadOnlyList<WorkflowEdge> Edges);

/// <summary>
/// Runtime policy of a workflow definition, or the recorded default policy of the workflow settings: which backend
/// runs it and which kinds of runs are allowed.
/// </summary>
/// <param name="PreferredBackend">
/// Backend used when a start request names none, as a JSON integer: 0 InProcess, 1 DurableTask, 2 AzureFunctions. It
/// must be registered and runnable in this host; by default only InProcess is.
/// </param>
/// <param name="AllowInProcessPreviewRuns">
/// True to allow preview (test) runs on the in-process backend; when false, test runs on InProcess are rejected.
/// </param>
/// <param name="RequireDurableProductionRuns">
/// True to reject production runs on a non-durable backend. It cannot be combined with InProcess as the preferred
/// backend.
/// </param>
/// <param name="ExposeAzureFunctionsStatusEndpoint">
/// Request for an Azure Functions status endpoint; true is accepted only when an Azure Functions backend is runnable,
/// which none is by default.
/// </param>
/// <param name="ExposeAzureFunctionsMcpTool">
/// Request for a Model Context Protocol tool on an Azure Functions backend; true is accepted only when an Azure
/// Functions backend is runnable, which none is by default.
/// </param>
public sealed record WorkflowRuntimePolicy(
    WorkflowRuntimeBackendKind PreferredBackend,
    bool AllowInProcessPreviewRuns,
    bool RequireDurableProductionRuns,
    bool ExposeAzureFunctionsStatusEndpoint,
    bool ExposeAzureFunctionsMcpTool);

/// <summary>
/// Nodes whose execution a preview (test) run replaces with a rendered output template, so that a test does not call
/// the real executor. It is rejected for production runs.
/// </summary>
/// <param name="Steps">Nodes to simulate; empty for none.</param>
public sealed record WorkflowPreviewSimulationPlan(IReadOnlyList<WorkflowPreviewSimulationStep> Steps)
{
    public static WorkflowPreviewSimulationPlan Empty { get; } = new([]);

    /// <summary>True when <c>steps</c> is not empty. Computed; it is ignored when sent.</summary>
    public bool HasSteps => Steps.Count > 0;
}

/// <summary>
/// One simulated node of a preview run.
/// </summary>
/// <param name="NodeId">Node whose execution is replaced.</param>
/// <param name="SourceExecutorId">
/// Executor the node uses; when set it must equal the node's <c>settings.executorId</c>, otherwise the run fails.
/// </param>
/// <param name="Reason">Why the node is simulated; informational.</param>
/// <param name="OutputTemplateJson">
/// Template of the simulated output as JSON text in a string, for example an executor's
/// <c>simulation.outputTemplateJson</c> from the executor catalog.
/// </param>
public sealed record WorkflowPreviewSimulationStep(
    WorkflowNodeId NodeId,
    WorkflowExecutorId? SourceExecutorId,
    string Reason,
    string OutputTemplateJson);

/// <summary>
/// One stored version of a workflow definition: its graph, runtime policy, input parameters and stable identities.
/// Returned by the definition reads, saves, imports and status changes, and accepted as a draft by
/// <c>POST /api/workflows/validate</c> and the test-run operation. A workflow keeps its <c>id</c> across versions; each
/// version has its own <c>versionId</c>.
/// </summary>
/// <param name="Id">Identifier of the workflow, the same for all its versions.</param>
/// <param name="VersionId">Identifier of this version.</param>
/// <param name="Name">Display name; required.</param>
/// <param name="Description">Description of the workflow; may be empty.</param>
/// <param name="Status">
/// Lifecycle status of this version, as a JSON integer: 0 Draft, 1 Active, 2 Suspended, 3 Archived.
/// </param>
/// <param name="Graph">The node graph.</param>
/// <param name="RuntimePolicy">Backend and run-kind policy of the workflow.</param>
/// <param name="CreatedAtUtc">Time the workflow was first stored, as an instant with offset.</param>
/// <param name="UpdatedAtUtc">Time this version was stored.</param>
public sealed record WorkflowDefinition(
    WorkflowId Id,
    WorkflowVersionId VersionId,
    string Name,
    string Description,
    WorkflowLifecycleStatus Status,
    WorkflowGraph Graph,
    WorkflowRuntimePolicy RuntimePolicy,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc)
{
    /// <summary>
    /// Input parameters the workflow expects in its run input; they drive start forms and are stored with the
    /// version. Empty when none are declared.
    /// </summary>
    public IReadOnlyList<WorkflowInputParameterDescriptor> InputParameters { get; init; } = [];

    /// <summary>
    /// Template key when the workflow was installed from a workflow template; empty otherwise. It is lower-case, not
    /// unique and cannot be set by a save.
    /// </summary>
    public string TemplateKey { get; init; } = string.Empty;

    /// <summary>
    /// Key of the template pack the workflow came from; empty when it was not installed from a template.
    /// </summary>
    public string TemplatePackKey { get; init; } = string.Empty;

    /// <summary>Version of that template pack; empty when not installed from a template.</summary>
    public string TemplatePackVersion { get; init; } = string.Empty;

    /// <summary>
    /// SHA-256 hash of the template source as lower-case hexadecimal; empty when not installed from a template.
    /// </summary>
    public string SourceHash { get; init; } = string.Empty;

    /// <summary>
    /// Namespace of the workflow's external identity, lower-case, for example <c>partner.system</c>; empty when the
    /// workflow has none. Together with <c>externalKey</c> it is unique across workflows.
    /// </summary>
    public string ExternalNamespace { get; init; } = string.Empty;

    /// <summary>
    /// Key of the workflow's external identity within its namespace, lower-case, for example <c>invoice:review</c>;
    /// empty when the workflow has none.
    /// </summary>
    public string ExternalKey { get; init; } = string.Empty;
}

/// <summary>
/// One problem found by workflow validation.
/// </summary>
/// <param name="Code">
/// Stable issue code, as a JSON integer: 0 MissingName, 1 MissingStartNode, 2 MissingEndNode, 3 EmptyGraph, 4
/// DuplicateNodeId, 5 DuplicateEdgeId, 6 DisconnectedNode, 7 UnknownEdgeEndpoint, 8 InvalidComponentReference, 9
/// InvalidExecutorReference, 10 InvalidExecutorSettings, 11 InvalidExecutionPolicy, 12 InvalidProviderModel, 13
/// InvalidWorkflowSettings, 14 InvalidRouteDefinition, 15 UnsupportedRuntimeBackend, 16 UnsupportedModality, 17
/// UnsupportedNodeKind, 18 ShapeMismatch.
/// </param>
/// <param name="Message">Human-readable explanation; its wording can change.</param>
/// <param name="NodeId">Node the issue concerns; null when it is not about one node.</param>
/// <param name="EdgeId">Edge the issue concerns; null when it is not about one edge.</param>
public sealed record WorkflowValidationIssue(
    WorkflowValidationIssueCode Code,
    string Message,
    WorkflowNodeId? NodeId = null,
    WorkflowEdgeId? EdgeId = null);

/// <summary>
/// Result of validating a workflow definition against the current components, executors, providers, runtime backends
/// and workflow settings. A definition must be valid to be saved, published or started.
/// </summary>
/// <param name="Issues">Every problem found; empty when the definition is valid.</param>
public sealed record WorkflowValidationResult(IReadOnlyList<WorkflowValidationIssue> Issues)
{
    /// <summary>True when <c>issues</c> is empty. Computed; it is ignored when sent.</summary>
    public bool Succeeded => Issues.Count == 0;

    public static WorkflowValidationResult Success { get; } = new([]);
}

/// <summary>
/// A workflow runtime backend known to this host, with its capabilities and whether it can run workflows here.
/// Returned by <c>GET /api/workflows/runtime-backends</c>.
/// </summary>
/// <param name="Kind">Backend, as a JSON integer: 0 InProcess, 1 DurableTask, 2 AzureFunctions.</param>
/// <param name="Name">Display name of the backend.</param>
/// <param name="IsDurable">True when runs on this backend survive a host restart.</param>
/// <param name="SupportsStreaming">True when the backend can report progress while a run executes.</param>
/// <param name="SupportsExternalRequests">True when runs on this backend can wait for external requests.</param>
/// <param name="SupportsDashboardObservability">True when the backend offers its own monitoring dashboard.</param>
/// <param name="OperationalNotes">Guidance on when to use the backend.</param>
public sealed record WorkflowRuntimeBackendDescriptor(
    WorkflowRuntimeBackendKind Kind,
    string Name,
    bool IsDurable,
    bool SupportsStreaming,
    bool SupportsExternalRequests,
    bool SupportsDashboardObservability,
    string OperationalNotes)
{
    /// <summary>
    /// Whether the catalog declares that runs can resume after an external response. The catalog entries of this host
    /// report false, even for the in-process backend whose waiting runs do resume; do not rely on it.
    /// </summary>
    public bool SupportsExternalResponseResume { get; init; }

    /// <summary>
    /// Whether the catalog declares support for cancelling an executing run. The catalog entries of this host report
    /// false, even for the in-process backend whose runs can be cancelled; do not rely on it.
    /// </summary>
    public bool SupportsActiveCancellation { get; init; }

    /// <summary>
    /// Availability in this host, as a JSON integer: 0 Registered, 1 Planned, 2 Unavailable.
    /// </summary>
    public WorkflowRuntimeBackendAvailabilityKind Availability { get; init; } = WorkflowRuntimeBackendAvailabilityKind.Registered;

    /// <summary>True when the backend is registered in this host.</summary>
    public bool IsRegistered { get; init; } = true;

    /// <summary>True when workflows can run on the backend in this host.</summary>
    public bool IsRunnable { get; init; } = true;

    /// <summary>Human-readable explanation of the availability.</summary>
    public string AvailabilityReason { get; init; } = "Runtime backend is registered and runnable in this host.";
}

public sealed record WorkflowRunStartRequest(
    WorkflowId WorkflowId,
    WorkflowVersionId VersionId,
    string InputJson,
    WorkflowRuntimeBackendKind? RequestedBackend,
    Guid? SourceProcessRunId,
    Guid? SourceProcessAssignmentId)
{
    public WorkflowPreviewSimulationPlan PreviewSimulationPlan { get; init; } = WorkflowPreviewSimulationPlan.Empty;

    public WorkflowLaunchOrigin? Origin { get; init; }

    public WorkflowLaunchIdempotency Idempotency { get; init; } = new WorkflowLaunchIdempotency.NotRequested();

    public WorkflowRunId? RequestedRunId { get; init; }
}

/// <summary>
/// Stored record of a workflow run, returned without the public safe projection only by the test-run, cancellation
/// and analytics operations. Unlike the safe run projection it includes the backend run identifier and, in a test-run
/// result for the caller's own preview run, the launch origin; the other run operations return the projection instead.
/// </summary>
/// <param name="RunId">Identifier of the run.</param>
/// <param name="WorkflowId">Identifier of the workflow that runs.</param>
/// <param name="VersionId">Identifier of the definition version that runs.</param>
/// <param name="State">
/// Run state, as a JSON integer: 0 NotStarted, 1 Running, 2 WaitingForInput, 3 Idle, 4 Completed, 5 Failed,
/// 6 Cancelled.
/// </param>
/// <param name="Backend">
/// Runtime backend of the run, as a JSON integer: 0 InProcess, 1 DurableTask, 2 AzureFunctions.
/// </param>
/// <param name="BackendRunId">
/// Identifier of the run inside its backend; internal and not stable across backends.
/// </param>
/// <param name="Summary">Short status text of the run, not bounded or redacted in this record.</param>
/// <param name="CreatedAtUtc">Time the run was created, as an instant with offset.</param>
/// <param name="UpdatedAtUtc">Time the run record last changed.</param>
public sealed record WorkflowRunSnapshot(
    WorkflowRunId RunId,
    WorkflowId WorkflowId,
    WorkflowVersionId VersionId,
    WorkflowRunState State,
    WorkflowRuntimeBackendKind Backend,
    string BackendRunId,
    string Summary,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc)
{
    /// <summary>Time the run reached a final state; null while it has not.</summary>
    public DateTimeOffset? TerminalAtUtc { get; init; }

    /// <summary>
    /// Who or what launched the run, with the authority captured for it. Internal launch lineage, not a stable client
    /// contract; null for runs recorded without an origin, and always null in the cancellation and analytics
    /// responses, which can return other callers' runs.
    /// </summary>
    public WorkflowLaunchOrigin? Origin { get; init; }
}

/// <summary>
/// Stored record of one workflow run event, returned without the public safe projection only inside test-run results.
/// Unlike the safe event projection it includes the event payload.
/// </summary>
/// <param name="Id">Identifier of the event.</param>
/// <param name="RunId">Identifier of the run.</param>
/// <param name="Kind">
/// What happened, as a JSON integer: 0 Started, 1 ExecutorInvoked, 2 ExecutorCompleted, 3 ExecutorFailed,
/// 4 SuperStep, 5 Output, 6 Warning, 7 Error, 8 WaitingForInput, 9 Completed, 10 Cancelled, 11 Unknown.
/// </param>
/// <param name="NodeId">Node the event belongs to; null for run-level events.</param>
/// <param name="Message">Event message, not bounded in this record.</param>
/// <param name="PayloadJson">Recorded event payload as JSON text in a string; may be empty.</param>
/// <param name="CreatedAtUtc">Time the event was recorded, as an instant with offset.</param>
public sealed record WorkflowEventRecord(
    Guid Id,
    WorkflowRunId RunId,
    WorkflowEventKind Kind,
    WorkflowNodeId? NodeId,
    string Message,
    string PayloadJson,
    DateTimeOffset CreatedAtUtc) {
    [JsonIgnore]
    public WorkflowRunDisclosureDeclaration? DisclosureDeclaration { get; init; }

    [JsonIgnore]
    public WorkflowNodeCompletionProof? CompletionProof { get; init; }

    [JsonIgnore]
    public IReadOnlyList<WorkflowProviderReadEvidence> ProviderReadEvidence { get; init; } = [];
}

public enum WorkflowEventPayloadSource
{
    Runtime,
    MafNative,
    CanDoItAllProgress,
    ExternalRequest
}

public sealed record WorkflowEventPayloadEnvelope(
    WorkflowEventPayloadSource Source,
    string EventType,
    WorkflowNodeId? NodeId,
    WorkflowExecutorId? ExecutorId,
    WorkflowExternalRequestId? RequestId,
    WorkflowExternalRequestKind? RequestKind,
    string InlineJson,
    int? InlineCharacters,
    bool InlineTruncated,
    string Reference)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WorkflowUsageMetrics? Usage { get; init; }
}

/// <summary>
/// Stored record of an external request of a workflow run, returned without the public safe projection only inside
/// test-run results. Unlike the pending-request projection it includes the raw request and response JSON.
/// </summary>
/// <param name="Id">Identifier of the external request.</param>
/// <param name="RunId">Identifier of the run that raised it.</param>
/// <param name="Kind">
/// What the request asks for, as a JSON integer: 0 HumanInput, 1 Approval, 2 ToolApproval.
/// </param>
/// <param name="NodeId">Node that raised the request.</param>
/// <param name="EventName">Name of the request event recorded by the runtime.</param>
/// <param name="RequestJson">
/// Raw request payload as JSON text in a string, including the prompt and any context passed from earlier nodes.
/// </param>
/// <param name="ResponseJson">Raw response as JSON text in a string; empty until a response is recorded.</param>
/// <param name="CreatedAtUtc">Time the request was raised, as an instant with offset.</param>
/// <param name="RespondedAtUtc">Time a response was recorded; null while none was.</param>
public sealed record WorkflowExternalRequestRecord(
    WorkflowExternalRequestId Id,
    WorkflowRunId RunId,
    WorkflowExternalRequestKind Kind,
    WorkflowNodeId NodeId,
    string EventName,
    string RequestJson,
    string ResponseJson,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? RespondedAtUtc)
{
    /// <summary>Current version of the request; answers must name it.</summary>
    public WorkflowExternalRequestVersion Version { get; init; } = WorkflowExternalRequestVersion.Initial;

    /// <summary>
    /// Stored state of the request, as a JSON integer: 0 Pending, 1 ResponseClaimed, 2 Responded, 3 Denied,
    /// 4 Superseded, 5 Cancelled, 6 LegacyNonResumable.
    /// </summary>
    public WorkflowExternalRequestState State { get; init; } = WorkflowExternalRequestState.LegacyNonResumable;

    /// <summary>What a response must satisfy; null for a request without a response contract.</summary>
    public WorkflowExternalResponseContract? ResponseContract { get; init; }

    [JsonIgnore]
    public WorkflowExternalRequestContinuation? Continuation { get; init; }

    [JsonIgnore]
    public WorkflowExternalRequestAuthorizationPolicySnapshot? AuthorizationPolicy { get; init; }

    [JsonIgnore]
    public WorkflowExternalRequestState EffectiveState => State == WorkflowExternalRequestState.LegacyNonResumable && RespondedAtUtc.HasValue
        ? WorkflowExternalRequestState.Responded
        : State;
}

/// <summary>
/// Stored record of a checkpoint of a workflow run, returned without the public safe projection only inside test-run
/// results. Unlike the checkpoint projection it includes backend checkpoint identifiers, payload references and hashes.
/// </summary>
/// <param name="Id">Identifier of the checkpoint.</param>
/// <param name="RunId">Identifier of the run.</param>
/// <param name="WorkflowId">Identifier of the workflow of the run.</param>
/// <param name="VersionId">Identifier of the definition version the run executes.</param>
/// <param name="Backend">
/// Runtime backend that took the checkpoint, as a JSON integer: 0 InProcess, 1 DurableTask, 2 AzureFunctions.
/// </param>
/// <param name="Kind">
/// Why the checkpoint was taken, as a JSON integer: 0 RuntimeBoundary, 1 SuperStep, 2 WaitingForInput, 3 Completed,
/// 4 Failed, 5 Cancelled.
/// </param>
/// <param name="TrustBoundary">
/// What it holds, as a JSON integer: 0 MetadataOnly, 1 TrustedRuntimeState.
/// </param>
/// <param name="ResumeAvailability">
/// Whether the run can resume from it, as a JSON integer: 0 NotSupported, 1 BlockedByPolicy, 2 Available.
/// </param>
/// <param name="NodeId">Node at which it was taken; null when not tied to a node.</param>
/// <param name="ExternalRequestId">External request it waits for; null when it is not a wait for input.</param>
/// <param name="BackendCheckpointId">Internal identifier of the checkpoint in its backend; may be empty.</param>
/// <param name="PayloadReference">Internal reference to the stored checkpoint payload.</param>
/// <param name="PayloadHash">Hash of the stored checkpoint payload; empty for metadata-only checkpoints.</param>
/// <param name="Summary">Short description of the checkpoint.</param>
/// <param name="ResumeUnavailableReason">Why the run cannot resume from it; empty when it can.</param>
/// <param name="CreatedAtUtc">Time the checkpoint was recorded, as an instant with offset.</param>
/// <param name="ResumedAtUtc">Time the run resumed from it; null when it has not.</param>
public sealed record WorkflowCheckpointRecord(
    WorkflowCheckpointId Id,
    WorkflowRunId RunId,
    WorkflowId WorkflowId,
    WorkflowVersionId VersionId,
    WorkflowRuntimeBackendKind Backend,
    WorkflowCheckpointKind Kind,
    WorkflowCheckpointTrustBoundary TrustBoundary,
    WorkflowResumeAvailability ResumeAvailability,
    WorkflowNodeId? NodeId,
    WorkflowExternalRequestId? ExternalRequestId,
    string BackendCheckpointId,
    string PayloadReference,
    string PayloadHash,
    string Summary,
    string ResumeUnavailableReason,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ResumedAtUtc);

/// <summary>
/// Stored record of a workflow run artifact, returned without the public safe projection only inside test-run results.
/// Unlike the artifact projection it includes the storage path.
/// </summary>
/// <param name="Id">Identifier of the artifact.</param>
/// <param name="RunId">Identifier of the run that recorded it.</param>
/// <param name="Kind">
/// What it holds, as a JSON integer: 0 Text, 1 Json, 2 File, 3 Image, 4 Binary, 5 ToolReceipt, 6 PreviewSimulation.
/// </param>
/// <param name="NodeId">Node that produced it; null when not tied to a node.</param>
/// <param name="Name">Display name, such as a file name.</param>
/// <param name="ContentType">Media type of the content, for example <c>application/json</c>.</param>
/// <param name="StoragePath">
/// Workspace-relative logical path of the stored content, or of the workspace file the run wrote, for example
/// <c>workflow-runs/3f2504e04f8911d39a0c0305e82c3301/payloads/run-input-7d9e1f2a3b4c4d5e8f6a7b8c9d0e1f2a.json</c>.
/// </param>
/// <param name="Summary">Short description of the artifact.</param>
/// <param name="CreatedAtUtc">Time the artifact was recorded, as an instant with offset.</param>
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

public sealed record WorkflowNodeInput(string PayloadJson) {
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WorkflowExecutionOccurrence? ExecutionOccurrence { get; init; }
}

public sealed record WorkflowNodeExecutionResult(
    WorkflowNodeId NodeId,
    string PayloadJson,
    WorkflowValueShape ResultShape)
{
    [JsonIgnore]
    public IReadOnlyList<WorkflowProviderReadEvidence> ProviderReadEvidence { get; init; } = [];

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WorkflowUsageMetrics? Usage { get; init; }

    public IReadOnlyList<WorkflowUsageObservation> UsageObservations { get; init; } = [];
}
