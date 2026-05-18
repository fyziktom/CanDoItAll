using Microsoft.EntityFrameworkCore.Metadata;

namespace CanDoItAll.Modules.CognitiveMemory;

public sealed record CognitiveMemoryIndexExpectation(
    Type EntityType,
    IReadOnlyList<string> PropertyNames,
    bool IsUnique);

public static class CognitiveMemoryEfGuardrails
{
    public static IReadOnlyList<CognitiveMemoryIndexExpectation> FoundationIndexExpectations { get; } =
    [
        new(typeof(CognitiveMemorySourceManifestRecord), [nameof(CognitiveMemorySourceManifestRecord.SourceSystem), nameof(CognitiveMemorySourceManifestRecord.SourceScopeKey), nameof(CognitiveMemorySourceManifestRecord.SourceSnapshotId)], true),
        new(typeof(CognitiveMemorySourceManifestRecord), [nameof(CognitiveMemorySourceManifestRecord.ProjectId), nameof(CognitiveMemorySourceManifestRecord.SourceSystem), nameof(CognitiveMemorySourceManifestRecord.ObservedAtUtc)], false),
        new(typeof(CognitiveMemorySourceItemRecord), [nameof(CognitiveMemorySourceItemRecord.SourceManifestId), nameof(CognitiveMemorySourceItemRecord.SourceItemKey)], true),
        new(typeof(CognitiveMemorySourceItemRecord), [nameof(CognitiveMemorySourceItemRecord.ProjectId), nameof(CognitiveMemorySourceItemRecord.SourceSystem), nameof(CognitiveMemorySourceItemRecord.SourceItemType)], false),
        new(typeof(CognitiveMemoryRecord), [nameof(CognitiveMemoryRecord.ProjectId), nameof(CognitiveMemoryRecord.Kind), nameof(CognitiveMemoryRecord.ValidationState)], false),
        new(typeof(CognitiveMemoryRelationRecord), [nameof(CognitiveMemoryRelationRecord.ProjectId), nameof(CognitiveMemoryRelationRecord.SourceMemoryRecordId), nameof(CognitiveMemoryRelationRecord.TargetMemoryRecordId), nameof(CognitiveMemoryRelationRecord.RelationKind)], true),
        new(typeof(CognitiveMemoryProjectionStateRecord), [nameof(CognitiveMemoryProjectionStateRecord.ProjectId), nameof(CognitiveMemoryProjectionStateRecord.ProjectionKind), nameof(CognitiveMemoryProjectionStateRecord.TargetProvider)], true),
        new(typeof(CognitiveMemoryRecallTraceRecord), [nameof(CognitiveMemoryRecallTraceRecord.ProjectId), nameof(CognitiveMemoryRecallTraceRecord.OperationMode), nameof(CognitiveMemoryRecallTraceRecord.StartedAtUtc)], false),
        new(typeof(CognitiveMemoryReviewItemRecord), [nameof(CognitiveMemoryReviewItemRecord.ProjectId), nameof(CognitiveMemoryReviewItemRecord.Status), nameof(CognitiveMemoryReviewItemRecord.RiskLevel)], false),
        new(typeof(CognitiveMemoryRunRecord), [nameof(CognitiveMemoryRunRecord.IdempotencyKey)], true)
    ];

    public static IReadOnlyList<CognitiveMemoryIndexExpectation> TaxonomyIndexExpectations { get; } =
    [
        new(typeof(CognitiveMemoryRecord), [nameof(CognitiveMemoryRecord.ProjectId), nameof(CognitiveMemoryRecord.TopicKey)], false),
        new(typeof(CognitiveMemoryRecord), [nameof(CognitiveMemoryRecord.PrimaryClaimId)], false),
        new(typeof(CognitiveMemoryRecordEvidenceAnchorRecord), [nameof(CognitiveMemoryRecordEvidenceAnchorRecord.MemoryRecordId), nameof(CognitiveMemoryRecordEvidenceAnchorRecord.EvidenceAnchorId), nameof(CognitiveMemoryRecordEvidenceAnchorRecord.EvidenceRole)], true),
        new(typeof(CognitiveMemoryRecordEvidenceAnchorRecord), [nameof(CognitiveMemoryRecordEvidenceAnchorRecord.EvidenceAnchorId), nameof(CognitiveMemoryRecordEvidenceAnchorRecord.EvidenceRole)], false),
        new(typeof(CognitiveMemoryRelationRecord), [nameof(CognitiveMemoryRelationRecord.RelationScoreEvaluationTraceId)], false),
        new(typeof(CognitiveMemoryRelationEvidenceRecord), [nameof(CognitiveMemoryRelationEvidenceRecord.RelationId), nameof(CognitiveMemoryRelationEvidenceRecord.EvidenceAnchorId), nameof(CognitiveMemoryRelationEvidenceRecord.Direction)], true),
        new(typeof(CognitiveMemoryRelationEvidenceRecord), [nameof(CognitiveMemoryRelationEvidenceRecord.EvidenceAnchorId), nameof(CognitiveMemoryRelationEvidenceRecord.Direction)], false),
        new(typeof(CognitiveMemoryProjectionRecord), [nameof(CognitiveMemoryProjectionRecord.MemoryRecordId), nameof(CognitiveMemoryProjectionRecord.ProjectionStoreKind), nameof(CognitiveMemoryProjectionRecord.ProjectionKind), nameof(CognitiveMemoryProjectionRecord.ProjectionProfileId), nameof(CognitiveMemoryProjectionRecord.EmbeddingProfileId)], true),
        new(typeof(CognitiveMemoryProjectionRecord), [nameof(CognitiveMemoryProjectionRecord.ProjectId), nameof(CognitiveMemoryProjectionRecord.CollectionName), nameof(CognitiveMemoryProjectionRecord.Status)], false),
        new(typeof(CognitiveMemoryProjectionRecord), [nameof(CognitiveMemoryProjectionRecord.ProjectId), nameof(CognitiveMemoryProjectionRecord.RebuildRequired), nameof(CognitiveMemoryProjectionRecord.StaleReason)], false),
        new(typeof(CognitiveMemoryProjectionRecord), [nameof(CognitiveMemoryProjectionRecord.SourceHash)], false),
        new(typeof(CognitiveMemoryProjectionRecord), [nameof(CognitiveMemoryProjectionRecord.PayloadHash)], false),
        new(typeof(CognitiveMemoryProjectionRecord), [nameof(CognitiveMemoryProjectionRecord.PointId)], true)
    ];

