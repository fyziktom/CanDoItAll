using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.ProviderHistory;

namespace CanDoItAll.AgentFramework.Models;

/// <summary>
/// How a launch chose the definition version to run, as a JSON integer: 0 ExactSavedVersion (a <c>versionId</c> was
/// given), 1 LatestActive (the latest Active version), 2 DraftPreview (an unsaved draft in a preview run).
/// </summary>
public enum WorkflowDefinitionSelectionKind
{
    ExactSavedVersion,
    LatestActive,
    DraftPreview
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$selection")]
[JsonDerivedType(typeof(WorkflowDefinitionSelection.ExactSavedVersion), "exact-saved-version")]
[JsonDerivedType(typeof(WorkflowDefinitionSelection.LatestActive), "latest-active")]
[JsonDerivedType(typeof(WorkflowDefinitionSelection.DraftPreview), "draft-preview")]
public abstract record WorkflowDefinitionSelection
{
    private WorkflowDefinitionSelection(WorkflowDefinitionSelectionKind kind)
    {
        Kind = kind;
    }

    public WorkflowDefinitionSelectionKind Kind { get; }

    public sealed record ExactSavedVersion(
        WorkflowId WorkflowId,
        WorkflowVersionId VersionId) :
        WorkflowDefinitionSelection(WorkflowDefinitionSelectionKind.ExactSavedVersion);

    public sealed record LatestActive(WorkflowId WorkflowId) :
        WorkflowDefinitionSelection(WorkflowDefinitionSelectionKind.LatestActive);

    public sealed record DraftPreview(WorkflowDefinition Definition) :
        WorkflowDefinitionSelection(WorkflowDefinitionSelectionKind.DraftPreview);
}

public enum WorkflowLaunchMode
{
    Preview,
    Production
}

public enum WorkflowLaunchCompletionPolicy
{
    WaitForStopped,
    ReturnWhenAccepted
}

/// <summary>
/// Kind of actor that launched or answered a workflow run, as a JSON integer: 0 User, 1 Agent, 2 Service.
/// </summary>
public enum WorkflowLaunchActorKind
{
    User,
    Agent,
    Service
}

/// <summary>
/// Actor recorded with a workflow launch: who started the run.
/// </summary>
public sealed record WorkflowLaunchActor
{
    [JsonConstructor]
    public WorkflowLaunchActor(WorkflowLaunchActorKind kind, string subjectId)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Workflow launch actor kind is not defined.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(subjectId);
        Kind = kind;
        SubjectId = subjectId.Trim();
    }

    /// <summary>Kind of actor, as a JSON integer: 0 User, 1 Agent, 2 Service.</summary>
    public WorkflowLaunchActorKind Kind { get; }

    /// <summary>
    /// Subject identifier of the actor, for example the bearer token subject of an API caller or an agent
    /// identifier; trimmed and never empty.
    /// </summary>
    public string SubjectId { get; }
}

/// <summary>
/// Identifier of a process run recorded in a workflow launch origin, as an object whose <c>value</c> member holds the
/// GUID.
/// </summary>
public readonly record struct WorkflowProcessRunId
{
    [JsonConstructor]
    public WorkflowProcessRunId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Workflow process run id cannot be empty.", nameof(value));
        }

        Value = value;
    }

    /// <summary>The process run identifier, a GUID string.</summary>
    public Guid Value { get; }

    public override string ToString() => Value.ToString("D");
}

/// <summary>
/// Identifier of a process step assignment recorded in a workflow launch origin, as an object whose <c>value</c>
/// member holds the GUID.
/// </summary>
public readonly record struct WorkflowProcessAssignmentId
{
    [JsonConstructor]
    public WorkflowProcessAssignmentId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Workflow process assignment id cannot be empty.", nameof(value));
        }

        Value = value;
    }

    /// <summary>The process step assignment identifier, a GUID string.</summary>
    public Guid Value { get; }

    public override string ToString() => Value.ToString("D");
}

