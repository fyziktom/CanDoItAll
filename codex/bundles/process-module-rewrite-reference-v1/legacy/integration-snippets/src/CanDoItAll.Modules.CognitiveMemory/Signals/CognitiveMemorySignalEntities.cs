using CanDoItAll.Infrastructure.Persistence;

namespace CanDoItAll.Modules.CognitiveMemory;

public sealed class CognitiveMemoryPredictionExpectationRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProjectId { get; set; }

    public CognitiveMemoryPredictionExpectationKind ExpectationKind { get; set; } = CognitiveMemoryPredictionExpectationKind.ClaimRecall;

    public CognitiveMemoryActorKind ActorKind { get; set; } = CognitiveMemoryActorKind.System;

    public string ActorId { get; set; } = string.Empty;

    public string PolicyProfileId { get; set; } = string.Empty;

    public Guid? WorkspaceFrameId { get; set; }

    public Guid? AttentionDecisionId { get; set; }

    public Guid? MemoryRecordId { get; set; }

    public Guid? ClaimId { get; set; }

    public Guid? SourceItemId { get; set; }

    public Guid? ProcedureSkillId { get; set; }

    public Guid? WorkflowRunId { get; set; }

    public Guid? ProcessRunId { get; set; }

    public Guid? ProbeSessionId { get; set; }

    public string ExpectedContextKey { get; set; } = string.Empty;

    public CognitiveMemoryWorkspaceSourceSufficiency ExpectedSourceSufficiency { get; set; } = CognitiveMemoryWorkspaceSourceSufficiency.Unknown;

    public double? MinimumExpectedConfidence { get; set; }

    public double? MaximumExpectedConfidence { get; set; }

    public string Summary { get; set; } = string.Empty;

    public string ExpectedOutcome { get; set; } = string.Empty;

    public string AlgorithmVersion { get; set; } = string.Empty;

    public string MetadataJson { get; set; } = "{}";

    public DateTimeOffset CreatedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

public sealed class CognitiveMemoryPredictionExpectationEvidenceAnchorRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PredictionExpectationId { get; set; }

    public Guid ProjectId { get; set; }

    public Guid EvidenceAnchorId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class CognitiveMemoryPredictionErrorRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProjectId { get; set; }

    public Guid? PredictionExpectationId { get; set; }

    public CognitiveMemoryPredictionErrorKind ErrorKind { get; set; } = CognitiveMemoryPredictionErrorKind.Unknown;

    public CognitiveMemoryActorKind ActorKind { get; set; } = CognitiveMemoryActorKind.System;

    public string ActorId { get; set; } = string.Empty;

    public string PolicyProfileId { get; set; } = string.Empty;

    public Guid? WorkspaceFrameId { get; set; }

    public Guid? AttentionDecisionId { get; set; }

    public Guid? MemoryRecordId { get; set; }

    public Guid? ClaimId { get; set; }

    public Guid? SourceItemId { get; set; }

    public Guid? ProcedureSkillId { get; set; }

    public Guid? WorkflowRunId { get; set; }

    public Guid? ProcessRunId { get; set; }

    public Guid? ProbeTurnId { get; set; }

    public Guid SeverityScoreEvaluationTraceId { get; set; }

    public CognitiveMemoryScoreProjectionBucket SeverityBucket { get; set; } = CognitiveMemoryScoreProjectionBucket.Unknown;

    public double? DisplaySeverityProjection { get; set; }

    public int SeverityComponentCount { get; set; }

    public int MatchedShapeCount { get; set; }

    public int MissingRequiredDimensionCount { get; set; }

    public string ObservationSummary { get; set; } = string.Empty;

    public string ExpectedSummary { get; set; } = string.Empty;

    public string ObservedSummary { get; set; } = string.Empty;

    public string CauseHypothesis { get; set; } = string.Empty;

    public CognitiveMemoryPredictionSuggestedActionKind SuggestedActionKind { get; set; } = CognitiveMemoryPredictionSuggestedActionKind.Unknown;

    public string SuggestedAction { get; set; } = string.Empty;

    public bool RequiresReview { get; set; }

    public int CreatedSignalCount { get; set; }

    public string AlgorithmVersion { get; set; } = string.Empty;

    public string MetadataJson { get; set; } = "{}";

    public DateTimeOffset ObservedAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

public sealed class CognitiveMemoryPredictionErrorEvidenceAnchorRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PredictionErrorId { get; set; }

    public Guid ProjectId { get; set; }

    public Guid EvidenceAnchorId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class CognitiveMemoryPredictionErrorSignalRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PredictionErrorId { get; set; }

    public Guid CognitiveSignalId { get; set; }

    public Guid ProjectId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class CognitiveMemorySignalRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProjectId { get; set; }

    public CognitiveMemorySignalKind SignalKind { get; set; } = CognitiveMemorySignalKind.Unknown;

    public CognitiveMemorySignalSourceKind SourceKind { get; set; } = CognitiveMemorySignalSourceKind.Unknown;

    public CognitiveMemoryActorKind ActorKind { get; set; } = CognitiveMemoryActorKind.System;

    public string ActorId { get; set; } = string.Empty;

    public string PolicyProfileId { get; set; } = string.Empty;

    public CognitiveMemoryAccessLevel AccessLevel { get; set; } = CognitiveMemoryAccessLevel.Project;

    public CognitiveMemoryRedactionState RedactionState { get; set; } = CognitiveMemoryRedactionState.Safe;

    public CognitiveMemoryRiskLevel RiskLevel { get; set; } = CognitiveMemoryRiskLevel.Low;

    public bool RequiresReview { get; set; }

    public Guid? WorkspaceFrameId { get; set; }

    public Guid? AttentionDecisionId { get; set; }

    public Guid? PredictionErrorId { get; set; }

    public Guid? MemoryRecordId { get; set; }

    public Guid? ClaimId { get; set; }

    public Guid? SourceItemId { get; set; }

    public Guid? ProcedureSkillId { get; set; }

    public Guid? WorkflowRunId { get; set; }

    public Guid? ProcessRunId { get; set; }

    public Guid? ProbeTurnId { get; set; }

    public Guid? ReviewItemId { get; set; }

    public Guid SignalScoreEvaluationTraceId { get; set; }

    public string ScoreSchemaVersion { get; set; } = string.Empty;

    public string NormalizationProfileId { get; set; } = string.Empty;

    public string AlgorithmVersion { get; set; } = string.Empty;

    public int ComponentCount { get; set; }

    public int MatchedShapeCount { get; set; }

    public int MissingRequiredDimensionCount { get; set; }

    public double? DisplayMagnitudeProjection { get; set; }

    public string Summary { get; set; } = string.Empty;

    public string MetadataJson { get; set; } = "{}";

    public DateTimeOffset ObservedAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

public sealed class CognitiveMemorySignalEvidenceAnchorRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CognitiveSignalId { get; set; }

    public Guid ProjectId { get; set; }

    public Guid EvidenceAnchorId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class CognitiveMemorySignalConsumerPolicyRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CognitiveSignalId { get; set; }

    public Guid ProjectId { get; set; }

    public CognitiveMemorySignalConsumerKind ConsumerKind { get; set; } = CognitiveMemorySignalConsumerKind.Unknown;

    public CognitiveMemoryAccessLevel MaximumAccessLevel { get; set; } = CognitiveMemoryAccessLevel.Project;

    public bool RequiresReviewBeforeAction { get; set; }

    public bool CanCreateTruthDirectly { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