    public static IReadOnlyList<CognitiveMemoryIndexExpectation> WorkspaceIndexExpectations { get; } =
    [
        new(typeof(CognitiveMemoryWorkspaceFrameRecord), [nameof(CognitiveMemoryWorkspaceFrameRecord.ProjectId), nameof(CognitiveMemoryWorkspaceFrameRecord.FrameKind), nameof(CognitiveMemoryWorkspaceFrameRecord.Status), nameof(CognitiveMemoryWorkspaceFrameRecord.ExpiresAtUtc)], false),
        new(typeof(CognitiveMemoryWorkspaceFrameRecord), [nameof(CognitiveMemoryWorkspaceFrameRecord.ProjectId), nameof(CognitiveMemoryWorkspaceFrameRecord.OwnerUserId), nameof(CognitiveMemoryWorkspaceFrameRecord.Status)], false),
        new(typeof(CognitiveMemoryWorkspaceFrameRecord), [nameof(CognitiveMemoryWorkspaceFrameRecord.ProjectId), nameof(CognitiveMemoryWorkspaceFrameRecord.OwnerAgentId), nameof(CognitiveMemoryWorkspaceFrameRecord.Status)], false),
        new(typeof(CognitiveMemoryWorkspaceFrameRecord), [nameof(CognitiveMemoryWorkspaceFrameRecord.ProjectId), nameof(CognitiveMemoryWorkspaceFrameRecord.ProcessRunId), nameof(CognitiveMemoryWorkspaceFrameRecord.ProcessStepId), nameof(CognitiveMemoryWorkspaceFrameRecord.Status)], false),
        new(typeof(CognitiveMemoryWorkspaceFrameRecord), [nameof(CognitiveMemoryWorkspaceFrameRecord.ProjectId), nameof(CognitiveMemoryWorkspaceFrameRecord.WorkflowRunId), nameof(CognitiveMemoryWorkspaceFrameRecord.Status)], false),
        new(typeof(CognitiveMemoryWorkspaceFrameRecord), [nameof(CognitiveMemoryWorkspaceFrameRecord.ProjectId), nameof(CognitiveMemoryWorkspaceFrameRecord.ProbeSessionId), nameof(CognitiveMemoryWorkspaceFrameRecord.Status)], false),
        new(typeof(CognitiveMemoryWorkspaceFrameRecord), [nameof(CognitiveMemoryWorkspaceFrameRecord.ProjectId), nameof(CognitiveMemoryWorkspaceFrameRecord.ReviewSessionId), nameof(CognitiveMemoryWorkspaceFrameRecord.Status)], false),
        new(typeof(CognitiveMemoryWorkspaceFrameRecord), [nameof(CognitiveMemoryWorkspaceFrameRecord.ProjectId), nameof(CognitiveMemoryWorkspaceFrameRecord.LearningTaskId), nameof(CognitiveMemoryWorkspaceFrameRecord.Status)], false),
        new(typeof(CognitiveMemoryWorkspaceGoalRecord), [nameof(CognitiveMemoryWorkspaceGoalRecord.WorkspaceFrameId), nameof(CognitiveMemoryWorkspaceGoalRecord.Sequence)], true),
        new(typeof(CognitiveMemoryWorkingMemorySlotRecord), [nameof(CognitiveMemoryWorkingMemorySlotRecord.WorkspaceFrameId), nameof(CognitiveMemoryWorkingMemorySlotRecord.SlotKind), nameof(CognitiveMemoryWorkingMemorySlotRecord.CreatedAtUtc)], false),
        new(typeof(CognitiveMemoryWorkspaceSlotEvidenceAnchorRecord), [nameof(CognitiveMemoryWorkspaceSlotEvidenceAnchorRecord.WorkspaceSlotId), nameof(CognitiveMemoryWorkspaceSlotEvidenceAnchorRecord.EvidenceAnchorId)], true),
        new(typeof(CognitiveMemoryWorkspaceOpenQuestionRecord), [nameof(CognitiveMemoryWorkspaceOpenQuestionRecord.WorkspaceFrameId), nameof(CognitiveMemoryWorkspaceOpenQuestionRecord.Status), nameof(CognitiveMemoryWorkspaceOpenQuestionRecord.CreatedAtUtc)], false),
        new(typeof(CognitiveMemoryInhibitedCandidateRecord), [nameof(CognitiveMemoryInhibitedCandidateRecord.WorkspaceFrameId), nameof(CognitiveMemoryInhibitedCandidateRecord.ReasonKind), nameof(CognitiveMemoryInhibitedCandidateRecord.CreatedAtUtc)], false),
        new(typeof(CognitiveMemoryAttentionDecisionRecord), [nameof(CognitiveMemoryAttentionDecisionRecord.ProjectId), nameof(CognitiveMemoryAttentionDecisionRecord.WorkspaceFrameId), nameof(CognitiveMemoryAttentionDecisionRecord.CreatedAtUtc)], false),
        new(typeof(CognitiveMemoryAttentionDecisionRecord), [nameof(CognitiveMemoryAttentionDecisionRecord.WorkspaceFrameId), nameof(CognitiveMemoryAttentionDecisionRecord.DecisionKind)], false),
        new(typeof(CognitiveMemoryAttentionDecisionRecord), [nameof(CognitiveMemoryAttentionDecisionRecord.RoutingScoreEvaluationTraceId)], false),
        new(typeof(CognitiveMemoryRecallTraceRecord), [nameof(CognitiveMemoryRecallTraceRecord.WorkspaceFrameId)], false),
        new(typeof(CognitiveMemoryRecallTraceRecord), [nameof(CognitiveMemoryRecallTraceRecord.AttentionDecisionId)], false)
    ];