/// <summary>
/// Correlation reference of the request that launched a workflow run, as an object whose <c>value</c> member holds
/// the text. For runs started through the HTTP API it is the server trace identifier of the start request. It is for
/// diagnostics only; it is not an idempotency key and does not prove that anything was committed.
/// </summary>
public readonly record struct WorkflowLaunchCorrelationId
{
    [JsonConstructor]
    public WorkflowLaunchCorrelationId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }

    public WorkflowLaunchCorrelationId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Workflow launch correlation id cannot be empty.", nameof(value));
        }

        Value = value.ToString("D");
    }

    /// <summary>The correlation text, trimmed and never empty.</summary>
    public string Value { get; }

    public override string ToString() => Value;
}

/// <summary>
/// Identifier of the agent or Project Structure session that launched a workflow run, as an object whose
/// <c>value</c> member holds the text.
/// </summary>
public readonly record struct WorkflowLaunchSessionId
{
    [JsonConstructor]
    public WorkflowLaunchSessionId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }

    /// <summary>The session identifier, trimmed and never empty.</summary>
    public string Value { get; }

    public override string ToString() => Value;
}

/// <summary>
/// Identifier of the scheduler firing that launched a workflow run, as an object whose <c>value</c> member holds the
/// GUID.
/// </summary>
public readonly record struct WorkflowSchedulerFireId
{
    [JsonConstructor]
    public WorkflowSchedulerFireId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Workflow scheduler fire id cannot be empty.", nameof(value));
        }

        Value = value;
    }

    /// <summary>The scheduler fire identifier, a GUID string.</summary>
    public Guid Value { get; }

    public override string ToString() => Value.ToString("D");
}

/// <summary>
/// Project Structure node recorded in a workflow launch origin or output binding, as an object whose <c>value</c>
/// member holds the string node identifier.
/// </summary>
public readonly record struct WorkflowProjectStructureNodeId
{
    [JsonConstructor]
    public WorkflowProjectStructureNodeId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }

    /// <summary>The string node identifier, trimmed; not a GUID in general.</summary>
    public string Value { get; }

    public override string ToString() => Value;
}

/// <summary>
/// Kind of workflow launch origin, as a JSON integer: 0 Api (the HTTP start operations), 1 Preview (a test run),
/// 2 SchedulerPlanRun (a scheduler plan), 3 ProjectStructureNode (a Project Structure workflow node),
/// 4 AgentRuntimeInvocation (an agent tool), 5 ProcessAssignment, 6 ProcessToolInvocation and
/// 7 ProcessDispatchAssignment (process steps).
/// </summary>
public enum WorkflowLaunchOriginKind
{
    Api = 0,
    Preview = 1,
    SchedulerPlanRun = 2,
    ProjectStructureNode = 3,
    AgentRuntimeInvocation = 4,
    ProcessAssignment = 5,
    ProcessToolInvocation = 6,
    ProcessDispatchAssignment = 7
}

/// <summary>
/// Launch lineage recorded with a workflow run: who or what started it, with the authority captured at launch. The
/// member <c>$origin</c> names the variant (<c>api</c>, <c>preview</c>, <c>scheduler-plan-run</c>,
/// <c>project-structure-node</c>, <c>agent-runtime-invocation</c>, <c>process-assignment</c>,
/// <c>process-tool-invocation-v1</c> or <c>process-dispatch-assignment-v1</c>). It appears only in the stored run
/// records returned by the test-run, cancellation and analytics operations; it is internal lineage, not a stable
/// client contract.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$origin")]
[JsonDerivedType(typeof(WorkflowLaunchOrigin.Api), "api")]
[JsonDerivedType(typeof(WorkflowLaunchOrigin.Preview), "preview")]
[JsonDerivedType(typeof(WorkflowLaunchOrigin.SchedulerPlanRun), "scheduler-plan-run")]
[JsonDerivedType(typeof(WorkflowLaunchOrigin.ProjectStructureNode), "project-structure-node")]
[JsonDerivedType(typeof(WorkflowLaunchOrigin.AgentRuntimeInvocation), "agent-runtime-invocation")]
[JsonDerivedType(typeof(WorkflowLaunchOrigin.ProcessAssignment), "process-assignment")]
[JsonDerivedType(typeof(WorkflowLaunchOrigin.ProcessToolInvocation), "process-tool-invocation-v1")]
[JsonDerivedType(typeof(WorkflowLaunchOrigin.ProcessDispatchAssignment), "process-dispatch-assignment-v1")]
public abstract record WorkflowLaunchOrigin
{
    private WorkflowLaunchOrigin(WorkflowLaunchOriginKind kind, WorkflowLaunchCorrelationId correlationId)
    {
        if (string.IsNullOrWhiteSpace(correlationId.Value))
        {
            throw new ArgumentException("Workflow launch correlation id is required.", nameof(correlationId));
        }

        Kind = kind;
        CorrelationId = correlationId;
    }

