using System.Text.Json.Serialization;

namespace CanDoItAll.Modules.CognitiveMemory;

public enum CognitiveMemoryEvidenceAnchorKind
{
    TextSpan = 0,
    StructuredPath = 1,
    FilePath = 2,
    RepositoryLocation = 3,
    MindMapNode = 4,
    WorkflowArtifact = 5,
    ProcessEvent = 6,
    ProbeTurn = 7,
    ReviewDecision = 8,
    SelfRegulationAssessment = 9,
    ProfessorReview = 10,
    CalibrationOutcome = 11,
    CuratorConversationTurn = 12
}

public enum CognitiveMemoryEvidenceDirection
{
    Supports = 0,
    Attacks = 1,
    Qualifies = 2,
    Supersedes = 3,
    NarrowsScope = 4,
    BroadensScope = 5,
    Example = 6,
    CounterExample = 7,
    SelfRegulationContext = 8,
    ProfessorReviewChallenge = 9,
    CalibrationEvidence = 10
}

public enum CognitiveMemoryClaimKind
{
    Fact = 0,
    Decision = 1,
    Requirement = 2,
    Policy = 3,
    ProcedureConstraint = 4,
    Observation = 5,
    FailureMode = 6,
    Hypothesis = 7
}

public enum CognitiveMemoryBeliefStateKind
{
    Unexamined = 0,
    Supported = 1,
    WeaklySupported = 2,
    Contested = 3,
    Contradicted = 4,
    ScopeLimited = 5,
    Stale = 6,
    Superseded = 7,
    Rejected = 8,
    Validated = 9
}

public enum CognitiveMemorySourceTrustLevel
{
    Unknown = 0,
    RuntimeSource = 1,
    HumanReview = 2,
    OfficialSource = 3,
    GeneratedSynthesis = 4,
    ExternalUnverified = 5
}

public enum CognitiveMemoryEntityKind
{
    Project = 0,
    Module = 1,
    Plugin = 2,
    Workflow = 3,
    Process = 4,
    Agent = 5,
    UserRole = 6,
    SourceSystem = 7,
    Environment = 8,
    RepositoryBranch = 9,
    TechnologyTopic = 10,
    ProcedureTarget = 11,
    BusinessObject = 12,
    Artifact = 13
}

public enum CognitiveMemoryContextFrameKind
{
    Project = 0,
    Environment = 1,
    Runtime = 2,
    Process = 3,
    Role = 4,
    Temporal = 5,
    SourceTrust = 6,
    Risk = 7,
    AccessScope = 8,
    Composite = 9
}

public enum CognitiveMemoryContextDimensionKind
{
    Project = 0,
    Environment = 1,
    Runtime = 2,
    Process = 3,
    Role = 4,
    TimeRange = 5,
    SourceTrust = 6,
    Risk = 7,
    AccessScope = 8,
    Version = 9,
    Branch = 10,
    Platform = 11
}

public enum CognitiveMemoryContextBoundaryKind
{
    EnvironmentBoundary = 0,
    RuntimeBoundary = 1,
    ProjectBoundary = 2,
    AccessBoundary = 3,
    TemporalBoundary = 4,
    RiskBoundary = 5
}

public enum CognitiveMemoryContextBoundaryPolicy
{
    RelatedNotSubstitutable = 0,
    EquivalentWithinScope = 1,
    Incompatible = 2,
    RequiresHumanReview = 3
}

public enum CognitiveMemoryMutationCommandKind
{
    ProposeClaim = 0,
    SupportClaim = 1,
    AttackClaim = 2,
    NarrowScope = 3,
    BroadenScope = 4,
    SupersedeClaim = 5,
    RejectClaim = 6,
    ValidateClaim = 7,
    RetireClaim = 8,
    CreateRelation = 9,
    InvalidateProjection = 10,
    RecordEvidence = 11
}

public enum CognitiveMemoryActorKind
{
    User = 0,
    Agent = 1,
    WorkflowExecutor = 2,
    ProcessRole = 3,
    DistributedWorker = 4,
    System = 5
}

