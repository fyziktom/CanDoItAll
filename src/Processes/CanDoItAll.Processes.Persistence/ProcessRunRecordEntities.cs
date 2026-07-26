using CanDoItAll.Processes.Projections;

namespace CanDoItAll.Processes.Persistence;

public sealed class ProcessRunRecordEntity
{
    public Guid RunId { get; set; }

    public Guid RootRunId { get; set; }

    public Guid? ParentRunId { get; set; }

    public Guid? PlanId { get; set; }

    public Guid? DefinitionId { get; set; }

    public Guid? DefinitionVersionId { get; set; }

    public Guid? ProjectId { get; set; }

    public ProcessRunDisposition Disposition { get; set; }

    public ProcessRunRecordLifecycleState LifecycleState { get; set; }

    public ProcessRunRecordCompleteness Completeness { get; set; }

    public DateTimeOffset? StartedAtUtc { get; set; }

    public DateTimeOffset EndedAtUtc { get; set; }

    public long? DurationMilliseconds { get; set; }

    public int TotalStepCount { get; set; }

    public int ExecutableStepCount { get; set; }

    public int CompletedStepCount { get; set; }

    public int FailedStepCount { get; set; }

    public int CancelledStepCount { get; set; }

    public int RepetitionCount { get; set; }

    public int ExecutionCount { get; set; }

    public int ReworkCount { get; set; }

    public int IncidentCount { get; set; }

    public int EscalationCount { get; set; }

    public long InputTokenCount { get; set; }

    public long CachedInputTokenCount { get; set; }

    public long OutputTokenCount { get; set; }

    public long ReasoningTokenCount { get; set; }

    public long TotalTokenCount { get; set; }

    public decimal EstimatedCost { get; set; }

    public decimal ActualCost { get; set; }

    public int ToolCallCount { get; set; }

    public int ArtifactCount { get; set; }

    public int SubprocessCount { get; set; }

    public string? FactsJson { get; set; }

    public string ParticipantIdsJson { get; set; } = "[]";

    public ProcessRunEvidenceSource AvailableEvidenceSources { get; set; }

    public ProcessRunEvidenceSource MissingEvidenceSources { get; set; }

    public string CompletenessWarningsJson { get; set; } = "[]";

    public ProcessRunFactsStatus FactsStatus { get; set; }

    public Guid? FactsLeaseToken { get; set; }

    public DateTimeOffset? FactsLeaseExpiresAtUtc { get; set; }

    public int FactsAttemptCount { get; set; }

    public DateTimeOffset? FactsNextAttemptAtUtc { get; set; }

    public string? FactsLastErrorClass { get; set; }

    public string? FactsLastErrorDiagnosticReference { get; set; }

    public string? NarrativeJson { get; set; }

    public ProcessRunNarrativeStatus NarrativeStatus { get; set; }

    public Guid? NarrativeLeaseToken { get; set; }

    public DateTimeOffset? NarrativeLeaseExpiresAtUtc { get; set; }

    public int NarrativeAttemptCount { get; set; }

    public DateTimeOffset? NarrativeNextAttemptAtUtc { get; set; }

    public string? NarrativeLastErrorClass { get; set; }

    public string? NarrativeLastErrorDiagnosticReference { get; set; }

    public long SourceGlobalSequence { get; set; }

    public long SourceRootSequence { get; set; }

    public string SchemaVersion { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class ProcessRunRecordParticipantEntity
{
    public string ParticipantId { get; set; } = string.Empty;

    public Guid RunId { get; set; }
}