    /// <summary>
    /// Kind of origin, as a JSON integer: 0 Api, 1 Preview, 2 SchedulerPlanRun, 3 ProjectStructureNode,
    /// 4 AgentRuntimeInvocation, 5 ProcessAssignment, 6 ProcessToolInvocation, 7 ProcessDispatchAssignment.
    /// </summary>
    public WorkflowLaunchOriginKind Kind { get; }

    /// <summary>Correlation reference of the launching request; for diagnostics only.</summary>
    public WorkflowLaunchCorrelationId CorrelationId { get; }

    /// <summary>
    /// Workspace scope in which the launch was authorized, for example the organization of the current database
    /// profile for API launches; external requests of the run must be answered within it.
    /// </summary>
    public WorkspaceScopeDescriptor? AuthorizationScope { get; init; }

    /// <summary>Fingerprint of the authorization policy version applied at launch; internal.</summary>
    public string AuthorizationPolicyFingerprint { get; init; } = string.Empty;

    /// <summary>Caller identity recorded for provider request history; null when none was captured.</summary>
    public HistoryCaller? HistoryCaller { get; init; }

    /// <summary>
    /// Project Structure output authority captured at launch; an opaque internal object, null when none was captured.
    /// </summary>
    public WorkflowStructureAuthority? StructureAuthority { get; init; }

    /// <summary>
    /// Origin of a run started through the HTTP start operations (<c>$origin</c> <c>api</c>).
    /// </summary>
    public sealed record Api : WorkflowLaunchOrigin
    {
        [JsonConstructor]
        public Api(WorkflowLaunchActor actor, WorkflowLaunchCorrelationId correlationId)
            : base(WorkflowLaunchOriginKind.Api, correlationId)
        {
            ArgumentNullException.ThrowIfNull(actor);
            Actor = actor;
        }

        /// <summary>
        /// Caller that started the run: the bearer token subject, or the local operator when API authorization is
        /// disabled.
        /// </summary>
        public WorkflowLaunchActor Actor { get; }
    }

    /// <summary>
    /// Origin of a preview (test) run (<c>$origin</c> <c>preview</c>).
    /// </summary>
    public sealed record Preview : WorkflowLaunchOrigin
    {
        [JsonConstructor]
        public Preview(WorkflowLaunchActor actor, WorkflowLaunchCorrelationId correlationId)
            : base(WorkflowLaunchOriginKind.Preview, correlationId)
        {
            ArgumentNullException.ThrowIfNull(actor);
            Actor = actor;
        }

        /// <summary>Caller that started the preview run.</summary>
        public WorkflowLaunchActor Actor { get; }
    }

    /// <summary>
    /// Origin of a run started by a scheduler plan (<c>$origin</c> <c>scheduler-plan-run</c>).
    /// </summary>
    public sealed record SchedulerPlanRun : WorkflowLaunchOrigin
    {
        [JsonConstructor]
        public SchedulerPlanRun(
            Guid planId,
            Guid planRunId,
            WorkflowSchedulerFireId fireId,
            DateTimeOffset firedAtUtc,
            WorkflowLaunchCorrelationId correlationId)
            : base(WorkflowLaunchOriginKind.SchedulerPlanRun, correlationId)
        {
            if (planId == Guid.Empty)
            {
                throw new ArgumentException("Scheduler plan id cannot be empty.", nameof(planId));
            }

            if (planRunId == Guid.Empty)
            {
                throw new ArgumentException("Scheduler plan run id cannot be empty.", nameof(planRunId));
            }

            if (fireId.Value == Guid.Empty)
            {
                throw new ArgumentException("Scheduler fire id cannot be empty.", nameof(fireId));
            }

            if (firedAtUtc == default)
            {
                throw new ArgumentException("Scheduler fired-at timestamp is required.", nameof(firedAtUtc));
            }

            PlanId = planId;
            PlanRunId = planRunId;
            FireId = fireId;
            FiredAtUtc = firedAtUtc;
        }