    public static IReadOnlyList<CognitiveMemoryIndexExpectation> SignalIndexExpectations { get; } =
    [
        new(typeof(CognitiveMemoryPredictionExpectationRecord), [nameof(CognitiveMemoryPredictionExpectationRecord.ProjectId), nameof(CognitiveMemoryPredictionExpectationRecord.ExpectationKind), nameof(CognitiveMemoryPredictionExpectationRecord.CreatedAtUtc)], false),
        new(typeof(CognitiveMemoryPredictionExpectationRecord), [nameof(CognitiveMemoryPredictionExpectationRecord.ProjectId), nameof(CognitiveMemoryPredictionExpectationRecord.ActorKind), nameof(CognitiveMemoryPredictionExpectationRecord.ActorId)], false),
        new(typeof(CognitiveMemoryPredictionExpectationEvidenceAnchorRecord), [nameof(CognitiveMemoryPredictionExpectationEvidenceAnchorRecord.PredictionExpectationId), nameof(CognitiveMemoryPredictionExpectationEvidenceAnchorRecord.EvidenceAnchorId)], true),
        new(typeof(CognitiveMemoryPredictionErrorRecord), [nameof(CognitiveMemoryPredictionErrorRecord.ProjectId), nameof(CognitiveMemoryPredictionErrorRecord.ErrorKind), nameof(CognitiveMemoryPredictionErrorRecord.ObservedAtUtc)], false),
        new(typeof(CognitiveMemoryPredictionErrorRecord), [nameof(CognitiveMemoryPredictionErrorRecord.ProjectId), nameof(CognitiveMemoryPredictionErrorRecord.RequiresReview), nameof(CognitiveMemoryPredictionErrorRecord.ObservedAtUtc)], false),
        new(typeof(CognitiveMemoryPredictionErrorRecord), [nameof(CognitiveMemoryPredictionErrorRecord.PredictionExpectationId)], false),
        new(typeof(CognitiveMemoryPredictionErrorRecord), [nameof(CognitiveMemoryPredictionErrorRecord.SeverityScoreEvaluationTraceId)], false),
        new(typeof(CognitiveMemoryPredictionErrorEvidenceAnchorRecord), [nameof(CognitiveMemoryPredictionErrorEvidenceAnchorRecord.PredictionErrorId), nameof(CognitiveMemoryPredictionErrorEvidenceAnchorRecord.EvidenceAnchorId)], true),
        new(typeof(CognitiveMemoryPredictionErrorSignalRecord), [nameof(CognitiveMemoryPredictionErrorSignalRecord.PredictionErrorId), nameof(CognitiveMemoryPredictionErrorSignalRecord.CognitiveSignalId)], true),
        new(typeof(CognitiveMemorySignalRecord), [nameof(CognitiveMemorySignalRecord.ProjectId), nameof(CognitiveMemorySignalRecord.SignalKind), nameof(CognitiveMemorySignalRecord.ObservedAtUtc)], false),
        new(typeof(CognitiveMemorySignalRecord), [nameof(CognitiveMemorySignalRecord.ProjectId), nameof(CognitiveMemorySignalRecord.SourceKind), nameof(CognitiveMemorySignalRecord.ObservedAtUtc)], false),
        new(typeof(CognitiveMemorySignalRecord), [nameof(CognitiveMemorySignalRecord.ProjectId), nameof(CognitiveMemorySignalRecord.RequiresReview), nameof(CognitiveMemorySignalRecord.ObservedAtUtc)], false),
        new(typeof(CognitiveMemorySignalRecord), [nameof(CognitiveMemorySignalRecord.ProjectId), nameof(CognitiveMemorySignalRecord.ActorKind), nameof(CognitiveMemorySignalRecord.ActorId)], false),
        new(typeof(CognitiveMemorySignalRecord), [nameof(CognitiveMemorySignalRecord.ProjectId), nameof(CognitiveMemorySignalRecord.WorkspaceFrameId), nameof(CognitiveMemorySignalRecord.ObservedAtUtc)], false),
        new(typeof(CognitiveMemorySignalRecord), [nameof(CognitiveMemorySignalRecord.SignalScoreEvaluationTraceId)], false),
        new(typeof(CognitiveMemorySignalEvidenceAnchorRecord), [nameof(CognitiveMemorySignalEvidenceAnchorRecord.CognitiveSignalId), nameof(CognitiveMemorySignalEvidenceAnchorRecord.EvidenceAnchorId)], true),
        new(typeof(CognitiveMemorySignalConsumerPolicyRecord), [nameof(CognitiveMemorySignalConsumerPolicyRecord.CognitiveSignalId), nameof(CognitiveMemorySignalConsumerPolicyRecord.ConsumerKind)], true),
        new(typeof(CognitiveMemorySignalConsumerPolicyRecord), [nameof(CognitiveMemorySignalConsumerPolicyRecord.ProjectId), nameof(CognitiveMemorySignalConsumerPolicyRecord.ConsumerKind), nameof(CognitiveMemorySignalConsumerPolicyRecord.CreatedAtUtc)], false)
    ];