public enum CognitiveMemoryMutationCommandStatus
{
    Accepted = 0,
    Rejected = 1,
    ReviewRequired = 2
}

public enum CognitiveMemoryMutationAuditEventKind
{
    Submitted = 0,
    Rejected = 1,
    IdempotentReplay = 2,
    ReviewRequired = 3,
    AcceptedForHandler = 4
}

public enum CognitiveMemoryProjectionPayloadSchemaKind
{
    Unknown = 0,
    ClaimContainer = 1,
    EvidenceAnchor = 2,
    ContextFrame = 3,
    Entity = 4
}

public enum CognitiveMemoryProjectionPayloadValidationIssue
{
    MissingSchemaVersion = 0,
    UnknownSchemaKind = 1,
    MissingClaimIds = 2,
    MissingContextFrameIds = 3,
    MissingBeliefStates = 4,
    MissingEntityOrBoundaryMetadata = 5
}

public readonly record struct CognitiveMemoryEvidenceAnchorId
{
    [JsonConstructor]
    public CognitiveMemoryEvidenceAnchorId(Guid value)
    {
        Value = CognitiveMemoryGuard.EnsureNonEmpty(value, nameof(value));
    }

    public Guid Value { get; }

    public static CognitiveMemoryEvidenceAnchorId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct CognitiveMemoryClaimId
{
    [JsonConstructor]
    public CognitiveMemoryClaimId(Guid value)
    {
        Value = CognitiveMemoryGuard.EnsureNonEmpty(value, nameof(value));
    }

    public Guid Value { get; }

    public static CognitiveMemoryClaimId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct CognitiveMemoryContextFrameId
{
    [JsonConstructor]
    public CognitiveMemoryContextFrameId(Guid value)
    {
        Value = CognitiveMemoryGuard.EnsureNonEmpty(value, nameof(value));
    }

    public Guid Value { get; }

    public static CognitiveMemoryContextFrameId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct CognitiveMemoryEntityId
{
    [JsonConstructor]
    public CognitiveMemoryEntityId(Guid value)
    {
        Value = CognitiveMemoryGuard.EnsureNonEmpty(value, nameof(value));
    }

    public Guid Value { get; }

    public static CognitiveMemoryEntityId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct CognitiveMemoryMutationCommandId
{
    [JsonConstructor]
    public CognitiveMemoryMutationCommandId(Guid value)
    {
        Value = CognitiveMemoryGuard.EnsureNonEmpty(value, nameof(value));
    }

    public Guid Value { get; }

    public static CognitiveMemoryMutationCommandId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public sealed record CognitiveMemoryEvidenceAnchorQuery
{
    public CognitiveMemoryEvidenceAnchorQuery(
        Guid? projectId,
        CognitiveMemorySourceManifestId? sourceManifestId,
        CognitiveMemorySourceItemId? sourceItemId,
        IReadOnlyList<CognitiveMemoryEvidenceAnchorKind>? anchorKinds,
        IReadOnlyList<CognitiveMemorySourceTrustLevel>? trustLevels,
        DateTimeOffset? observedFromUtc,
        DateTimeOffset? observedToUtc,
        CognitiveMemoryPageRequest? page = null)
    {
        CognitiveMemoryNeuroContractGuard.EnsureUtcRange(observedFromUtc, observedToUtc, nameof(observedFromUtc), nameof(observedToUtc));

        ProjectId = projectId;
        SourceManifestId = sourceManifestId;
        SourceItemId = sourceItemId;
        AnchorKinds = CognitiveMemoryNeuroContractGuard.NormalizeList(anchorKinds);
        TrustLevels = CognitiveMemoryNeuroContractGuard.NormalizeList(trustLevels);
        ObservedFromUtc = observedFromUtc;
        ObservedToUtc = observedToUtc;
        Page = page ?? new CognitiveMemoryPageRequest();
    }

    public Guid? ProjectId { get; }

    public CognitiveMemorySourceManifestId? SourceManifestId { get; }

    public CognitiveMemorySourceItemId? SourceItemId { get; }

    public IReadOnlyList<CognitiveMemoryEvidenceAnchorKind> AnchorKinds { get; }

    public IReadOnlyList<CognitiveMemorySourceTrustLevel> TrustLevels { get; }

    public DateTimeOffset? ObservedFromUtc { get; }

    public DateTimeOffset? ObservedToUtc { get; }

    public CognitiveMemoryPageRequest Page { get; }
}

public sealed record CognitiveMemoryClaimQuery
{
    public CognitiveMemoryClaimQuery(
        Guid? projectId,
        CognitiveMemoryRecordId? memoryRecordId,
        CognitiveMemoryContextFrameId? primaryContextFrameId,
        IReadOnlyList<CognitiveMemoryClaimKind>? claimKinds,
        IReadOnlyList<CognitiveMemoryBeliefStateKind>? beliefStates,
        IReadOnlyList<CognitiveMemoryValidationState>? validationStates,
        DateTimeOffset? validAtUtc,
        CognitiveMemoryPageRequest? page = null)
    {
        ProjectId = projectId;
        MemoryRecordId = memoryRecordId;
        PrimaryContextFrameId = primaryContextFrameId;
        ClaimKinds = CognitiveMemoryNeuroContractGuard.NormalizeList(claimKinds);
        BeliefStates = CognitiveMemoryNeuroContractGuard.NormalizeList(beliefStates);
        ValidationStates = CognitiveMemoryNeuroContractGuard.NormalizeList(validationStates);
        ValidAtUtc = validAtUtc;
        Page = page ?? new CognitiveMemoryPageRequest();
    }

    public Guid? ProjectId { get; }

    public CognitiveMemoryRecordId? MemoryRecordId { get; }

    public CognitiveMemoryContextFrameId? PrimaryContextFrameId { get; }

    public IReadOnlyList<CognitiveMemoryClaimKind> ClaimKinds { get; }

    public IReadOnlyList<CognitiveMemoryBeliefStateKind> BeliefStates { get; }

    public IReadOnlyList<CognitiveMemoryValidationState> ValidationStates { get; }

    public DateTimeOffset? ValidAtUtc { get; }

    public CognitiveMemoryPageRequest Page { get; }
}

public sealed record CognitiveMemoryContextFrameQuery
{
    public CognitiveMemoryContextFrameQuery(
        Guid? projectId,
        IReadOnlyList<CognitiveMemoryContextFrameKind>? frameKinds,
        CognitiveMemoryContextDimensionKind? dimensionKind,
        string? dimensionValueKey,
        IReadOnlyList<CognitiveMemoryContextBoundaryPolicy>? boundaryPolicies,
        CognitiveMemoryPageRequest? page = null)
    {
        ProjectId = projectId;
        FrameKinds = CognitiveMemoryNeuroContractGuard.NormalizeList(frameKinds);
        DimensionKind = dimensionKind;
        DimensionValueKey = string.IsNullOrWhiteSpace(dimensionValueKey) ? null : dimensionValueKey.Trim();
        BoundaryPolicies = CognitiveMemoryNeuroContractGuard.NormalizeList(boundaryPolicies);
        Page = page ?? new CognitiveMemoryPageRequest();
    }

    public Guid? ProjectId { get; }

    public IReadOnlyList<CognitiveMemoryContextFrameKind> FrameKinds { get; }

    public CognitiveMemoryContextDimensionKind? DimensionKind { get; }

    public string? DimensionValueKey { get; }

    public IReadOnlyList<CognitiveMemoryContextBoundaryPolicy> BoundaryPolicies { get; }

    public CognitiveMemoryPageRequest Page { get; }
}

public sealed record CognitiveMemoryMutationAuditQuery
{
    public CognitiveMemoryMutationAuditQuery(
        Guid? projectId,
        CognitiveMemoryMutationCommandId? mutationCommandId,
        IReadOnlyList<CognitiveMemoryMutationAuditEventKind>? eventKinds,
        DateTimeOffset? createdFromUtc,
        DateTimeOffset? createdToUtc,
        CognitiveMemoryPageRequest? page = null)
    {
        CognitiveMemoryNeuroContractGuard.EnsureUtcRange(createdFromUtc, createdToUtc, nameof(createdFromUtc), nameof(createdToUtc));

        ProjectId = projectId;
        MutationCommandId = mutationCommandId;
        EventKinds = CognitiveMemoryNeuroContractGuard.NormalizeList(eventKinds);
        CreatedFromUtc = createdFromUtc;
        CreatedToUtc = createdToUtc;
        Page = page ?? new CognitiveMemoryPageRequest();
    }

    public Guid? ProjectId { get; }

    public CognitiveMemoryMutationCommandId? MutationCommandId { get; }

    public IReadOnlyList<CognitiveMemoryMutationAuditEventKind> EventKinds { get; }

    public DateTimeOffset? CreatedFromUtc { get; }

    public DateTimeOffset? CreatedToUtc { get; }

    public CognitiveMemoryPageRequest Page { get; }
}

public sealed record CognitiveMemoryClaimProjectionPayload
{
    public CognitiveMemoryClaimProjectionPayload(
        CognitiveMemoryPayloadSchemaVersion schemaVersion,
        CognitiveMemoryProjectionPayloadSchemaKind schemaKind,
        CognitiveMemoryRecordId memoryRecordId,
        IReadOnlyList<CognitiveMemoryClaimId>? claimIds,
        IReadOnlyList<CognitiveMemoryContextFrameId>? contextFrameIds,
        IReadOnlyList<CognitiveMemoryEntityId>? entityIds,
        IReadOnlyList<CognitiveMemoryBeliefStateKind>? beliefStates,
        IReadOnlyList<CognitiveMemoryContextBoundaryPolicy>? contextBoundaryPolicies,
        CognitiveMemoryScoreProjectionBucket beliefBucket)
    {
        SchemaVersion = schemaVersion;
        SchemaKind = schemaKind;
        MemoryRecordId = memoryRecordId;
        ClaimIds = CognitiveMemoryNeuroContractGuard.NormalizeList(claimIds);
        ContextFrameIds = CognitiveMemoryNeuroContractGuard.NormalizeList(contextFrameIds);
        EntityIds = CognitiveMemoryNeuroContractGuard.NormalizeList(entityIds);
        BeliefStates = CognitiveMemoryNeuroContractGuard.NormalizeList(beliefStates);
        ContextBoundaryPolicies = CognitiveMemoryNeuroContractGuard.NormalizeList(contextBoundaryPolicies);
        BeliefBucket = beliefBucket;
    }

    public CognitiveMemoryPayloadSchemaVersion SchemaVersion { get; }

    public CognitiveMemoryProjectionPayloadSchemaKind SchemaKind { get; }

    public CognitiveMemoryRecordId MemoryRecordId { get; }

    public IReadOnlyList<CognitiveMemoryClaimId> ClaimIds { get; }

    public IReadOnlyList<CognitiveMemoryContextFrameId> ContextFrameIds { get; }

    public IReadOnlyList<CognitiveMemoryEntityId> EntityIds { get; }

    public IReadOnlyList<CognitiveMemoryBeliefStateKind> BeliefStates { get; }

    public IReadOnlyList<CognitiveMemoryContextBoundaryPolicy> ContextBoundaryPolicies { get; }

    public CognitiveMemoryScoreProjectionBucket BeliefBucket { get; }
}

public sealed record CognitiveMemoryProjectionPayloadValidationResult(
    bool IsValid,
    IReadOnlyList<CognitiveMemoryProjectionPayloadValidationIssue> Issues)
{
    public void ThrowIfInvalid()
    {
        if (!IsValid)
        {
            throw new InvalidOperationException($"Cognitive memory projection payload is invalid: {string.Join(", ", Issues)}.");
        }
    }
}

public static class CognitiveMemoryProjectionPayloadValidator
{
    public static CognitiveMemoryProjectionPayloadValidationResult Validate(CognitiveMemoryClaimProjectionPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var issues = new List<CognitiveMemoryProjectionPayloadValidationIssue>();
        if (string.IsNullOrWhiteSpace(payload.SchemaVersion.Value))
        {
            issues.Add(CognitiveMemoryProjectionPayloadValidationIssue.MissingSchemaVersion);
        }

        if (payload.SchemaKind == CognitiveMemoryProjectionPayloadSchemaKind.Unknown)
        {
            issues.Add(CognitiveMemoryProjectionPayloadValidationIssue.UnknownSchemaKind);
        }

        if (payload.ClaimIds.Count == 0)
        {
            issues.Add(CognitiveMemoryProjectionPayloadValidationIssue.MissingClaimIds);
        }

        if (payload.ContextFrameIds.Count == 0)
        {
            issues.Add(CognitiveMemoryProjectionPayloadValidationIssue.MissingContextFrameIds);
        }

        if (payload.BeliefStates.Count == 0)
        {
            issues.Add(CognitiveMemoryProjectionPayloadValidationIssue.MissingBeliefStates);
        }

        if (payload.EntityIds.Count == 0 && payload.ContextBoundaryPolicies.Count == 0)
        {
            issues.Add(CognitiveMemoryProjectionPayloadValidationIssue.MissingEntityOrBoundaryMetadata);
        }

        return new CognitiveMemoryProjectionPayloadValidationResult(issues.Count == 0, issues);
    }
}

public sealed record CognitiveMemoryMutationCommand(
    Guid? ProjectId,
    CognitiveMemoryMutationCommandKind CommandKind,
    CognitiveMemoryActorKind ActorKind,
    string ActorId,
    CognitiveMemoryIdempotencyKey IdempotencyKey,
    IReadOnlyList<Guid> AffectedMemoryRecordIds,
    IReadOnlyList<Guid> AffectedClaimIds,
    IReadOnlyList<Guid> EvidenceAnchorIds,
    string PayloadJson,
    string? ExpectedVersionToken,
    bool RequiresHumanReview,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record CognitiveMemoryMutationResult(
    Guid CommandId,
    bool Accepted,
    bool Applied,
    bool ReviewRequired,
    string? ReviewReason,
    string? NewVersionToken,
    IReadOnlyList<Guid> CreatedAuditEventIds,
    IReadOnlyList<string> Warnings);

public interface ICognitiveMemoryMutationAuthority
{
    ValueTask<CognitiveMemoryMutationResult> SubmitAsync(
        CognitiveMemoryMutationCommand command,
        CancellationToken cancellationToken = default);
}

public static class CognitiveMemoryNeuroFoundationPolicies
{
    public static bool RequiresEvidenceAnchors(CognitiveMemoryMutationCommandKind commandKind)
        => commandKind is CognitiveMemoryMutationCommandKind.ProposeClaim
            or CognitiveMemoryMutationCommandKind.SupportClaim
            or CognitiveMemoryMutationCommandKind.AttackClaim
            or CognitiveMemoryMutationCommandKind.NarrowScope
            or CognitiveMemoryMutationCommandKind.BroadenScope
            or CognitiveMemoryMutationCommandKind.SupersedeClaim
            or CognitiveMemoryMutationCommandKind.RejectClaim
            or CognitiveMemoryMutationCommandKind.ValidateClaim
            or CognitiveMemoryMutationCommandKind.RetireClaim
            or CognitiveMemoryMutationCommandKind.RecordEvidence;
}

internal static class CognitiveMemoryNeuroContractGuard
{
    public static IReadOnlyList<T> NormalizeList<T>(IReadOnlyList<T>? values)
        => values is null
            ? []
            : values;

    public static void EnsureUtcRange(
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        string fromParameterName,
        string toParameterName)
    {
        if (fromUtc is not { } from || toUtc is not { } to || from <= to)
        {
            return;
        }

        throw new ArgumentOutOfRangeException(fromParameterName, $"Start timestamp must not be after {toParameterName}.");
    }
}
