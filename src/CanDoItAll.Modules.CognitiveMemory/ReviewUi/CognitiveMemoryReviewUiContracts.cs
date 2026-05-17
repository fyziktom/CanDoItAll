namespace CanDoItAll.Modules.CognitiveMemory;

public enum CognitiveMemoryReviewDecisionKind
{
    Approve = 0,
    Reject = 1,
    RequestChanges = 2,
    Defer = 3
}

public sealed record CognitiveMemoryReviewUiQuery(
    Guid? ProjectId = null,
    int Take = 12);

public sealed record CognitiveMemoryReviewDecisionRequest(
    CognitiveMemoryReviewItemId ReviewItemId,
    CognitiveMemoryReviewDecisionKind DecisionKind,
    string ActorId,
    string Notes,
    Guid ExpectedConcurrencyToken);

public sealed record CognitiveMemoryReviewUiSnapshot(
    CognitiveMemoryReviewUiSummary Summary,
    IReadOnlyList<CognitiveMemoryExplorerItem> MemoryRecords,
    IReadOnlyList<CognitiveMemoryReviewQueueItem> ReviewItems,
    IReadOnlyList<CognitiveMemoryRecallTraceView> RecallTraces,
    IReadOnlyList<CognitiveMemoryConsolidationRunView> ConsolidationRuns,
    IReadOnlyList<CognitiveMemoryProjectionHealthView> ProjectionHealth,
    IReadOnlyList<CognitiveMemoryProcedureSkillView> ProcedureSkills,
    IReadOnlyList<CognitiveMemoryReplayJobView> ReplayJobs);

public sealed record CognitiveMemoryReviewUiSummary(
    int MemoryRecordCount,
    int PendingReviewCount,
    int HighRiskReviewCount,
    int RecallTraceCount,
    int ConsolidationIssueCount,
    int ProjectionIssueCount,
    int ProcedureReviewCount,
    int SimulationReviewCount);

public sealed record CognitiveMemoryExplorerItem(
    CognitiveMemoryRecordId Id,
    Guid? ProjectId,
    CognitiveMemoryRecordKind Kind,
    CognitiveMemoryRecordOrigin Origin,
    string Title,
    string SummaryText,
    string TopicKey,
    CognitiveMemoryValidationState ValidationState,
    CognitiveMemoryStabilityState StabilityState,
    int SourceEvidenceCount,
    int EvidenceAnchorCount,
    CognitiveMemoryScoreProjectionBucket ConfidenceBucket,
    CognitiveMemoryScoreProjectionBucket ActivationBucket,
    CognitiveMemoryAccessLevel AccessLevel,
    CognitiveMemoryRiskLevel RiskLevel,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<CognitiveMemorySourceLinkView> SourceLinks);

public sealed record CognitiveMemorySourceLinkView(
    Guid SourceItemId,
    CognitiveMemoryEvidenceRole EvidenceRole,
    string Locator,
    string Summary);

public sealed record CognitiveMemoryReviewQueueItem(
    CognitiveMemoryReviewItemId Id,
    Guid? ProjectId,
    CognitiveMemoryReviewKind ReviewKind,
    CognitiveMemoryReviewStatus Status,
    CognitiveMemoryReviewSubjectKind SubjectKind,
    Guid SubjectId,
    string SubjectTitle,
    CognitiveMemoryRiskLevel RiskLevel,
    string ReasonCode,
    string ReasonText,
    int SourceEvidenceCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? DecidedAtUtc,
    string DecidedByActorId,
    string DecisionNotes,
    Guid ConcurrencyToken);

public sealed record CognitiveMemoryRecallTraceView(
    Guid Id,
    Guid? ProjectId,
    CognitiveMemoryRecallMode RecallMode,
    CognitiveMemoryRunStatus Outcome,
    int IncludedRecordCount,
    int ExcludedRecordCount,
    int SelectedClaimCount,
    int SelectedEvidenceAnchorCount,
    int InhibitedCandidateCount,
    CognitiveMemoryBudgetLimit? LimitingBudget,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    IReadOnlyList<CognitiveMemoryRecallStageView> Stages,
    IReadOnlyList<CognitiveMemoryRecallCandidateView> Candidates,
    IReadOnlyList<CognitiveMemoryRecallSourceReferenceView> SourceReferences);

