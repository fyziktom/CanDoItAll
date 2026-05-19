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
    int Take = 12,
    bool IncludeResolvedReviewItems = false);

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
    IReadOnlyList<CognitiveMemoryReplayJobView> ReplayJobs,
    IReadOnlyList<CognitiveMemoryProbeSessionView> ProbeSessions,
    IReadOnlyList<CognitiveMemorySelfRegulationView> SelfRegulationAssessments,
    IReadOnlyList<CognitiveMemoryAnswerGateView> AnswerGateDecisions,
    IReadOnlyList<CognitiveMemoryProfessorReviewView> ProfessorReviews,
    IReadOnlyList<CognitiveMemoryLearningProposalView> LearningProposals,
    IReadOnlyList<CognitiveMemoryCrossProjectPromotionView> CrossProjectPromotions,
    IReadOnlyList<CognitiveMemoryDistributedJobView> DistributedJobs);

public sealed record CognitiveMemoryReviewUiSummary(
    int MemoryRecordCount,
    int PendingReviewCount,
    int HighRiskReviewCount,
    int RecallTraceCount,
    int ConsolidationIssueCount,
    int ProjectionIssueCount,
    int ProcedureReviewCount,
    int SimulationReviewCount,
    int ProbeSessionCount,
    int SelfRegulationActionCount,
    int AnswerGateInterventionCount,
    int ProfessorReviewCount,
    int LearningProposalCount,
    int CrossProjectReviewCount,
    int DistributedIssueCount);

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
    Guid ConcurrencyToken,
    CognitiveMemoryReviewCandidatePreview? CandidatePreview = null);

public sealed record CognitiveMemoryReviewCandidatePreview(
    Guid CandidateId,
    CognitiveMemoryConsolidationCandidateKind CandidateKind,
    CognitiveMemoryConsolidationCandidateStatus CandidateStatus,
    Guid? SourceItemId,
    Guid? EvidenceAnchorId,
    Guid? MemoryRecordId,
    Guid? MutationCommandId,
    CognitiveMemoryScoreProjectionBucket ScoreBucket,
    double? DisplayPriorityProjection,
    string ProposedTitle,
    string ProposedMemoryText,
    string ProposedReason,
    string SourceSystem,
    string SourceItemType,
    string SourceTitle,
    string SourceLocator,
    string SourceExcerpt,
    string SourceContentHash);

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

public sealed record CognitiveMemoryProbeSessionView(
    Guid Id,
    Guid ProjectId,
    CognitiveMemoryProbeSessionStatus Status,
    CognitiveMemoryRecallMode RecallMode,
    string Title,
    int TurnCount,
    DateTimeOffset UpdatedAtUtc);

public sealed record CognitiveMemorySelfRegulationView(
    Guid Id,
    Guid? ProjectId,
    CognitiveMemorySelfRegulationStateKind State,
    CognitiveMemoryScoreProjectionBucket AssessmentBucket,
    double? DisplayAssessmentScore,
    string DomainKey,
    string TaskTypeKey,
    string WarningsJson,
    string RequiredOperationsJson,
    DateTimeOffset CreatedAtUtc);

public sealed record CognitiveMemoryAnswerGateView(
    Guid Id,
    Guid ProjectId,
    CognitiveMemoryAnswerGateDecisionKind DecisionKind,
    CognitiveMemoryScoreProjectionBucket DecisionBucket,
    double? DisplayConfidenceProjection,
    string Reason,
    string WarningsJson,
    string RequiredOperationsJson,
    DateTimeOffset CreatedAtUtc);

public sealed record CognitiveMemoryProfessorReviewView(
    Guid Id,
    Guid? ProjectId,
    CognitiveMemoryProfessorReviewMode ReviewMode,
    CognitiveMemoryProfessorReviewStatus Status,
    string RequestedByActorId,
    string InputSummary,
    string MissingEvidence,
    bool RequiresHumanReview,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? CompletedAtUtc);

public sealed record CognitiveMemoryLearningProposalView(
    Guid Id,
    Guid ProjectId,
    CognitiveMemoryLearningProposalStatus Status,
    string Title,
    string Explanation,
    CognitiveMemoryScoreProjectionBucket NeedBucket,
    double? DisplayPriorityProjection,
    DateTimeOffset CreatedAtUtc);

public sealed record CognitiveMemoryCrossProjectPromotionView(
    Guid Id,
    Guid SourceProjectId,
    Guid SourceMemoryRecordId,
    CognitiveMemoryCrossProjectPromotionStatus Status,
    CognitiveMemoryScoreProjectionBucket PromotionBucket,
    string Reason,
    Guid? ReviewItemId,
    DateTimeOffset CreatedAtUtc);

public sealed record CognitiveMemoryDistributedJobView(
    Guid Id,
    Guid ProjectId,
    CognitiveMemoryDistributedJobKind JobKind,
    CognitiveMemoryDistributedJobState State,
    string SourceScopeKey,
    string LeasedWorkerId,
    DateTimeOffset CreatedAtUtc,
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
