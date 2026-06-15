using CanDoItAll.Infrastructure.Persistence;

namespace CanDoItAll.Modules.CognitiveMemory;

public sealed class CognitiveMemoryWorkspaceFrameRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProjectId { get; set; }

    public CognitiveMemoryWorkspaceFrameKind FrameKind { get; set; } = CognitiveMemoryWorkspaceFrameKind.UserConversation;

    public CognitiveMemoryWorkspaceFrameStatus Status { get; set; } = CognitiveMemoryWorkspaceFrameStatus.Active;

    public string OwnerUserId { get; set; } = string.Empty;

    public string OwnerAgentId { get; set; } = string.Empty;

    public Guid? ProcessRunId { get; set; }

    public Guid? WorkflowRunId { get; set; }

    public Guid? ProcessStepId { get; set; }

    public Guid? ProbeSessionId { get; set; }

    public Guid? ReviewSessionId { get; set; }

    public Guid? LearningTaskId { get; set; }

    public int ContextBudgetTokenLimit { get; set; }

    public int ContextBudgetSectionLimit { get; set; }

    public int ContextBudgetDetailLimit { get; set; }

    public int CurrentTokenEstimate { get; set; }

    public int CurrentSectionEstimate { get; set; }

    public int CurrentDetailEstimate { get; set; }

    public bool BudgetExhausted { get; set; }

    public CognitiveMemoryBudgetLimit? LimitingBudget { get; set; }

    public Guid? CognitiveLoadScoreEvaluationTraceId { get; set; }

    public CognitiveMemoryScoreProjectionBucket CognitiveLoadBucket { get; set; } = CognitiveMemoryScoreProjectionBucket.Unknown;

    public double? DisplayCognitiveLoadScore { get; set; }

    public Guid? LastAttentionDecisionId { get; set; }

    public Guid? LastSelfRegulationAssessmentId { get; set; }

    public Guid? LastAnswerPostureDecisionId { get; set; }

    public string MetadataJson { get; set; } = "{}";

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public DateTimeOffset? ExpiresAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

public sealed class CognitiveMemoryWorkspaceGoalRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid WorkspaceFrameId { get; set; }

    public Guid ProjectId { get; set; }

    public int Sequence { get; set; }

    public Guid? ParentGoalId { get; set; }

    public string GoalKey { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class CognitiveMemoryWorkingMemorySlotRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid WorkspaceFrameId { get; set; }

    public Guid ProjectId { get; set; }

    public CognitiveMemoryWorkingMemorySlotKind SlotKind { get; set; } = CognitiveMemoryWorkingMemorySlotKind.MemoryRecord;

    public Guid? MemoryRecordId { get; set; }

    public Guid? ClaimId { get; set; }

    public Guid? SourceItemId { get; set; }

    public Guid? ProcedureSkillId { get; set; }

    public Guid? RecallTraceId { get; set; }

    public Guid? ProbeTurnId { get; set; }

    public Guid? WorkflowArtifactId { get; set; }

    public Guid? OpenQuestionId { get; set; }

    public string ExternalPlaceholderKey { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public Guid? AttentionScoreEvaluationTraceId { get; set; }

    public CognitiveMemoryScoreProjectionBucket AttentionBucket { get; set; } = CognitiveMemoryScoreProjectionBucket.Unknown;

    public double? DisplayAttentionScore { get; set; }

    public CognitiveMemoryWorkspaceSourceSufficiency SourceSufficiency { get; set; } = CognitiveMemoryWorkspaceSourceSufficiency.Unknown;

    public CognitiveMemoryRiskLevel RiskLevel { get; set; } = CognitiveMemoryRiskLevel.Low;

    public CognitiveMemoryScoreProjectionBucket ConfidenceBucket { get; set; } = CognitiveMemoryScoreProjectionBucket.Unknown;

    public CognitiveMemoryScoreProjectionBucket StalenessBucket { get; set; } = CognitiveMemoryScoreProjectionBucket.Unknown;

    public CognitiveMemoryFocusInclusionReasonKind InclusionReasonKind { get; set; } = CognitiveMemoryFocusInclusionReasonKind.GoalMatch;

    public string InclusionReason { get; set; } = string.Empty;

    public string RelationToActiveGoal { get; set; } = string.Empty;

    public string CompressionSummary { get; set; } = string.Empty;

    public int EstimatedTokenCount { get; set; }

    public int EstimatedSectionCount { get; set; }

    public int EstimatedDetailCount { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class CognitiveMemoryWorkspaceSlotEvidenceAnchorRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid WorkspaceSlotId { get; set; }

    public Guid WorkspaceFrameId { get; set; }

    public Guid ProjectId { get; set; }

    public Guid EvidenceAnchorId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class CognitiveMemoryWorkspaceOpenQuestionRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid WorkspaceFrameId { get; set; }

    public Guid ProjectId { get; set; }

    public string QuestionText { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public CognitiveMemoryWorkspaceOpenQuestionStatus Status { get; set; } = CognitiveMemoryWorkspaceOpenQuestionStatus.Open;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? ResolvedAtUtc { get; set; }
}

public sealed class CognitiveMemoryInhibitedCandidateRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid WorkspaceFrameId { get; set; }

    public Guid ProjectId { get; set; }

    public CognitiveMemoryWorkingMemorySlotKind CandidateKind { get; set; } = CognitiveMemoryWorkingMemorySlotKind.MemoryRecord;

    public Guid? MemoryRecordId { get; set; }

    public Guid? ClaimId { get; set; }

    public Guid? SourceItemId { get; set; }

    public string ExternalCandidateKey { get; set; } = string.Empty;

    public CognitiveMemoryInhibitionReasonKind ReasonKind { get; set; } = CognitiveMemoryInhibitionReasonKind.ContextBoundary;

    public string Reason { get; set; } = string.Empty;

    public Guid? InhibitionScoreEvaluationTraceId { get; set; }

    public CognitiveMemoryScoreProjectionBucket InhibitionBucket { get; set; } = CognitiveMemoryScoreProjectionBucket.Inhibit;

    public double? DisplayRelevanceScore { get; set; }

    public double? DisplayInhibitionStrength { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class CognitiveMemoryAttentionDecisionRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProjectId { get; set; }

    public Guid WorkspaceFrameId { get; set; }

    public Guid? SelfRegulationAssessmentId { get; set; }

    public Guid? AnswerPostureDecisionId { get; set; }

    public CognitiveMemoryAttentionDecisionKind DecisionKind { get; set; } = CognitiveMemoryAttentionDecisionKind.Unknown;

    public CognitiveMemoryAttentionReasonKind ReasonKind { get; set; } = CognitiveMemoryAttentionReasonKind.ScoreShapeMatched;

    public string RequestHash { get; set; } = string.Empty;

    public string RequestPreview { get; set; } = string.Empty;

    public Guid RoutingScoreEvaluationTraceId { get; set; }

    public CognitiveMemoryScoreProjectionBucket RoutingBucket { get; set; } = CognitiveMemoryScoreProjectionBucket.Unknown;

    public double? DisplayPriorityProjection { get; set; }

    public int MatchedShapeCount { get; set; }

    public int MissingRequiredDimensionCount { get; set; }

    public string Explanation { get; set; } = string.Empty;

    public string RequiredNextActionsJson { get; set; } = "[]";

    public string MetadataJson { get; set; } = "{}";

    public DateTimeOffset CreatedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}