        /// <summary>Identifier of the scheduler plan.</summary>
        public Guid PlanId { get; }

        /// <summary>Identifier of the scheduler plan run.</summary>
        public Guid PlanRunId { get; }

        /// <summary>Workflow run identifier reserved by the scheduler before the launch; null when none was.</summary>
        public WorkflowRunId? PreparedRunId { get; init; }

        /// <summary>The scheduler firing that launched the run.</summary>
        public WorkflowSchedulerFireId FireId { get; }

        /// <summary>Time the scheduler fired, as an instant with offset.</summary>
        public DateTimeOffset FiredAtUtc { get; }
    }

    /// <summary>
    /// Origin of a run started from a Project Structure workflow node (<c>$origin</c> <c>project-structure-node</c>).
    /// </summary>
    public sealed record ProjectStructureNode : WorkflowLaunchOrigin
    {
        [JsonConstructor]
        public ProjectStructureNode(
            Guid projectId,
            WorkflowProjectStructureNodeId nodeId,
            WorkflowLaunchActor requestingActor,
            WorkflowLaunchSessionId sessionId,
            WorkflowLaunchCorrelationId correlationId)
            : base(WorkflowLaunchOriginKind.ProjectStructureNode, correlationId)
        {
            if (projectId == Guid.Empty)
            {
                throw new ArgumentException("Project id cannot be empty.", nameof(projectId));
            }

            if (string.IsNullOrWhiteSpace(nodeId.Value))
            {
                throw new ArgumentException("Project-structure node id is required.", nameof(nodeId));
            }

            ArgumentNullException.ThrowIfNull(requestingActor);

            if (string.IsNullOrWhiteSpace(sessionId.Value))
            {
                throw new ArgumentException("Project-structure agent session id is required.", nameof(sessionId));
            }

            ProjectId = projectId;
            NodeId = nodeId;
            RequestingActor = requestingActor;
            SessionId = sessionId;
        }

        /// <summary>Identifier of the project that contains the workflow node.</summary>
        public Guid ProjectId { get; }

        /// <summary>The Project Structure workflow node that started the run.</summary>
        public WorkflowProjectStructureNodeId NodeId { get; }

        /// <summary>User or agent that started the node.</summary>
        public WorkflowLaunchActor RequestingActor { get; }

        /// <summary>Session in which the node was started.</summary>
        public WorkflowLaunchSessionId SessionId { get; }

        /// <summary>Admission that binds the run's outputs to the project; null for older runs.</summary>
        public WorkflowStructureAdmissionBinding? StructureAdmission { get; init; }
    }

    /// <summary>
    /// Origin of a run started by an agent tool (<c>$origin</c> <c>agent-runtime-invocation</c>).
    /// </summary>
    public sealed record AgentRuntimeInvocation : WorkflowLaunchOrigin
    {
        [JsonConstructor]
        public AgentRuntimeInvocation(
            WorkflowLaunchActor agent,
            WorkflowLaunchSessionId runtimeSessionId,
            string purpose,
            WorkflowLaunchCorrelationId correlationId)
            : base(WorkflowLaunchOriginKind.AgentRuntimeInvocation, correlationId)
        {
            ArgumentNullException.ThrowIfNull(agent);
            if (agent.Kind != WorkflowLaunchActorKind.Agent)
            {
                throw new ArgumentException("Agent runtime workflow origin requires an agent actor.", nameof(agent));
            }

            if (string.IsNullOrWhiteSpace(runtimeSessionId.Value))
            {
                throw new ArgumentException("Agent runtime session id is required.", nameof(runtimeSessionId));
            }

            ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
            Agent = agent;
            RuntimeSessionId = runtimeSessionId;
            Purpose = purpose.Trim();
        }

        /// <summary>The agent that started the run.</summary>
        public WorkflowLaunchActor Agent { get; }

        /// <summary>Agent runtime session in which the tool ran.</summary>
        public WorkflowLaunchSessionId RuntimeSessionId { get; }

        /// <summary>Execution purpose of the agent invocation, trimmed.</summary>
        public string Purpose { get; }
    }