    public static IReadOnlyList<CognitiveMemoryIndexExpectation> RecallIndexExpectations { get; } =
    [
        new(typeof(CognitiveMemoryRecallTraceRecord), [nameof(CognitiveMemoryRecallTraceRecord.ProjectId), nameof(CognitiveMemoryRecallTraceRecord.RecallMode), nameof(CognitiveMemoryRecallTraceRecord.Outcome), nameof(CognitiveMemoryRecallTraceRecord.StartedAtUtc)], false),
        new(typeof(CognitiveMemoryRecallTraceStageRecord), [nameof(CognitiveMemoryRecallTraceStageRecord.RecallTraceId), nameof(CognitiveMemoryRecallTraceStageRecord.StageKind), nameof(CognitiveMemoryRecallTraceStageRecord.ChannelKind)], false),
        new(typeof(CognitiveMemoryRecallTraceStageRecord), [nameof(CognitiveMemoryRecallTraceStageRecord.ProjectId), nameof(CognitiveMemoryRecallTraceStageRecord.StageKind), nameof(CognitiveMemoryRecallTraceStageRecord.Status), nameof(CognitiveMemoryRecallTraceStageRecord.StartedAtUtc)], false),
        new(typeof(CognitiveMemoryRecallCandidateRecord), [nameof(CognitiveMemoryRecallCandidateRecord.RecallTraceId), nameof(CognitiveMemoryRecallCandidateRecord.DecisionKind), nameof(CognitiveMemoryRecallCandidateRecord.PrimaryChannelKind)], false),
        new(typeof(CognitiveMemoryRecallCandidateRecord), [nameof(CognitiveMemoryRecallCandidateRecord.ProjectId), nameof(CognitiveMemoryRecallCandidateRecord.MemoryRecordId), nameof(CognitiveMemoryRecallCandidateRecord.DecisionKind)], false),
        new(typeof(CognitiveMemoryRecallCandidateRecord), [nameof(CognitiveMemoryRecallCandidateRecord.ProjectId), nameof(CognitiveMemoryRecallCandidateRecord.PrimaryChannelKind), nameof(CognitiveMemoryRecallCandidateRecord.CreatedAtUtc)], false),
        new(typeof(CognitiveMemoryRecallCandidateRecord), [nameof(CognitiveMemoryRecallCandidateRecord.ScoreEvaluationTraceId)], false),
        new(typeof(CognitiveMemoryRecallContextPackRecord), [nameof(CognitiveMemoryRecallContextPackRecord.RecallTraceId)], true),
        new(typeof(CognitiveMemoryRecallContextPackRecord), [nameof(CognitiveMemoryRecallContextPackRecord.ProjectId), nameof(CognitiveMemoryRecallContextPackRecord.CreatedAtUtc)], false),
        new(typeof(CognitiveMemoryRecallContextSectionRecord), [nameof(CognitiveMemoryRecallContextSectionRecord.ContextPackId), nameof(CognitiveMemoryRecallContextSectionRecord.Sequence)], true),
        new(typeof(CognitiveMemoryRecallContextSectionRecord), [nameof(CognitiveMemoryRecallContextSectionRecord.RecallTraceId), nameof(CognitiveMemoryRecallContextSectionRecord.SectionKind)], false),
        new(typeof(CognitiveMemoryRecallSourceRefRecord), [nameof(CognitiveMemoryRecallSourceRefRecord.RecallTraceId), nameof(CognitiveMemoryRecallSourceRefRecord.MemoryRecordId), nameof(CognitiveMemoryRecallSourceRefRecord.IncludedInContext)], false),
        new(typeof(CognitiveMemoryRecallSourceRefRecord), [nameof(CognitiveMemoryRecallSourceRefRecord.ContextPackId), nameof(CognitiveMemoryRecallSourceRefRecord.IncludedInContext)], false),
        new(typeof(CognitiveMemoryRecallSourceRefRecord), [nameof(CognitiveMemoryRecallSourceRefRecord.ProjectId), nameof(CognitiveMemoryRecallSourceRefRecord.SourceSystem), nameof(CognitiveMemoryRecallSourceRefRecord.IncludedInContext)], false)
    ];

    public static IReadOnlyList<CognitiveMemoryIndexExpectation> ConsolidationIndexExpectations { get; } =
    [
        new(typeof(CognitiveMemoryConsolidationRunRecord), [nameof(CognitiveMemoryConsolidationRunRecord.IdempotencyKey)], true),
        new(typeof(CognitiveMemoryConsolidationRunRecord), [nameof(CognitiveMemoryConsolidationRunRecord.ProjectId), nameof(CognitiveMemoryConsolidationRunRecord.Mode), nameof(CognitiveMemoryConsolidationRunRecord.Status), nameof(CognitiveMemoryConsolidationRunRecord.StartedAtUtc)], false),
        new(typeof(CognitiveMemoryConsolidationRunRecord), [nameof(CognitiveMemoryConsolidationRunRecord.ProjectId), nameof(CognitiveMemoryConsolidationRunRecord.Mode), nameof(CognitiveMemoryConsolidationRunRecord.LeaseExpiresAtUtc)], false),
        new(typeof(CognitiveMemoryConsolidationCandidateRecord), [nameof(CognitiveMemoryConsolidationCandidateRecord.RunId), nameof(CognitiveMemoryConsolidationCandidateRecord.CandidateKind), nameof(CognitiveMemoryConsolidationCandidateRecord.Status)], false),
        new(typeof(CognitiveMemoryConsolidationCandidateRecord), [nameof(CognitiveMemoryConsolidationCandidateRecord.ProjectId), nameof(CognitiveMemoryConsolidationCandidateRecord.CandidateKind), nameof(CognitiveMemoryConsolidationCandidateRecord.Status)], false),
        new(typeof(CognitiveMemoryConsolidationCandidateRecord), [nameof(CognitiveMemoryConsolidationCandidateRecord.ProjectId), nameof(CognitiveMemoryConsolidationCandidateRecord.SourceItemId), nameof(CognitiveMemoryConsolidationCandidateRecord.CandidateKind), nameof(CognitiveMemoryConsolidationCandidateRecord.SourceContentHash), nameof(CognitiveMemoryConsolidationCandidateRecord.AlgorithmVersion)], true),
        new(typeof(CognitiveMemoryConsolidationCursorRecord), [nameof(CognitiveMemoryConsolidationCursorRecord.ProjectId), nameof(CognitiveMemoryConsolidationCursorRecord.Mode), nameof(CognitiveMemoryConsolidationCursorRecord.SourceSystem)], true),
        new(typeof(CognitiveMemoryConsolidationReportRecord), [nameof(CognitiveMemoryConsolidationReportRecord.RunId)], true)
    ];

