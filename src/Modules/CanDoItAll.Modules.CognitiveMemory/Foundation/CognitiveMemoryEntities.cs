using CanDoItAll.Infrastructure.Persistence;

namespace CanDoItAll.Modules.CognitiveMemory;

public sealed class CognitiveMemorySourceManifestRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? ProjectId { get; set; }

    public string SourceSystem { get; set; } = string.Empty;

    public string SourceScopeKey { get; set; } = string.Empty;

    public string SourceSnapshotId { get; set; } = string.Empty;

    public CognitiveMemoryHashAlgorithm SnapshotHashAlgorithm { get; set; } = CognitiveMemoryHashAlgorithm.Sha256;

    public string SnapshotHash { get; set; } = string.Empty;

    public string ProviderVersion { get; set; } = string.Empty;

    public string? Cursor { get; set; }

    public CognitiveMemoryRunStatus ScanStatus { get; set; } = CognitiveMemoryRunStatus.Pending;

    public DateTimeOffset ObservedAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

public sealed class CognitiveMemorySourceItemRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SourceManifestId { get; set; }

    public Guid? ProjectId { get; set; }

    public string SourceSystem { get; set; } = string.Empty;

    public string SourceItemKey { get; set; } = string.Empty;

    public string SourceItemType { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string ContentText { get; set; } = string.Empty;

    public string? Locator { get; set; }

    public CognitiveMemoryHashAlgorithm ContentHashAlgorithm { get; set; } = CognitiveMemoryHashAlgorithm.Sha256;

    public string ContentHash { get; set; } = string.Empty;

    public CognitiveMemoryRedactionState RedactionState { get; set; } = CognitiveMemoryRedactionState.Unclassified;

    public CognitiveMemoryAccessLevel AccessLevel { get; set; } = CognitiveMemoryAccessLevel.Project;

    public string AccessScope { get; set; } = string.Empty;

    public string ProvenanceJson { get; set; } = "{}";

    public DateTimeOffset ObservedAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

public sealed class CognitiveMemoryRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? ProjectId { get; set; }

    public CognitiveMemoryRecordKind Kind { get; set; } = CognitiveMemoryRecordKind.Semantic;

    public CognitiveMemoryRecordOrigin Origin { get; set; } = CognitiveMemoryRecordOrigin.SourceDerived;

    public string Title { get; set; } = string.Empty;

    public string CanonicalText { get; set; } = string.Empty;

    public string SummaryText { get; set; } = string.Empty;

    public string TopicKey { get; set; } = string.Empty;

    public CognitiveMemoryValidationState ValidationState { get; set; } = CognitiveMemoryValidationState.Draft;

    public CognitiveMemoryStabilityState StabilityState { get; set; } = CognitiveMemoryStabilityState.Unknown;

    public CognitiveMemoryOperationMode CreatedInMode { get; set; } = CognitiveMemoryOperationMode.Observe;

    public string AlgorithmVersion { get; set; } = string.Empty;

    public CognitiveMemoryHashAlgorithm ContentHashAlgorithm { get; set; } = CognitiveMemoryHashAlgorithm.Sha256;

    public string ContentHash { get; set; } = string.Empty;

    public int SourceEvidenceCount { get; set; }

    public int EvidenceAnchorCount { get; set; }

    public string GeneratedReason { get; set; } = string.Empty;

    public Guid? PrimaryClaimId { get; set; }

    public Guid? PrimaryContextFrameId { get; set; }

    public Guid? ConfidenceScoreEvaluationTraceId { get; set; }

    public Guid? ActivationScoreEvaluationTraceId { get; set; }

    public CognitiveMemoryScoreProjectionBucket ConfidenceBucket { get; set; } = CognitiveMemoryScoreProjectionBucket.Unknown;

    public CognitiveMemoryScoreProjectionBucket ActivationBucket { get; set; } = CognitiveMemoryScoreProjectionBucket.Unknown;

    public CognitiveMemoryAccessLevel AccessLevel { get; set; } = CognitiveMemoryAccessLevel.Project;

    public CognitiveMemoryRiskLevel RiskLevel { get; set; } = CognitiveMemoryRiskLevel.Low;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

public sealed class CognitiveMemorySourceLinkRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid MemoryRecordId { get; set; }

    public Guid SourceManifestId { get; set; }

    public Guid SourceItemId { get; set; }

    public CognitiveMemoryEvidenceRole EvidenceRole { get; set; } = CognitiveMemoryEvidenceRole.PrimarySource;

    public string? Locator { get; set; }

    public string? QuoteHash { get; set; }

    public string Summary { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class CognitiveMemoryRelationRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? ProjectId { get; set; }

    public Guid SourceMemoryRecordId { get; set; }

    public Guid TargetMemoryRecordId { get; set; }

    public CognitiveMemoryRelationKind RelationKind { get; set; } = CognitiveMemoryRelationKind.SimilarTo;

    public int EvidenceCount { get; set; }

    public Guid? RelationScoreEvaluationTraceId { get; set; }

    public CognitiveMemoryScoreProjectionBucket RelationBucket { get; set; } = CognitiveMemoryScoreProjectionBucket.Unknown;

    public double? DisplayStrengthProjection { get; set; }

    public string Reason { get; set; } = string.Empty;

    public string AlgorithmVersion { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

public sealed class CognitiveMemoryProjectionStateRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? ProjectId { get; set; }

    public CognitiveMemoryProjectionKind ProjectionKind { get; set; } = CognitiveMemoryProjectionKind.RelationalSearch;

    public string TargetProvider { get; set; } = string.Empty;

    public string ProjectionSchemaVersion { get; set; } = string.Empty;

    public string AlgorithmVersion { get; set; } = string.Empty;

    public CognitiveMemoryProjectionStatus Status { get; set; } = CognitiveMemoryProjectionStatus.Pending;

    public string LastSourceHash { get; set; } = string.Empty;

    public DateTimeOffset? LastProjectedAtUtc { get; set; }

    public string FailureCode { get; set; } = string.Empty;

    public string FailureMessage { get; set; } = string.Empty;

    public bool RebuildRequired { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

public sealed class CognitiveMemoryRecallTraceRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? ProjectId { get; set; }

    public CognitiveMemoryOperationMode OperationMode { get; set; } = CognitiveMemoryOperationMode.Recall;

    public CognitiveMemoryRecallMode RecallMode { get; set; } = CognitiveMemoryRecallMode.FocusedTaskContext;

    public string RequestedByActorId { get; set; } = string.Empty;

    public string PolicyProfileId { get; set; } = string.Empty;

    public Guid? WorkspaceFrameId { get; set; }

    public Guid? AttentionDecisionId { get; set; }

    public Guid? SelfRegulationAssessmentId { get; set; }

    public Guid? AnswerPostureDecisionId { get; set; }

    public Guid? AnswerGateDecisionId { get; set; }

    public Guid? ContextPackId { get; set; }

    public CognitiveMemoryHashAlgorithm RequestHashAlgorithm { get; set; } = CognitiveMemoryHashAlgorithm.Sha256;

    public string RequestHash { get; set; } = string.Empty;

    public string AlgorithmVersion { get; set; } = string.Empty;

    public CognitiveMemoryRunStatus Outcome { get; set; } = CognitiveMemoryRunStatus.Pending;

    public int IncludedRecordCount { get; set; }

    public int ExcludedRecordCount { get; set; }

    public int SelectedClaimCount { get; set; }

    public int SelectedEvidenceAnchorCount { get; set; }

    public int InhibitedCandidateCount { get; set; }

    public CognitiveMemoryBudgetLimit? LimitingBudget { get; set; }

    public string TraceJson { get; set; } = "{}";

    public DateTimeOffset StartedAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

public sealed class CognitiveMemoryReviewItemRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? ProjectId { get; set; }

    public CognitiveMemoryReviewKind ReviewKind { get; set; } = CognitiveMemoryReviewKind.GeneratedMemory;

    public CognitiveMemoryReviewStatus Status { get; set; } = CognitiveMemoryReviewStatus.Pending;

    public CognitiveMemoryReviewSubjectKind SubjectKind { get; set; } = CognitiveMemoryReviewSubjectKind.MemoryRecord;

    public Guid SubjectId { get; set; }

    public CognitiveMemoryRiskLevel RiskLevel { get; set; } = CognitiveMemoryRiskLevel.Medium;

    public string ReasonCode { get; set; } = string.Empty;

    public string ReasonText { get; set; } = string.Empty;

    public int SourceEvidenceCount { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? DecidedAtUtc { get; set; }

    public string DecidedByActorId { get; set; } = string.Empty;

    public string DecisionNotes { get; set; } = string.Empty;

    public Guid ConcurrencyToken { get; set; }
}

public sealed class CognitiveMemoryRunRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? ProjectId { get; set; }

    public CognitiveMemoryRunKind RunKind { get; set; } = CognitiveMemoryRunKind.SourceScan;

    public CognitiveMemoryRunStatus Status { get; set; } = CognitiveMemoryRunStatus.Pending;

    public CognitiveMemoryOperationMode OperationMode { get; set; } = CognitiveMemoryOperationMode.Observe;

    public string IdempotencyKey { get; set; } = string.Empty;

    public CognitiveMemoryHashAlgorithm InputHashAlgorithm { get; set; } = CognitiveMemoryHashAlgorithm.Sha256;

    public string InputHash { get; set; } = string.Empty;

    public string AlgorithmVersion { get; set; } = string.Empty;

    public string Cursor { get; set; } = string.Empty;

    public DateTimeOffset StartedAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public string FailureCode { get; set; } = string.Empty;

    public string FailureMessage { get; set; } = string.Empty;

    public Guid ConcurrencyToken { get; set; }
}