public sealed record CognitiveMemoryRecallStageView(
    CognitiveMemoryRecallTraceStageKind StageKind,
    CognitiveMemoryRecallChannelKind ChannelKind,
    CognitiveMemoryRecallStageStatus Status,
    int CandidateCount,
    int SelectedCount,
    int ExcludedCount,
    string FailureCode,
    string FailureMessage);

public sealed record CognitiveMemoryRecallCandidateView(
    CognitiveMemoryRecallChannelKind PrimaryChannelKind,
    CognitiveMemoryRecallCandidateDecisionKind DecisionKind,
    CognitiveMemoryRecallExclusionReasonKind ExclusionReasonKind,
    string Title,
    string Summary,
    string Reason,
    CognitiveMemoryScoreProjectionBucket ScoreBucket,
    double? DisplayRankProjection,
    bool SourceRedacted);

public sealed record CognitiveMemoryRecallSourceReferenceView(
    string SourceSystem,
    string Locator,
    string Summary,
    CognitiveMemoryAccessLevel AccessLevel,
    CognitiveMemoryRedactionState RedactionState,
    bool IncludedInContext,
    CognitiveMemoryRecallExclusionReasonKind ExclusionReasonKind);

public sealed record CognitiveMemoryConsolidationRunView(
    Guid Id,
    Guid? ProjectId,
    CognitiveMemoryConsolidationMode Mode,
    CognitiveMemoryConsolidationTriggerKind TriggerKind,
    CognitiveMemoryRunStatus Status,
    int SourceItemsScanned,
    int CandidatesCreated,
    int MutationCommandsSubmitted,
    int ReviewItemsCreated,
    int ProjectionInvalidations,
    string FailureCode,
    string FailureMessage,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc);

public sealed record CognitiveMemoryProjectionHealthView(
    CognitiveMemoryProjectionId Id,
    Guid? ProjectId,
    CognitiveMemoryProjectionKind ProjectionKind,
    CognitiveMemoryProjectionStatus Status,
    string TargetProvider,
    bool RebuildRequired,
    string FailureCode,
    string FailureMessage,
    DateTimeOffset UpdatedAtUtc);

public sealed record CognitiveMemoryProcedureSkillView(
    CognitiveMemoryProcedureSkillId Id,
    Guid ProjectId,
    string Title,
    CognitiveMemoryProcedureSkillMaturity Maturity,
    CognitiveMemoryRiskLevel RiskLevel,
    CognitiveMemoryValidationState ValidationState,
    CognitiveMemoryAccessLevel AccessLevel,
    CognitiveMemoryScoreProjectionBucket MaturityBucket,
    double? DisplayMaturityScore,
    int StepCount,
    int FailureModeCount,
    int ValidationEvidenceCount,
    int AutomationBindingCount,
    DateTimeOffset UpdatedAtUtc);

public sealed record CognitiveMemoryReplayJobView(
    Guid Id,
    Guid ProjectId,
    CognitiveMemoryReplayJobKind JobKind,
    CognitiveMemoryReplayJobState State,
    CognitiveMemoryScoreProjectionBucket PriorityBucket,
    double? DisplayPriorityProjection,
    int QueuePriority,
    string Reason,
    string FailureCode,
    string FailureMessage,
    DateTimeOffset UpdatedAtUtc);

public interface ICognitiveMemoryReviewUiService
{
    ValueTask<CognitiveMemoryReviewUiSnapshot> GetSnapshotAsync(
        CognitiveMemoryReviewUiQuery query,
        CancellationToken cancellationToken = default);

    ValueTask<CognitiveMemoryReviewQueueItem> DecideReviewItemAsync(
        CognitiveMemoryReviewDecisionRequest request,
        CancellationToken cancellationToken = default);
}
