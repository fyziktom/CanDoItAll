using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.ProviderHistory;

namespace CanDoItAll.AgentFramework.Models;

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

public enum WorkflowLaunchActorKind
{
    User,
    Agent,
    Service
}

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

    public WorkflowLaunchActorKind Kind { get; }

    public string SubjectId { get; }
}

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

    public Guid Value { get; }

    public override string ToString() => Value.ToString("D");
}

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

    public Guid Value { get; }

    public override string ToString() => Value.ToString("D");
}

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

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct WorkflowLaunchSessionId
{
    [JsonConstructor]
    public WorkflowLaunchSessionId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }

    public string Value { get; }

    public override string ToString() => Value;
}

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

    public Guid Value { get; }

    public override string ToString() => Value.ToString("D");
}

public readonly record struct WorkflowProjectStructureNodeId
{
    [JsonConstructor]
    public WorkflowProjectStructureNodeId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public enum WorkflowLaunchOriginKind
{
    Api = 0,
    Preview = 1,
    SchedulerPlanRun = 2,
    ProjectStructureNode = 3,
    AgentRuntimeInvocation = 4,
    ProcessAssignment = 5
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$origin")]
[JsonDerivedType(typeof(WorkflowLaunchOrigin.Api), "api")]
[JsonDerivedType(typeof(WorkflowLaunchOrigin.Preview), "preview")]
[JsonDerivedType(typeof(WorkflowLaunchOrigin.SchedulerPlanRun), "scheduler-plan-run")]
[JsonDerivedType(typeof(WorkflowLaunchOrigin.ProjectStructureNode), "project-structure-node")]
[JsonDerivedType(typeof(WorkflowLaunchOrigin.AgentRuntimeInvocation), "agent-runtime-invocation")]
[JsonDerivedType(typeof(WorkflowLaunchOrigin.ProcessAssignment), "process-assignment")]
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

    public WorkflowLaunchOriginKind Kind { get; }

    public WorkflowLaunchCorrelationId CorrelationId { get; }

    public WorkspaceScopeDescriptor? AuthorizationScope { get; init; }

    public string AuthorizationPolicyFingerprint { get; init; } = string.Empty;

    public HistoryCaller? HistoryCaller { get; init; }

    public sealed record Api : WorkflowLaunchOrigin
    {
        [JsonConstructor]
        public Api(WorkflowLaunchActor actor, WorkflowLaunchCorrelationId correlationId)
            : base(WorkflowLaunchOriginKind.Api, correlationId)
        {
            ArgumentNullException.ThrowIfNull(actor);
            Actor = actor;
        }

        public WorkflowLaunchActor Actor { get; }
    }

    public sealed record Preview : WorkflowLaunchOrigin
    {
        [JsonConstructor]
        public Preview(WorkflowLaunchActor actor, WorkflowLaunchCorrelationId correlationId)
            : base(WorkflowLaunchOriginKind.Preview, correlationId)
        {
            ArgumentNullException.ThrowIfNull(actor);
            Actor = actor;
        }

        public WorkflowLaunchActor Actor { get; }
    }

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

        public Guid PlanId { get; }

        public Guid PlanRunId { get; }

        public WorkflowSchedulerFireId FireId { get; }

        public DateTimeOffset FiredAtUtc { get; }
    }

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
            if (requestingActor.Kind != WorkflowLaunchActorKind.Agent)
            {
                throw new ArgumentException("Project-structure workflow origin requires an agent actor.", nameof(requestingActor));
            }

            if (string.IsNullOrWhiteSpace(sessionId.Value))
            {
                throw new ArgumentException("Project-structure agent session id is required.", nameof(sessionId));
            }

            ProjectId = projectId;
            NodeId = nodeId;
            RequestingActor = requestingActor;
            SessionId = sessionId;
        }

        public Guid ProjectId { get; }

        public WorkflowProjectStructureNodeId NodeId { get; }

        public WorkflowLaunchActor RequestingActor { get; }

        public WorkflowLaunchSessionId SessionId { get; }
    }

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

        public WorkflowLaunchActor Agent { get; }

        public WorkflowLaunchSessionId RuntimeSessionId { get; }

        public string Purpose { get; }
    }

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

        public Guid ProcessRunId => ProcessRun.Value;

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
    DateTimeOffset CompletedAtUtc);

public sealed record WorkflowLaunchIdempotencyClaimResult(
    WorkflowLaunchIdempotencyClaimOutcome Outcome,
    WorkflowRunId? ReservedRunId = null,
    WorkflowLaunchIdempotencyCompletion? Completion = null);

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

public enum WorkflowLaunchIdempotencyDisposition
{
    NotRequested,
    EnforcedNewRun,
    ReplayedExistingRun
}

public sealed record WorkflowLaunchResult(
    WorkflowRunSnapshot Run,
    WorkflowResolvedRuntimeRequest ResolvedRequest,
    WorkflowLaunchIdempotencyDisposition IdempotencyDisposition);