    public static IReadOnlyList<CognitiveMemoryIndexExpectation> TemporalReplayIndexExpectations { get; } =
    [
        new(typeof(CognitiveMemoryTemporalEpisodeRecord), [nameof(CognitiveMemoryTemporalEpisodeRecord.ProjectId), nameof(CognitiveMemoryTemporalEpisodeRecord.EpisodeKind), nameof(CognitiveMemoryTemporalEpisodeRecord.StartedAtUtc)], false),
        new(typeof(CognitiveMemoryEpisodeStepRecord), [nameof(CognitiveMemoryEpisodeStepRecord.EpisodeId), nameof(CognitiveMemoryEpisodeStepRecord.SequenceIndex)], true),
        new(typeof(CognitiveMemoryEpisodeStepRecord), [nameof(CognitiveMemoryEpisodeStepRecord.ProjectId), nameof(CognitiveMemoryEpisodeStepRecord.ActorKind), nameof(CognitiveMemoryEpisodeStepRecord.ActorId)], false),
        new(typeof(CognitiveMemoryTemporalEpisodeLinkRecord), [nameof(CognitiveMemoryTemporalEpisodeLinkRecord.EpisodeId), nameof(CognitiveMemoryTemporalEpisodeLinkRecord.LinkKind), nameof(CognitiveMemoryTemporalEpisodeLinkRecord.TargetId), nameof(CognitiveMemoryTemporalEpisodeLinkRecord.TargetKey)], true),
        new(typeof(CognitiveMemoryEpisodeStepEvidenceRecord), [nameof(CognitiveMemoryEpisodeStepEvidenceRecord.StepId), nameof(CognitiveMemoryEpisodeStepEvidenceRecord.EvidenceRole), nameof(CognitiveMemoryEpisodeStepEvidenceRecord.EvidenceAnchorId)], true),
        new(typeof(CognitiveMemoryEpisodeCausalLinkRecord), [nameof(CognitiveMemoryEpisodeCausalLinkRecord.EpisodeId), nameof(CognitiveMemoryEpisodeCausalLinkRecord.LinkKind), nameof(CognitiveMemoryEpisodeCausalLinkRecord.FromStepId), nameof(CognitiveMemoryEpisodeCausalLinkRecord.ToStepId)], false),
        new(typeof(CognitiveMemoryReplayJobRecord), [nameof(CognitiveMemoryReplayJobRecord.ProjectId), nameof(CognitiveMemoryReplayJobRecord.State), nameof(CognitiveMemoryReplayJobRecord.ScheduledAtUtc)], false),
        new(typeof(CognitiveMemoryReplayJobRecord), [nameof(CognitiveMemoryReplayJobRecord.ProjectId), nameof(CognitiveMemoryReplayJobRecord.JobKind), nameof(CognitiveMemoryReplayJobRecord.InputHash)], true),
        new(typeof(CognitiveMemoryReplayJobTargetRecord), [nameof(CognitiveMemoryReplayJobTargetRecord.ReplayJobId), nameof(CognitiveMemoryReplayJobTargetRecord.TargetKind), nameof(CognitiveMemoryReplayJobTargetRecord.TargetId), nameof(CognitiveMemoryReplayJobTargetRecord.TargetKey)], true),
        new(typeof(CognitiveMemoryReplayJobSignalRecord), [nameof(CognitiveMemoryReplayJobSignalRecord.ReplayJobId), nameof(CognitiveMemoryReplayJobSignalRecord.CognitiveSignalId)], true),
        new(typeof(CognitiveMemoryReplayJobPredictionErrorRecord), [nameof(CognitiveMemoryReplayJobPredictionErrorRecord.ReplayJobId), nameof(CognitiveMemoryReplayJobPredictionErrorRecord.PredictionErrorId)], true),
        new(typeof(CognitiveMemoryReplayOutputRecord), [nameof(CognitiveMemoryReplayOutputRecord.ProjectId), nameof(CognitiveMemoryReplayOutputRecord.OutputKind), nameof(CognitiveMemoryReplayOutputRecord.Status)], false),
        new(typeof(CognitiveMemoryReplayWorkerResultRecord), [nameof(CognitiveMemoryReplayWorkerResultRecord.ProjectId), nameof(CognitiveMemoryReplayWorkerResultRecord.Status), nameof(CognitiveMemoryReplayWorkerResultRecord.SubmittedAtUtc)], false)
    ];