    /// <summary>
    /// Origin of a run started by an agent tool within a process step (<c>$origin</c>
    /// <c>process-tool-invocation-v1</c>).
    /// </summary>
    public sealed record ProcessToolInvocation : WorkflowLaunchOrigin {
        [JsonConstructor]
        public ProcessToolInvocation(WorkflowProcessToolBinding invocation, WorkflowLaunchCorrelationId correlationId)
            : base(WorkflowLaunchOriginKind.ProcessToolInvocation, correlationId) {
            ArgumentNullException.ThrowIfNull(invocation);
            invocation.Validate();
            Invocation = invocation;
        }

        /// <summary>The approved tool proposal and process step that admitted the run.</summary>
        public WorkflowProcessToolBinding Invocation { get; }
    }

    /// <summary>
    /// Origin of a run dispatched for a process step assignment (<c>$origin</c> <c>process-dispatch-assignment-v1</c>).
    /// </summary>
    public sealed record ProcessDispatchAssignment : WorkflowLaunchOrigin {
        [JsonConstructor]
        public ProcessDispatchAssignment(WorkflowProcessDispatchBinding dispatch, WorkflowLaunchCorrelationId correlationId)
            : base(WorkflowLaunchOriginKind.ProcessDispatchAssignment, correlationId) {
            ArgumentNullException.ThrowIfNull(dispatch);
            dispatch.Validate();
            Dispatch = dispatch;
        }

        /// <summary>The process assignment claim that dispatched the run.</summary>
        public WorkflowProcessDispatchBinding Dispatch { get; }
    }

    /// <summary>
    /// Origin of a run started for a process step assignment (<c>$origin</c> <c>process-assignment</c>); the legacy
    /// form, kept readable for older runs.
    /// </summary>
    public sealed record ProcessAssignment : WorkflowLaunchOrigin
    {
        [JsonConstructor]
        public ProcessAssignment(
            Guid processRunId,
            Guid assignmentId,
            WorkflowLaunchCorrelationId correlationId)
            : base(WorkflowLaunchOriginKind.ProcessAssignment, correlationId)
        {
            ProcessRun = new WorkflowProcessRunId(processRunId);
            Assignment = new WorkflowProcessAssignmentId(assignmentId);
        }

        public ProcessAssignment(
            WorkflowProcessRunId processRun,
            WorkflowProcessAssignmentId assignment,
            WorkflowLaunchCorrelationId correlationId)
            : this(processRun.Value, assignment.Value, correlationId)
        {
        }

        [JsonIgnore]
        public WorkflowProcessRunId ProcessRun { get; }

        [JsonIgnore]
        public WorkflowProcessAssignmentId Assignment { get; }

        /// <summary>Identifier of the process run.</summary>
        public Guid ProcessRunId => ProcessRun.Value;

        /// <summary>Identifier of the process step assignment.</summary>
        public Guid AssignmentId => Assignment.Value;
    }
}

