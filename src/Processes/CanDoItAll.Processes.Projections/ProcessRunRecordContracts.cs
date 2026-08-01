using CanDoItAll.Processes.Abstractions;

namespace CanDoItAll.Processes.Projections;

public static class ProcessRunRecordSchema
{
    public const string CurrentVersion = "1.0";
}

public static class ProcessRunRecordPayloadLimits
{
    public const int MaximumPageSize = 200;
    public const int MaximumRunIdFilterCount = 200;
    public const int MaximumProjectIdFilterCount = 2_000;
    public const int MaximumAnalyticsDaySpan = 366;
    public const int MaximumClaimBatchSize = 100;
    public const int MaximumSteps = 2_048;
    public const int MaximumParticipants = 512;
    public const int MaximumWorkflowIds = 2_048;
    public const int MaximumSubprocessRunIds = 2_048;
    public const int MaximumExecutionRunIds = 4_096;
    public const int MaximumArtifactIds = 4_096;
    public const int MaximumStepDependencyIds = 256;
    public const int MaximumRuntimeEventMinuteBuckets = 10_080;
    public const int MaximumRuntimeEventCategories = 5;
    public const int MaximumCompletenessWarnings = 64;
    public const int MaximumNarrativeItemsPerSection = 64;
    public const int MaximumStepKeyLength = 256;
    public const int MaximumNarrativeOverviewLength = 8_192;
    public const int MaximumNarrativeItemLength = 2_048;
    public const int MaximumFactsPayloadBytes = 8 * 1_024 * 1_024;
    public const int MaximumNarrativePayloadBytes = 512 * 1_024;
}

public enum ProcessRunDisposition
{
    Succeeded,
    Failed,
    Cancelled,
    Escalated,
    Blocked
}

public enum ProcessRunRecordLifecycleState
{
    Current,
    Superseded
}

public enum ProcessRunRecordCompleteness
{
    SeedOnly,
    Partial,
    Complete
}

public enum ProcessRunRecordListPayload
{
    Compact,
    Full
}

public enum ProcessRunRecordSeedValidation
{
    None,
    CurrentReportableSource
}

public enum ProcessRunFactsStatus
{
    Pending,
    Assembling,
    Completed,
    Failed
}

public enum ProcessRunNarrativeStatus
{
    Pending,
    Generating,
    Completed,
    Failed
}

public enum ProcessRunStepOutcome
{
    Unknown,
    Pending,
    Running,
    Waiting,
    Blocked,
    Completed,
    Failed,
    Cancelled,
    Skipped
}

public enum ProcessRunRuntimeEventCategory
{
    RunLifecycle,
    Step,
    Dispatch,
    Manager,
    Other
}

public enum ProcessRunRecordWarningCode
{
    MissingInstancePlan,
    MissingStepAssignments,
    MissingExecutionObservations,
    MissingUsageTelemetry,
    MissingPricing,
    MissingArtifactLineage,
    MissingRuntimeEvents,
    MissingSubprocessEvidence,
    MissingSubprocessParentMetadata,
    SubprocessNonTerminal,
    PrimaryRunNonTerminalAtEscalation,
    SubprocessDepthLimitReached,
    SubprocessDiscoveryFailed,
    StepFactsTruncated,
    StepKeyTruncated,
    StepDependenciesTruncated,
    ParticipantIdsTruncated,
    WorkflowIdsTruncated,
    SubprocessRunIdsTruncated,
    ExecutionRunIdsTruncated,
    ArtifactIdsTruncated,
    RuntimeEventMinuteBucketsTruncated,
    MissingStepTiming,
    InvalidRunTiming,
    UnallocatedUsage,
    MissingStepKey,
    InvalidProjectId,
    PrimaryRunBlocked
}

[Flags]
public enum ProcessRunEvidenceSource
{
    None = 0,
    RuntimeState = 1 << 0,
    InstancePlan = 1 << 1,
    StepAssignments = 1 << 2,
    ExecutionObservations = 1 << 3,
    UsageTelemetry = 1 << 4,
    Pricing = 1 << 5,
    RuntimeEvents = 1 << 6,
    ArtifactLineage = 1 << 7,
    Subprocesses = 1 << 8,
    All = RuntimeState |
        InstancePlan |
        StepAssignments |
        ExecutionObservations |
        UsageTelemetry |
        Pricing |
        RuntimeEvents |
        ArtifactLineage |
        Subprocesses
}