    public static IReadOnlyList<CognitiveMemoryIndexExpectation> ProceduralIndexExpectations { get; } =
    [
        new(typeof(CognitiveMemoryProcedureSkillRecord), [nameof(CognitiveMemoryProcedureSkillRecord.ProjectId), nameof(CognitiveMemoryProcedureSkillRecord.Maturity), nameof(CognitiveMemoryProcedureSkillRecord.ValidationState)], false),
        new(typeof(CognitiveMemoryProcedureSkillRecord), [nameof(CognitiveMemoryProcedureSkillRecord.ProjectId), nameof(CognitiveMemoryProcedureSkillRecord.RiskLevel), nameof(CognitiveMemoryProcedureSkillRecord.Maturity)], false),
        new(typeof(CognitiveMemoryProcedureSkillRecord), [nameof(CognitiveMemoryProcedureSkillRecord.MaturityScoreEvaluationTraceId)], false),
        new(typeof(CognitiveMemoryProcedureStepRecord), [nameof(CognitiveMemoryProcedureStepRecord.ProcedureSkillId), nameof(CognitiveMemoryProcedureStepRecord.SequenceIndex)], true),
        new(typeof(CognitiveMemoryProcedureStepRecord), [nameof(CognitiveMemoryProcedureStepRecord.ProcedureSkillId), nameof(CognitiveMemoryProcedureStepRecord.StepKey)], true),
        new(typeof(CognitiveMemoryProcedureStepEvidenceRecord), [nameof(CognitiveMemoryProcedureStepEvidenceRecord.ProcedureStepId), nameof(CognitiveMemoryProcedureStepEvidenceRecord.EvidenceAnchorId)], true),
        new(typeof(CognitiveMemoryProcedureFailureModeRecord), [nameof(CognitiveMemoryProcedureFailureModeRecord.ProcedureSkillId), nameof(CognitiveMemoryProcedureFailureModeRecord.FailureKey)], true),
        new(typeof(CognitiveMemoryProcedureFailureModePredictionErrorRecord), [nameof(CognitiveMemoryProcedureFailureModePredictionErrorRecord.ProcedureFailureModeId), nameof(CognitiveMemoryProcedureFailureModePredictionErrorRecord.PredictionErrorId)], true),
        new(typeof(CognitiveMemoryProcedureFailureModeEpisodeRecord), [nameof(CognitiveMemoryProcedureFailureModeEpisodeRecord.ProcedureFailureModeId), nameof(CognitiveMemoryProcedureFailureModeEpisodeRecord.EpisodeId)], true),
        new(typeof(CognitiveMemoryProcedureValidationEvidenceRecord), [nameof(CognitiveMemoryProcedureValidationEvidenceRecord.ProcedureSkillId), nameof(CognitiveMemoryProcedureValidationEvidenceRecord.EvidenceRole), nameof(CognitiveMemoryProcedureValidationEvidenceRecord.EvidenceAnchorId)], true),
        new(typeof(CognitiveMemoryProcedureAutomationBindingRecord), [nameof(CognitiveMemoryProcedureAutomationBindingRecord.ProcedureSkillId), nameof(CognitiveMemoryProcedureAutomationBindingRecord.BindingKind), nameof(CognitiveMemoryProcedureAutomationBindingRecord.BindingKey)], true),
        new(typeof(CognitiveMemoryProcedureAutomationBindingRecord), [nameof(CognitiveMemoryProcedureAutomationBindingRecord.ProjectId), nameof(CognitiveMemoryProcedureAutomationBindingRecord.State), nameof(CognitiveMemoryProcedureAutomationBindingRecord.BindingKind)], false),
        new(typeof(CognitiveMemoryProcedureSimulationRecord), [nameof(CognitiveMemoryProcedureSimulationRecord.ProjectId), nameof(CognitiveMemoryProcedureSimulationRecord.Status), nameof(CognitiveMemoryProcedureSimulationRecord.CreatedAtUtc)], false),
        new(typeof(CognitiveMemoryProcedureSimulationRecord), [nameof(CognitiveMemoryProcedureSimulationRecord.ProjectId), nameof(CognitiveMemoryProcedureSimulationRecord.OutputKind), nameof(CognitiveMemoryProcedureSimulationRecord.RiskLevel)], false),
        new(typeof(CognitiveMemoryProcedureSimulationSkillRecord), [nameof(CognitiveMemoryProcedureSimulationSkillRecord.SimulationId), nameof(CognitiveMemoryProcedureSimulationSkillRecord.ProcedureSkillId)], true),
        new(typeof(CognitiveMemoryProcedureSimulationEvidenceRecord), [nameof(CognitiveMemoryProcedureSimulationEvidenceRecord.SimulationId), nameof(CognitiveMemoryProcedureSimulationEvidenceRecord.EvidenceAnchorId)], true)
    ];

    public static IReadOnlyList<CognitiveMemoryIndexExpectation> ScoreGeometryIndexExpectations { get; } =
    [
        new(typeof(CognitiveMemoryScoreEvaluationTraceRecord), [nameof(CognitiveMemoryScoreEvaluationTraceRecord.ProjectId), nameof(CognitiveMemoryScoreEvaluationTraceRecord.SpaceKind), nameof(CognitiveMemoryScoreEvaluationTraceRecord.SchemaVersion), nameof(CognitiveMemoryScoreEvaluationTraceRecord.CalculatedAtUtc)], false),
        new(typeof(CognitiveMemoryScoreEvaluationTraceRecord), [nameof(CognitiveMemoryScoreEvaluationTraceRecord.OwnerKind), nameof(CognitiveMemoryScoreEvaluationTraceRecord.OwnerId), nameof(CognitiveMemoryScoreEvaluationTraceRecord.SpaceKind)], false),
        new(typeof(CognitiveMemoryScoreEvaluationTraceRecord), [nameof(CognitiveMemoryScoreEvaluationTraceRecord.InputHash)], false),
        new(typeof(CognitiveMemoryScoreComponentRecord), [nameof(CognitiveMemoryScoreComponentRecord.ScoreEvaluationTraceId), nameof(CognitiveMemoryScoreComponentRecord.DimensionKind)], false),
        new(typeof(CognitiveMemoryScoreComponentRecord), [nameof(CognitiveMemoryScoreComponentRecord.ProjectId), nameof(CognitiveMemoryScoreComponentRecord.SpaceKind), nameof(CognitiveMemoryScoreComponentRecord.DimensionKind), nameof(CognitiveMemoryScoreComponentRecord.CalculatedAtUtc)], false),
        new(typeof(CognitiveMemoryScoreComponentRecord), [nameof(CognitiveMemoryScoreComponentRecord.OwnerKind), nameof(CognitiveMemoryScoreComponentRecord.OwnerId), nameof(CognitiveMemoryScoreComponentRecord.DimensionKind)], false),
        new(typeof(CognitiveMemoryScoreComponentRecord), [nameof(CognitiveMemoryScoreComponentRecord.SchemaVersion), nameof(CognitiveMemoryScoreComponentRecord.DimensionKind)], false)
    ];

