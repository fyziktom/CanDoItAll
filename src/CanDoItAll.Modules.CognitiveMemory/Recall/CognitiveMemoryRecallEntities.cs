namespace CanDoItAll.Modules.CognitiveMemory;

public sealed class CognitiveMemoryRecallTraceStageRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RecallTraceId { get; set; }

    public Guid? ProjectId { get; set; }

    public CognitiveMemoryRecallTraceStageKind StageKind { get; set; } = CognitiveMemoryRecallTraceStageKind.Unknown;

    public CognitiveMemoryRecallChannelKind ChannelKind { get; set; } = CognitiveMemoryRecallChannelKind.Unknown;

    public CognitiveMemoryRecallStageStatus Status { get; set; } = CognitiveMemoryRecallStageStatus.NotStarted;

    public int CandidateCount { get; set; }

    public int SelectedCount { get; set; }

    public int ExcludedCount { get; set; }

    public CognitiveMemoryBudgetLimit? LimitingBudget { get; set; }

    public string ProviderTrace { get; set; } = string.Empty;

    public string FailureCode { get; set; } = string.Empty;

    public string FailureMessage { get; set; } = string.Empty;

    public DateTimeOffset StartedAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }
}

public sealed class CognitiveMemoryRecallCandidateRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RecallTraceId { get; set; }

    public Guid? ProjectId { get; set; }

    public CognitiveMemoryRecallChannelKind PrimaryChannelKind { get; set; } = CognitiveMemoryRecallChannelKind.Unknown;

    public CognitiveMemoryRecallCandidateDecisionKind DecisionKind { get; set; } = CognitiveMemoryRecallCandidateDecisionKind.Unknown;

    public CognitiveMemoryRecallExclusionReasonKind ExclusionReasonKind { get; set; } = CognitiveMemoryRecallExclusionReasonKind.None;

    public Guid MemoryRecordId { get; set; }

    public CognitiveMemoryRecordKind MemoryKind { get; set; } = CognitiveMemoryRecordKind.Semantic;

    public Guid? ClaimId { get; set; }

    public Guid? SourceItemId { get; set; }

    public Guid? EvidenceAnchorId { get; set; }

    public Guid? WorkspaceFrameId { get; set; }

    public Guid? ContextFrameId { get; set; }

    public Guid ScoreEvaluationTraceId { get; set; }

    public CognitiveMemoryScoreProjectionBucket ScoreBucket { get; set; } = CognitiveMemoryScoreProjectionBucket.Unknown;

    public double? DisplayRankProjection { get; set; }

    public bool HasSourceDetail { get; set; }

    public bool SourceRedacted { get; set; }

    public int EstimatedTokenCount { get; set; }

    public int SourceRefCount { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public string ChannelTraceJson { get; set; } = "{}";

    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class CognitiveMemoryRecallContextPackRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RecallTraceId { get; set; }

    public Guid ProjectId { get; set; }

    public Guid? WorkspaceFrameId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public int CharacterBudget { get; set; }

    public int RenderedCharacterCount { get; set; }

    public int SectionCount { get; set; }

    public int SourceRefCount { get; set; }

    public int WarningCount { get; set; }

    public string MetadataJson { get; set; } = "{}";

    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class CognitiveMemoryRecallContextSectionRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ContextPackId { get; set; }

    public Guid RecallTraceId { get; set; }

    public Guid ProjectId { get; set; }

    public CognitiveMemoryRecallContextSectionKind SectionKind { get; set; } = CognitiveMemoryRecallContextSectionKind.Unknown;

    public int Sequence { get; set; }

    public string SectionKey { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public Guid? MemoryRecordId { get; set; }

    public Guid? ClaimId { get; set; }

    public Guid? SourceItemId { get; set; }

    public CognitiveMemoryAccessLevel AccessLevel { get; set; } = CognitiveMemoryAccessLevel.Project;

    public CognitiveMemoryRedactionState RedactionState { get; set; } = CognitiveMemoryRedactionState.Safe;

    public int EstimatedTokenCount { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class CognitiveMemoryRecallSourceRefRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RecallTraceId { get; set; }

    public Guid? ContextPackId { get; set; }

    public Guid? ProjectId { get; set; }

    public Guid MemoryRecordId { get; set; }

    public Guid? ClaimId { get; set; }

    public Guid? SourceItemId { get; set; }

    public Guid? EvidenceAnchorId { get; set; }

    public string SourceSystem { get; set; } = string.Empty;

    public string Locator { get; set; } = string.Empty;

    public string QuoteHash { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public CognitiveMemoryAccessLevel AccessLevel { get; set; } = CognitiveMemoryAccessLevel.Project;

    public CognitiveMemoryRedactionState RedactionState { get; set; } = CognitiveMemoryRedactionState.Safe;

    public bool IncludedInContext { get; set; }

    public CognitiveMemoryRecallExclusionReasonKind ExclusionReasonKind { get; set; } = CognitiveMemoryRecallExclusionReasonKind.None;

    public DateTimeOffset CreatedAtUtc { get; set; }
}