public readonly record struct WorkflowLaunchIdempotencyKey
{
    [JsonConstructor]
    public WorkflowLaunchIdempotencyKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim();
        if (normalized.Length > 256)
        {
            throw new ArgumentException("Workflow launch idempotency key cannot exceed 256 characters.", nameof(value));
        }

        Value = normalized;
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct WorkflowLaunchOriginScopeKey
{
    public WorkflowLaunchOriginScopeKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct WorkflowLaunchRequestFingerprint
{
    public WorkflowLaunchRequestFingerprint(
        string value,
        string canonicalInputHash = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
        CanonicalInputHash = canonicalInputHash.Trim();
    }

    public string Value { get; }

    public string CanonicalInputHash { get; }

    public override string ToString() => Value;
}

public readonly record struct WorkflowLaunchIdempotencyClaimToken
{
    public WorkflowLaunchIdempotencyClaimToken(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Workflow launch idempotency claim token cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static WorkflowLaunchIdempotencyClaimToken New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public sealed record WorkflowLaunchIdempotencyScope(
    WorkflowLaunchIdempotencyKey CallerKey,
    WorkflowId WorkflowId,
    WorkflowDefinitionSelectionKind SelectionKind,
    WorkflowVersionId? RequestedVersionId,
    WorkflowLaunchMode Mode,
    WorkflowLaunchOriginKind OriginKind,
    WorkflowLaunchOriginScopeKey OriginScopeKey);

public enum WorkflowLaunchIdempotencyClaimOutcome
{
    Acquired,
    InProgress,
    Completed
}

public sealed record WorkflowLaunchIdempotencyCompletion(
    WorkflowRunSnapshot Run,
    WorkflowResolvedRuntimeRequest ResolvedRequest,
    DateTimeOffset CompletedAtUtc) {
    public WorkflowLaunchObservation Observation { get; init; }
}

public sealed record WorkflowLaunchIdempotencyClaimResult(
    WorkflowLaunchIdempotencyClaimOutcome Outcome,
    WorkflowRunId? ReservedRunId = null,
    WorkflowLaunchIdempotencyCompletion? Completion = null);

/// <summary>
/// State of a start request's idempotency record, as a JSON integer: 0 Pending (the start is in progress, or it did
/// not finish recording its run), 1 Completed (the run was recorded; later requests with the key replay it).
/// </summary>
public enum WorkflowLaunchIdempotencyRecordState
{
    Pending,
    Completed
}

public sealed record WorkflowLaunchIdempotencyRecord(
    WorkflowLaunchIdempotencyScope Scope,
    WorkflowLaunchRequestFingerprint Fingerprint,
    WorkflowRunId OriginalRunId,
    WorkflowLaunchIdempotencyRecordState State,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    int ReplayCount,
    DateTimeOffset? LastReplayedAtUtc,
    WorkflowLaunchIdempotencyCompletion? Completion);

/// <summary>
/// What the server recorded for an <c>Idempotency-Key</c> sent to the HTTP start operations, returned by
/// <c>GET /api/workflows/runs/by-idempotency-key/{key}</c>. It identifies the original run and never contains the key
/// or the run input, only hashes of them.
/// </summary>
/// <param name="IdempotencyKeyHash">SHA-256 hash of the trimmed key, as upper-case hexadecimal.</param>
/// <param name="RequestFingerprint">
/// SHA-256 fingerprint of the original start request (version choice, canonical input, backend and authorization
/// context), as upper-case hexadecimal. A later request with the same key must produce the same fingerprint.
/// </param>
/// <param name="CanonicalInputHash">
/// SHA-256 hash of the run input with its object members sorted by name, as upper-case hexadecimal.
/// </param>
/// <param name="WorkflowId">Workflow the key was used for.</param>
/// <param name="SelectionKind">
/// How the version was chosen, as a JSON integer: 0 ExactSavedVersion (a <c>versionId</c> was sent), 1 LatestActive
/// (none was sent).
/// </param>
/// <param name="RequestedVersionId">Version sent in the request; null for latest-Active starts.</param>
/// <param name="ResolvedVersionId">Version that was started; null while the record is Pending.</param>
/// <param name="ResolvedBackend">
/// Backend the run was started on, as a JSON integer: 0 InProcess, 1 DurableTask, 2 AzureFunctions; null while the
/// record is Pending.
/// </param>
/// <param name="OriginalRunId">Run reserved for and created by the original request.</param>
/// <param name="ClaimState">
/// State of the record, as a JSON integer: 0 Pending (the start is in progress or did not finish recording its run),
/// 1 Completed.
/// </param>
/// <param name="RunState">
/// Current state of the original run, as a JSON integer: 0 NotStarted, 1 Running, 2 WaitingForInput, 3 Idle,
/// 4 Completed, 5 Failed, 6 Cancelled; null when the run is not recorded.
/// </param>
/// <param name="IsTerminal">True when the run is Completed, Failed or Cancelled.</param>
/// <param name="CreatedAtUtc">
/// Time the key was claimed for a start, as an instant with offset (the latest claim when an abandoned claim was
/// taken over).
/// </param>
/// <param name="CompletedAtUtc">Time the start was recorded as completed; null while Pending.</param>
/// <param name="WasReplayed">True when at least one later request with the key returned the original run.</param>
/// <param name="ReplayCount">Number of later requests with the key that returned the original run.</param>
/// <param name="LastReplayedAtUtc">Time of the latest such request; null when there was none.</param>
public sealed record WorkflowLaunchIdempotencyEvidence(
    string IdempotencyKeyHash,
    string RequestFingerprint,
    string CanonicalInputHash,
    WorkflowId WorkflowId,
    WorkflowDefinitionSelectionKind SelectionKind,
    WorkflowVersionId? RequestedVersionId,
    WorkflowVersionId? ResolvedVersionId,
    WorkflowRuntimeBackendKind? ResolvedBackend,
    WorkflowRunId OriginalRunId,
    WorkflowLaunchIdempotencyRecordState ClaimState,
    WorkflowRunState? RunState,
    bool IsTerminal,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    bool WasReplayed,
    int ReplayCount,
    DateTimeOffset? LastReplayedAtUtc);

public enum WorkflowLaunchIdempotencyKind
{
    NotRequested,
    CallerSupplied
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$idempotency")]
[JsonDerivedType(typeof(WorkflowLaunchIdempotency.NotRequested), "not-requested")]
[JsonDerivedType(typeof(WorkflowLaunchIdempotency.CallerSupplied), "caller-supplied")]
public abstract record WorkflowLaunchIdempotency
{
    private WorkflowLaunchIdempotency(WorkflowLaunchIdempotencyKind kind)
    {
        Kind = kind;
    }

    public WorkflowLaunchIdempotencyKind Kind { get; }

    public sealed record NotRequested() :
        WorkflowLaunchIdempotency(WorkflowLaunchIdempotencyKind.NotRequested);

    public sealed record CallerSupplied(WorkflowLaunchIdempotencyKey Key) :
        WorkflowLaunchIdempotency(WorkflowLaunchIdempotencyKind.CallerSupplied);
}

public sealed record WorkflowLaunchIntent(
    WorkflowDefinitionSelection Selection,
    WorkflowLaunchMode Mode,
    WorkflowLaunchOrigin Origin,
    string InputJson,
    WorkflowLaunchCompletionPolicy CompletionPolicy,
    WorkflowLaunchIdempotency Idempotency)
{
    public WorkflowRuntimeBackendKind? RequestedBackend { get; init; }

    public WorkflowPreviewSimulationPlan PreviewSimulationPlan { get; init; } = WorkflowPreviewSimulationPlan.Empty;
}

public sealed record WorkflowResolvedRuntimeRequest(
    WorkflowDefinition Definition,
    string InputJson,
    WorkflowRuntimeBackendDescriptor Backend,
    WorkflowPreviewSimulationPlan PreviewSimulationPlan,
    WorkflowLaunchMode Mode,
    WorkflowLaunchOrigin Origin,
    WorkflowLaunchCompletionPolicy CompletionPolicy,
    WorkflowLaunchIdempotency Idempotency,
    DateTimeOffset ResolvedAtUtc)
{
    public WorkflowRunId? RequestedRunId { get; init; }
}

/// <summary>
/// How a start request's <c>Idempotency-Key</c> was applied, as a JSON integer: 0 NotRequested (no key was sent; a
/// new run was started), 1 EnforcedNewRun (the key was recorded with a new run), 2 ReplayedExistingRun (the key was
/// known; the original run is returned and nothing new was started).
/// </summary>
public enum WorkflowLaunchIdempotencyDisposition
{
    NotRequested,
    EnforcedNewRun,
    ReplayedExistingRun
}

public sealed record WorkflowLaunchResult(
    WorkflowRunSnapshot Run,
    WorkflowResolvedRuntimeRequest ResolvedRequest,
    WorkflowLaunchIdempotencyDisposition IdempotencyDisposition) {
    public WorkflowLaunchObservation Observation { get; init; }

    [JsonIgnore]
    public Exception? ObservationException { get; init; }

    [JsonIgnore]
    public Exception? ReceiptObservationException { get; init; }
}

/// <summary>
/// How reliably the server observed a workflow start, as a JSON integer: 0 Confirmed (the run was admitted and
/// recorded normally), 1 RecoveredAfterObserverFailure (an error was reported after the run had been admitted, for
/// example because the run failed or its detail could not be read; the result was rebuilt from the stored run),
/// 2 AdmissionReceiptPending (the run was admitted but its idempotency record could not be completed yet; a later
/// request with the same key returns the same run).
/// </summary>
public enum WorkflowLaunchObservation {
    Confirmed,
    RecoveredAfterObserverFailure,
    AdmissionReceiptPending
}