    public static IReadOnlyList<CognitiveMemoryIndexExpectation> NeuroFoundationIndexExpectations { get; } =
    [
        new(typeof(CognitiveMemoryEvidenceAnchorRecord), [nameof(CognitiveMemoryEvidenceAnchorRecord.ProjectId), nameof(CognitiveMemoryEvidenceAnchorRecord.SourceManifestId), nameof(CognitiveMemoryEvidenceAnchorRecord.SourceItemId)], false),
        new(typeof(CognitiveMemoryEvidenceAnchorRecord), [nameof(CognitiveMemoryEvidenceAnchorRecord.ProjectId), nameof(CognitiveMemoryEvidenceAnchorRecord.AnchorKind), nameof(CognitiveMemoryEvidenceAnchorRecord.ObservedAtUtc)], false),
        new(typeof(CognitiveMemoryClaimRecord), [nameof(CognitiveMemoryClaimRecord.ProjectId), nameof(CognitiveMemoryClaimRecord.ClaimKind), nameof(CognitiveMemoryClaimRecord.CurrentBeliefState), nameof(CognitiveMemoryClaimRecord.ValidationState)], false),
        new(typeof(CognitiveMemoryClaimRecord), [nameof(CognitiveMemoryClaimRecord.ProjectId), nameof(CognitiveMemoryClaimRecord.SubjectKey), nameof(CognitiveMemoryClaimRecord.PredicateKey), nameof(CognitiveMemoryClaimRecord.ObjectKey)], false),
        new(typeof(CognitiveMemoryClaimEvidenceLinkRecord), [nameof(CognitiveMemoryClaimEvidenceLinkRecord.ClaimId), nameof(CognitiveMemoryClaimEvidenceLinkRecord.EvidenceAnchorId), nameof(CognitiveMemoryClaimEvidenceLinkRecord.Direction)], true),
        new(typeof(CognitiveMemoryBeliefStateRecord), [nameof(CognitiveMemoryBeliefStateRecord.ClaimId), nameof(CognitiveMemoryBeliefStateRecord.CalculatedAtUtc)], false),
        new(typeof(CognitiveMemoryEntityRecord), [nameof(CognitiveMemoryEntityRecord.ProjectId), nameof(CognitiveMemoryEntityRecord.EntityKind), nameof(CognitiveMemoryEntityRecord.CanonicalNameKey)], true),
        new(typeof(CognitiveMemoryEntityAliasRecord), [nameof(CognitiveMemoryEntityAliasRecord.ProjectId), nameof(CognitiveMemoryEntityAliasRecord.EntityKind), nameof(CognitiveMemoryEntityAliasRecord.AliasKey)], true),
        new(typeof(CognitiveMemoryContextFrameRecord), [nameof(CognitiveMemoryContextFrameRecord.ProjectId), nameof(CognitiveMemoryContextFrameRecord.FrameKind), nameof(CognitiveMemoryContextFrameRecord.DisplayName)], false),
        new(typeof(CognitiveMemoryContextFrameDimensionRecord), [nameof(CognitiveMemoryContextFrameDimensionRecord.ContextFrameId), nameof(CognitiveMemoryContextFrameDimensionRecord.DimensionKind), nameof(CognitiveMemoryContextFrameDimensionRecord.ValueKey)], true),
        new(typeof(CognitiveMemoryContextBoundaryRecord), [nameof(CognitiveMemoryContextBoundaryRecord.ProjectId), nameof(CognitiveMemoryContextBoundaryRecord.SourceContextFrameId), nameof(CognitiveMemoryContextBoundaryRecord.TargetContextFrameId), nameof(CognitiveMemoryContextBoundaryRecord.BoundaryKind)], true),
        new(typeof(CognitiveMemoryMutationCommandRecord), [nameof(CognitiveMemoryMutationCommandRecord.ProjectId), nameof(CognitiveMemoryMutationCommandRecord.IdempotencyKey)], true),
        new(typeof(CognitiveMemoryMutationAuditEventRecord), [nameof(CognitiveMemoryMutationAuditEventRecord.MutationCommandId), nameof(CognitiveMemoryMutationAuditEventRecord.Sequence)], true)
    ];

    public static IReadOnlyList<CognitiveMemoryIndexExpectation> SourceIngestionIndexExpectations { get; } =
    [
        new(typeof(CognitiveMemorySourceItemLayoutRecord), [nameof(CognitiveMemorySourceItemLayoutRecord.SourceItemId)], true),
        new(typeof(CognitiveMemorySourceItemGraphLinkRecord), [nameof(CognitiveMemorySourceItemGraphLinkRecord.SourceManifestId), nameof(CognitiveMemorySourceItemGraphLinkRecord.SourceItemKey), nameof(CognitiveMemorySourceItemGraphLinkRecord.TargetSourceItemKey), nameof(CognitiveMemorySourceItemGraphLinkRecord.LinkKind)], true),
        new(typeof(CognitiveMemorySourceItemContextHintRecord), [nameof(CognitiveMemorySourceItemContextHintRecord.SourceItemId), nameof(CognitiveMemorySourceItemContextHintRecord.ContextFrameId)], true),
        new(typeof(CognitiveMemorySourceTombstoneRecord), [nameof(CognitiveMemorySourceTombstoneRecord.SourceSystem), nameof(CognitiveMemorySourceTombstoneRecord.SourceScopeKey), nameof(CognitiveMemorySourceTombstoneRecord.SourceItemKey), nameof(CognitiveMemorySourceTombstoneRecord.DetectedInManifestId)], true),
        new(typeof(CognitiveMemorySourceScanFailureRecord), [nameof(CognitiveMemorySourceScanFailureRecord.RunId)], false),
        new(typeof(CognitiveMemorySourceScanFailureRecord), [nameof(CognitiveMemorySourceScanFailureRecord.ProjectId), nameof(CognitiveMemorySourceScanFailureRecord.SourceSystem), nameof(CognitiveMemorySourceScanFailureRecord.CreatedAtUtc)], false)
    ];