public readonly record struct ProcessRunParticipantId
{
    public ProcessRunParticipantId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Process run participant identifier cannot be empty.", nameof(value));
        }

        var normalized = value.Trim();
        if (normalized.Length > 256)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                normalized.Length,
                "Process run participant identifier cannot exceed 256 characters.");
        }

        Value = normalized;
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct ProcessRunRecordClaimToken
{
    public ProcessRunRecordClaimToken(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Process run record claim token cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static ProcessRunRecordClaimToken New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public sealed record ProcessRunRecordIdentity(
    ProcessRunId RunId,
    ProcessRunId RootRunId,
    ProcessRunId? ParentRunId,
    ProcessInstancePlanId? PlanId,
    ProcessDefinitionId? DefinitionId,
    ProcessDefinitionVersionId? DefinitionVersionId,
    Guid? ProjectId);

public sealed record ProcessRunRecordMetrics(
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset EndedAtUtc,
    long? DurationMilliseconds,
    int TotalStepCount,
    int ExecutableStepCount,
    int CompletedStepCount,
    int FailedStepCount,
    int CancelledStepCount,
    int RepetitionCount,
    int ExecutionCount,
    int ReworkCount,
    int IncidentCount,
    int EscalationCount,
    long InputTokenCount,
    long CachedInputTokenCount,
    long OutputTokenCount,
    long ReasoningTokenCount,
    long TotalTokenCount,
    decimal EstimatedCost,
    decimal ActualCost,
    int ToolCallCount,
    int ArtifactCount,
    int SubprocessCount);

public sealed record ProcessRunStepFact(
    ProcessRunId OwningRunId,
    ProcessStepInstanceId StepInstanceId,
    ProcessStepDefinitionId StepDefinitionId,
    string StepKey,
    ProcessRunStepOutcome Outcome,
    int AttemptCount,
    ProcessRunParticipantId? ParticipantId,
    Guid? WorkflowId,
    IReadOnlyList<ProcessStepInstanceId> DependencyStepIds,
    IReadOnlyList<Guid> ExecutionRunIds,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? EndedAtUtc,
    long? DurationMilliseconds,
    long InputTokenCount,
    long CachedInputTokenCount,
    long OutputTokenCount,
    long ReasoningTokenCount,
    long TotalTokenCount,
    decimal EstimatedCost,
    decimal ActualCost,
    int ToolCallCount,
    int ArtifactCount);

public sealed record ProcessRunRuntimeEventMinuteBucket(
    DateTimeOffset MinuteUtc,
    int EventCount,
    int ManagerEventCount,
    long DurationMilliseconds);

public sealed record ProcessRunRuntimeEventCategoryAggregate(
    ProcessRunRuntimeEventCategory Category,
    int EventCount,
    DateTimeOffset FirstOccurredAtUtc,
    DateTimeOffset LastOccurredAtUtc);

public sealed record ProcessRunHardFacts(
    IReadOnlyList<ProcessRunStepFact> Steps,
    IReadOnlyList<ProcessRunParticipantId> ParticipantIds,
    IReadOnlyList<Guid> WorkflowIds,
    IReadOnlyList<ProcessRunId> SubprocessRunIds,
    IReadOnlyList<Guid> ExecutionRunIds,
    IReadOnlyList<ArtifactInstanceId> ArtifactIds)
{
    public int TotalRuntimeEventCount { get; init; }

    public int ManagerRuntimeEventCount { get; init; }

    public IReadOnlyList<ProcessRunRuntimeEventMinuteBucket> RuntimeEventMinuteBuckets { get; init; } = [];

    public IReadOnlyList<ProcessRunRuntimeEventCategoryAggregate> RuntimeEventCategories { get; init; } = [];
}

public sealed record ProcessRunNarrativeProvenance(
    ProcessRunParticipantId ManagerAgentId,
    Guid NarrativeExecutionRunId,
    string GenerationPolicyId,
    string ModelId,
    DateTimeOffset GeneratedAtUtc);

public sealed record ProcessRunNarrative(
    string Overview,
    string Outcome,
    IReadOnlyList<string> WorkCompleted,
    IReadOnlyList<string> Problems,
    IReadOnlyList<string> Decisions,
    IReadOnlyList<string> FollowUps,
    ProcessRunNarrativeProvenance Provenance);

public sealed record ProcessRunRecordSummary(
    ProcessRunRecordIdentity Identity,
    ProcessRunDisposition Disposition,
    ProcessRunRecordLifecycleState LifecycleState,
    ProcessRunRecordCompleteness Completeness,
    ProcessRunEvidenceSource AvailableEvidenceSources,
    ProcessRunEvidenceSource MissingEvidenceSources,
    IReadOnlyList<ProcessRunRecordWarningCode> CompletenessWarnings,
    ProcessRunFactsStatus FactsStatus,
    int FactsAttemptCount,
    DateTimeOffset? FactsNextAttemptAtUtc,
    string? FactsLastErrorClass,
    string? FactsLastErrorDiagnosticReference,
    ProcessRunNarrativeStatus NarrativeStatus,
    int NarrativeAttemptCount,
    DateTimeOffset? NarrativeNextAttemptAtUtc,
    string? NarrativeLastErrorClass,
    string? NarrativeLastErrorDiagnosticReference,
    ProcessRunRecordMetrics Metrics,
    IReadOnlyList<ProcessRunParticipantId> ParticipantIds,
    ProcessRunNarrative? Narrative,
    long SourceGlobalSequence,
    long SourceRootSequence,
    string SchemaVersion,
    DateTimeOffset UpdatedAtUtc);

public sealed record ProcessRunRecord(
    ProcessRunRecordSummary Summary,
    ProcessRunHardFacts? Facts);

public sealed record ProcessRunRecordSeed(
    ProcessRunRecordIdentity Identity,
    ProcessRunDisposition Disposition,
    DateTimeOffset EndedAtUtc,
    long SourceGlobalSequence,
    long SourceRootSequence,
    DateTimeOffset ObservedAtUtc,
    string SchemaVersion = ProcessRunRecordSchema.CurrentVersion)
{
    public ProcessRunRecordSeedValidation Validation { get; init; }
}

public sealed record ProcessRunRecordSupersession(
    ProcessRunId RunId,
    long SourceGlobalSequence,
    long SourceRootSequence,
    DateTimeOffset SupersededAtUtc);

public sealed record ProcessRunRecordCursor(
    DateTimeOffset EndedAtUtc,
    ProcessRunId RunId);

public sealed record ProcessRunRecordListQuery(int Take = 50)
{
    public ProcessRunRecordListPayload Payload { get; init; } = ProcessRunRecordListPayload.Compact;

    public IReadOnlyList<ProcessRunId> RunIds { get; init; } = [];

    public Guid? ProjectId { get; init; }

    public IReadOnlyList<Guid> ProjectIds { get; init; } = [];

    public ProcessDefinitionId? DefinitionId { get; init; }

    public ProcessRunId? RootRunId { get; init; }

    public ProcessRunDisposition? Disposition { get; init; }

    public ProcessRunParticipantId? ParticipantId { get; init; }

    public DateTimeOffset? EndedFromUtc { get; init; }

    public DateTimeOffset? EndedBeforeUtc { get; init; }

    public ProcessRunRecordCursor? Cursor { get; init; }

    public bool RootRunsOnly { get; init; }

    public bool IncludeSuperseded { get; init; }
}

public sealed record ProcessRunRecordPage(
    IReadOnlyList<ProcessRunRecordSummary> Records,
    ProcessRunRecordCursor? NextCursor);

public sealed record ProcessRunRecordAnalyticsQuery(
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc)
{
    public Guid? ProjectId { get; init; }

    public IReadOnlyList<Guid> ProjectIds { get; init; } = [];

    public ProcessDefinitionId? DefinitionId { get; init; }

    public ProcessRunId? RootRunId { get; init; }

    public ProcessRunDisposition? Disposition { get; init; }

    public ProcessRunParticipantId? ParticipantId { get; init; }

    public bool RootRunsOnly { get; init; }

    public bool AllTime { get; init; }

    public bool IncludeTotals { get; init; } = true;

    public bool IncludeDailyCostTrend { get; init; } = true;
}

public sealed record ProcessRunDispositionAnalytics(
    ProcessRunDisposition Disposition,
    int MatchingRunCount);

public sealed record ProcessRunDailyCostTrendPoint(
    DateOnly DayUtc,
    decimal EstimatedCost,
    decimal ActualCost);

public sealed record ProcessRunRecordAnalytics(
    int MatchingRunCount,
    int FactsAvailableRunCount,
    int EvidenceCompleteRunCount,
    int EvidencePartialRunCount,
    int FactsUnavailableRunCount,
    DateTimeOffset? LatestEndedAtUtc,
    long? MaximumSourceGlobalSequence,
    long DurationMilliseconds,
    long InputTokenCount,
    long CachedInputTokenCount,
    long OutputTokenCount,
    long ReasoningTokenCount,
    long TotalTokenCount,
    decimal EstimatedCost,
    decimal ActualCost,
    int RepetitionCount,
    int ExecutionCount,
    int ReworkCount,
    int IncidentCount,
    int EscalationCount,
    int ToolCallCount,
    int ArtifactCount,
    IReadOnlyList<ProcessRunDispositionAnalytics> Dispositions)
{
    public int UnknownCostRunCount { get; init; }

    public IReadOnlyList<ProcessRunDailyCostTrendPoint> DailyCostTrend { get; init; } = [];
}

public sealed record ProcessRunRecordClaimRequest(
    DateTimeOffset NowUtc,
    TimeSpan LeaseDuration,
    int Take);

public sealed record ProcessRunFactsClaim(
    ProcessRunId RunId,
    long SourceGlobalSequence,
    ProcessRunRecordClaimToken ClaimToken,
    DateTimeOffset LeaseExpiresAtUtc,
    int AttemptCount);

public sealed record ProcessRunFactsCompletion(
    ProcessRunRecordIdentity Identity,
    long SourceGlobalSequence,
    ProcessRunRecordClaimToken ClaimToken,
    ProcessRunRecordCompleteness Completeness,
    ProcessRunEvidenceSource AvailableEvidenceSources,
    ProcessRunEvidenceSource MissingEvidenceSources,
    IReadOnlyList<ProcessRunRecordWarningCode> CompletenessWarnings,
    ProcessRunRecordMetrics Metrics,
    ProcessRunHardFacts Facts,
    DateTimeOffset CompletedAtUtc);

public sealed record ProcessRunNarrativeClaim(
    ProcessRunId RunId,
    long SourceGlobalSequence,
    ProcessRunRecordClaimToken ClaimToken,
    DateTimeOffset LeaseExpiresAtUtc,
    int AttemptCount);

public sealed record ProcessRunNarrativeCompletion(
    ProcessRunId RunId,
    long SourceGlobalSequence,
    ProcessRunRecordClaimToken ClaimToken,
    ProcessRunNarrative Narrative,
    DateTimeOffset CompletedAtUtc);

public sealed record ProcessRunStageFailure(
    ProcessRunId RunId,
    long SourceGlobalSequence,
    ProcessRunRecordClaimToken ClaimToken,
    string ErrorClass,
    string DiagnosticReference,
    DateTimeOffset FailedAtUtc,
    DateTimeOffset? NextAttemptAtUtc,
    bool ConsumesAttempt = true);

public interface IProcessRunRecordStore
{
    Task<bool> UpsertSeedAsync(
        ProcessRunRecordSeed seed,
        CancellationToken cancellationToken = default);

    Task<bool> SupersedeAsync(
        ProcessRunRecordSupersession supersession,
        CancellationToken cancellationToken = default);

    Task<ProcessRunRecord?> GetAsync(
        ProcessRunId runId,
        bool includeSuperseded = false,
        CancellationToken cancellationToken = default);

    Task<ProcessRunRecordPage> ListAsync(
        ProcessRunRecordListQuery query,
        CancellationToken cancellationToken = default);

    Task<ProcessRunRecordAnalytics> ReadAnalyticsAsync(
        ProcessRunRecordAnalyticsQuery query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProcessRunFactsClaim>> ClaimFactsAsync(
        ProcessRunRecordClaimRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> CompleteFactsAsync(
        ProcessRunFactsCompletion completion,
        CancellationToken cancellationToken = default);

    Task<bool> FailFactsAsync(
        ProcessRunStageFailure failure,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProcessRunNarrativeClaim>> ClaimNarrativesAsync(
        ProcessRunRecordClaimRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> CompleteNarrativeAsync(
        ProcessRunNarrativeCompletion completion,
        CancellationToken cancellationToken = default);

    Task<bool> FailNarrativeAsync(
        ProcessRunStageFailure failure,
        CancellationToken cancellationToken = default);
}

public interface IProcessRunRecordBackfillSource
{
    Task<IReadOnlyList<ProcessRunRecordSeed>> ListMissingReportableSeedsAsync(
        int take,
        CancellationToken cancellationToken = default);
}