    public static IReadOnlyList<CognitiveMemoryIndexExpectation> AdvancedIndexExpectations { get; } =
    [
        new(typeof(CognitiveMemoryProbeSessionRecord), [nameof(CognitiveMemoryProbeSessionRecord.ProjectId), nameof(CognitiveMemoryProbeSessionRecord.Status), nameof(CognitiveMemoryProbeSessionRecord.CreatedAtUtc)], false),
        new(typeof(CognitiveMemoryProbeTurnRecord), [nameof(CognitiveMemoryProbeTurnRecord.ProbeSessionId), nameof(CognitiveMemoryProbeTurnRecord.Sequence)], true),
        new(typeof(CognitiveMemoryProbeFeedbackRecord), [nameof(CognitiveMemoryProbeFeedbackRecord.ProjectId), nameof(CognitiveMemoryProbeFeedbackRecord.CalibrationOutcome), nameof(CognitiveMemoryProbeFeedbackRecord.CreatedAtUtc)], false),
        new(typeof(CognitiveMemoryProbeFindingRecord), [nameof(CognitiveMemoryProbeFindingRecord.ProjectId), nameof(CognitiveMemoryProbeFindingRecord.FindingKind), nameof(CognitiveMemoryProbeFindingRecord.CreatedAtUtc)], false),
        new(typeof(CognitiveMemoryProbeRegressionTestCaseRecord), [nameof(CognitiveMemoryProbeRegressionTestCaseRecord.ProjectId), nameof(CognitiveMemoryProbeRegressionTestCaseRecord.Status), nameof(CognitiveMemoryProbeRegressionTestCaseRecord.CreatedAtUtc)], false),
        new(typeof(CognitiveMemorySelfModelProfileRecord), [nameof(CognitiveMemorySelfModelProfileRecord.ProjectId), nameof(CognitiveMemorySelfModelProfileRecord.ModelProfileId), nameof(CognitiveMemorySelfModelProfileRecord.RoleKey), nameof(CognitiveMemorySelfModelProfileRecord.Status)], false),
        new(typeof(CognitiveMemoryDomainCompetenceProfileRecord), [nameof(CognitiveMemoryDomainCompetenceProfileRecord.ProjectId), nameof(CognitiveMemoryDomainCompetenceProfileRecord.ModelProfileId), nameof(CognitiveMemoryDomainCompetenceProfileRecord.DomainKey), nameof(CognitiveMemoryDomainCompetenceProfileRecord.TaskTypeKey), nameof(CognitiveMemoryDomainCompetenceProfileRecord.ProfileVersion)], true),
        new(typeof(CognitiveMemorySelfRegulationPolicyProfileRecord), [nameof(CognitiveMemorySelfRegulationPolicyProfileRecord.ProjectId), nameof(CognitiveMemorySelfRegulationPolicyProfileRecord.PolicyKey), nameof(CognitiveMemorySelfRegulationPolicyProfileRecord.ProfileVersion)], true),
        new(typeof(CognitiveMemoryCalibrationAggregateRecord), [nameof(CognitiveMemoryCalibrationAggregateRecord.ProjectId), nameof(CognitiveMemoryCalibrationAggregateRecord.DomainKey), nameof(CognitiveMemoryCalibrationAggregateRecord.TaskTypeKey), nameof(CognitiveMemoryCalibrationAggregateRecord.ModelProfileId), nameof(CognitiveMemoryCalibrationAggregateRecord.RiskKey), nameof(CognitiveMemoryCalibrationAggregateRecord.FeaturePatternKey), nameof(CognitiveMemoryCalibrationAggregateRecord.ProfileVersion)], true),
        new(typeof(CognitiveMemorySelfRegulationAssessmentRecord), [nameof(CognitiveMemorySelfRegulationAssessmentRecord.ProjectId), nameof(CognitiveMemorySelfRegulationAssessmentRecord.State), nameof(CognitiveMemorySelfRegulationAssessmentRecord.CreatedAtUtc)], false),
        new(typeof(CognitiveMemoryAnswerPostureDecisionRecord), [nameof(CognitiveMemoryAnswerPostureDecisionRecord.ProjectId), nameof(CognitiveMemoryAnswerPostureDecisionRecord.Posture), nameof(CognitiveMemoryAnswerPostureDecisionRecord.CreatedAtUtc)], false),
        new(typeof(CognitiveMemoryProfessorReviewRecord), [nameof(CognitiveMemoryProfessorReviewRecord.ProjectId), nameof(CognitiveMemoryProfessorReviewRecord.ReviewMode), nameof(CognitiveMemoryProfessorReviewRecord.Status), nameof(CognitiveMemoryProfessorReviewRecord.CreatedAtUtc)], false),
        new(typeof(CognitiveMemoryAnswerGateDecisionRecord), [nameof(CognitiveMemoryAnswerGateDecisionRecord.ProjectId), nameof(CognitiveMemoryAnswerGateDecisionRecord.DecisionKind), nameof(CognitiveMemoryAnswerGateDecisionRecord.CreatedAtUtc)], false),
        new(typeof(CognitiveMemoryKnowledgeRegionRecord), [nameof(CognitiveMemoryKnowledgeRegionRecord.ProjectId), nameof(CognitiveMemoryKnowledgeRegionRecord.RegionKind), nameof(CognitiveMemoryKnowledgeRegionRecord.RegionKey)], true),
        new(typeof(CognitiveMemoryCoverageMapRecord), [nameof(CognitiveMemoryCoverageMapRecord.ProjectId), nameof(CognitiveMemoryCoverageMapRecord.KnowledgeRegionId)], true),
        new(typeof(CognitiveMemoryLearningProposalRecord), [nameof(CognitiveMemoryLearningProposalRecord.ProjectId), nameof(CognitiveMemoryLearningProposalRecord.Status), nameof(CognitiveMemoryLearningProposalRecord.CreatedAtUtc)], false),
        new(typeof(CognitiveMemoryCrossProjectPromotionCandidateRecord), [nameof(CognitiveMemoryCrossProjectPromotionCandidateRecord.SourceProjectId), nameof(CognitiveMemoryCrossProjectPromotionCandidateRecord.SourceMemoryRecordId), nameof(CognitiveMemoryCrossProjectPromotionCandidateRecord.Status)], false),
        new(typeof(CognitiveMemoryDistributedWorkerRecord), [nameof(CognitiveMemoryDistributedWorkerRecord.WorkerId)], true),
        new(typeof(CognitiveMemoryDistributedJobRecord), [nameof(CognitiveMemoryDistributedJobRecord.ProjectId), nameof(CognitiveMemoryDistributedJobRecord.JobKind), nameof(CognitiveMemoryDistributedJobRecord.InputHash)], true),
        new(typeof(CognitiveMemoryDistributedWorkerResultRecord), [nameof(CognitiveMemoryDistributedWorkerResultRecord.ProjectId), nameof(CognitiveMemoryDistributedWorkerResultRecord.Status), nameof(CognitiveMemoryDistributedWorkerResultRecord.SubmittedAtUtc)], false)
    ];

    public static bool HasExpectedIndex(IEntityType entityType, CognitiveMemoryIndexExpectation expectation)
        => entityType.GetIndexes().Any(index =>
            index.IsUnique == expectation.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(expectation.PropertyNames, StringComparer.Ordinal));
}
