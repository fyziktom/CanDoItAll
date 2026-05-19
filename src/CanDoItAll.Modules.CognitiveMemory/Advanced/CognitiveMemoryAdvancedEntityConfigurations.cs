using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.CognitiveMemory;

internal sealed class CognitiveMemoryProbeSessionRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryProbeSessionRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryProbeSessionRecord> builder)
    {
        builder.ToTable("CognitiveMemory_ProbeSessions");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.ProjectId, item.Status, item.CreatedAtUtc });
        builder.HasIndex(item => new { item.ProjectId, item.WorkspaceFrameId, item.Status });
        builder.Property(item => item.Title).HasMaxLength(300);
        builder.Property(item => item.ActorId).HasMaxLength(160);
        builder.Property(item => item.PolicyProfileId).HasMaxLength(160);
        builder.Property(item => item.ProjectionCollectionName).HasMaxLength(160);
        builder.Property(item => item.ProjectionProfileId).HasMaxLength(160);
        builder.Property(item => item.EmbeddingProfileId).HasMaxLength(160);
        builder.Property(item => item.AlgorithmVersion).HasMaxLength(80);
        builder.Property(item => item.ConcurrencyToken).IsConcurrencyToken();
    }
}

internal sealed class CognitiveMemoryProbeTurnRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryProbeTurnRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryProbeTurnRecord> builder)
    {
        builder.ToTable("CognitiveMemory_ProbeTurns");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.ProbeSessionId, item.Sequence }).IsUnique();
        builder.HasIndex(item => new { item.ProjectId, item.Status, item.CreatedAtUtc });
        builder.HasIndex(item => item.RecallTraceId);
        builder.HasIndex(item => item.AnswerGateDecisionId);
        builder.Property(item => item.Question).HasMaxLength(2000);
        builder.Property(item => item.AnswerSummary).HasMaxLength(4000);
        builder.Property(item => item.WarningsJson).HasMaxLength(4000);
        builder.Property(item => item.MetadataJson).HasMaxLength(8000);
        builder.Property(item => item.ConcurrencyToken).IsConcurrencyToken();
    }
}

internal sealed class CognitiveMemoryProbeFeedbackRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryProbeFeedbackRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryProbeFeedbackRecord> builder)
    {
        builder.ToTable("CognitiveMemory_ProbeFeedback");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.ProbeTurnId, item.Action, item.CreatedAtUtc });
        builder.HasIndex(item => new { item.ProjectId, item.CalibrationOutcome, item.CreatedAtUtc });
        builder.Property(item => item.Notes).HasMaxLength(2000);
        builder.Property(item => item.CorrectionText).HasMaxLength(8000);
        builder.Property(item => item.ConcurrencyToken).IsConcurrencyToken();
    }
}

internal sealed class CognitiveMemoryProbeFindingRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryProbeFindingRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryProbeFindingRecord> builder)
    {
        builder.ToTable("CognitiveMemory_ProbeFindings");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.ProbeTurnId, item.FindingKind });
        builder.HasIndex(item => new { item.ProjectId, item.FindingKind, item.CreatedAtUtc });
        builder.Property(item => item.Summary).HasMaxLength(2000);
    }
}

internal sealed class CognitiveMemoryProbeRegressionTestCaseRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryProbeRegressionTestCaseRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryProbeRegressionTestCaseRecord> builder)
    {
        builder.ToTable("CognitiveMemory_ProbeRegressionTestCases");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.ProjectId, item.Status, item.CreatedAtUtc });
        builder.HasIndex(item => item.ProbeTurnId);
        builder.Property(item => item.Question).HasMaxLength(2000);
        builder.Property(item => item.ExpectedEvidenceText).HasMaxLength(4000);
        builder.Property(item => item.ExpectedContextKey).HasMaxLength(300);
        builder.Property(item => item.AccessPolicyProfileId).HasMaxLength(160);
        builder.Property(item => item.EvaluatorProfileVersion).HasMaxLength(80);
        builder.Property(item => item.ConcurrencyToken).IsConcurrencyToken();
    }
}

internal sealed class CognitiveMemoryProbeRegressionRunRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryProbeRegressionRunRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryProbeRegressionRunRecord> builder)
    {
        builder.ToTable("CognitiveMemory_ProbeRegressionRuns");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.ProjectId, item.Outcome, item.StartedAtUtc });
        builder.HasIndex(item => new { item.RegressionTestCaseId, item.StartedAtUtc });
        builder.Property(item => item.FailureReason).HasMaxLength(2000);
        builder.Property(item => item.EvaluatorProfileVersion).HasMaxLength(80);
        builder.Property(item => item.ConcurrencyToken).IsConcurrencyToken();
    }
}

internal sealed class CognitiveMemorySelfModelProfileRecordConfiguration : IEntityTypeConfiguration<CognitiveMemorySelfModelProfileRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemorySelfModelProfileRecord> builder)
    {
        builder.ToTable("CognitiveMemory_SelfModelProfiles");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.ProjectId, item.ModelProfileId, item.RoleKey, item.Status });
        builder.Property(item => item.ModelProfileId).HasConversion(item => item.Value, value => new CognitiveMemoryModelProfileId(value)).HasMaxLength(160);
        builder.Property(item => item.RoleKey).HasConversion(item => item.Value, value => new CognitiveMemoryRoleKey(value)).HasMaxLength(160);
        builder.Property(item => item.ProfileVersion).HasMaxLength(80);
        builder.Property(item => item.OperatingPrinciples).HasMaxLength(4000);
        builder.Property(item => item.AllowedTaskCategoriesJson).HasMaxLength(4000);
        builder.Property(item => item.RestrictedTaskCategoriesJson).HasMaxLength(4000);
        builder.Property(item => item.AlgorithmVersion).HasMaxLength(80);
        builder.Property(item => item.ConcurrencyToken).IsConcurrencyToken();
    }
}

internal sealed class CognitiveMemoryDomainCompetenceProfileRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryDomainCompetenceProfileRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryDomainCompetenceProfileRecord> builder)
    {
        builder.ToTable("CognitiveMemory_DomainCompetenceProfiles");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.ProjectId, item.ModelProfileId, item.DomainKey, item.TaskTypeKey, item.ProfileVersion }).IsUnique();
        builder.HasIndex(item => item.CompetenceScoreEvaluationTraceId);
        builder.Property(item => item.DomainKey).HasMaxLength(160);
        builder.Property(item => item.TaskTypeKey).HasMaxLength(160);
        builder.Property(item => item.ModelProfileId).HasConversion(item => item.Value, value => new CognitiveMemoryModelProfileId(value)).HasMaxLength(160);
        builder.Property(item => item.ProfileVersion).HasMaxLength(80);
        builder.Property(item => item.EvidenceRefsJson).HasMaxLength(8000);
        builder.Property(item => item.ConcurrencyToken).IsConcurrencyToken();
    }
}

internal sealed class CognitiveMemoryKnownFailurePatternRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryKnownFailurePatternRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryKnownFailurePatternRecord> builder)
    {
        builder.ToTable("CognitiveMemory_KnownFailurePatterns");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.ProjectId, item.PatternKind, item.DomainKey, item.TaskTypeKey });
        builder.HasIndex(item => item.PatternScoreEvaluationTraceId);
        builder.Property(item => item.DomainKey).HasMaxLength(160);
        builder.Property(item => item.TaskTypeKey).HasMaxLength(160);
        builder.Property(item => item.TriggerSummary).HasMaxLength(2000);
        builder.Property(item => item.Mitigation).HasMaxLength(2000);
        builder.Property(item => item.EvidenceRefsJson).HasMaxLength(8000);
        builder.Property(item => item.ConcurrencyToken).IsConcurrencyToken();
    }
}

internal sealed class CognitiveMemorySelfRegulationPolicyProfileRecordConfiguration : IEntityTypeConfiguration<CognitiveMemorySelfRegulationPolicyProfileRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemorySelfRegulationPolicyProfileRecord> builder)
    {
        builder.ToTable("CognitiveMemory_SelfRegulationPolicyProfiles");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.ProjectId, item.PolicyKey, item.ProfileVersion }).IsUnique();
        builder.Property(item => item.PolicyKey).HasMaxLength(160);
        builder.Property(item => item.ProfileVersion).HasMaxLength(80);
        builder.Property(item => item.AllowedPosturesJson).HasMaxLength(4000);
        builder.Property(item => item.RequiredOperationsJson).HasMaxLength(4000);
        builder.Property(item => item.ConcurrencyToken).IsConcurrencyToken();
    }
}

internal sealed class CognitiveMemorySelfModelUpdateProposalRecordConfiguration : IEntityTypeConfiguration<CognitiveMemorySelfModelUpdateProposalRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemorySelfModelUpdateProposalRecord> builder)
    {
        builder.ToTable("CognitiveMemory_SelfModelUpdateProposals");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.ProjectId, item.Status, item.CreatedAtUtc });
        builder.Property(item => item.ModelProfileId).HasConversion(item => item.Value, value => new CognitiveMemoryModelProfileId(value)).HasMaxLength(160);
        builder.Property(item => item.DomainKey).HasMaxLength(160);
        builder.Property(item => item.ProposedChange).HasMaxLength(4000);
        builder.Property(item => item.EvidenceRefsJson).HasMaxLength(8000);
        builder.Property(item => item.RequestedByActorId).HasMaxLength(160);
        builder.Property(item => item.ConcurrencyToken).IsConcurrencyToken();
    }
}

internal sealed class CognitiveMemoryCalibrationEventRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryCalibrationEventRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryCalibrationEventRecord> builder)
    {
        builder.ToTable("CognitiveMemory_CalibrationEvents");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.ProjectId, item.DomainKey, item.TaskTypeKey, item.ModelProfileId, item.ObservedAtUtc });
        builder.HasIndex(item => new { item.ProjectId, item.OutcomeKind, item.ObservedAtUtc });
        builder.Property(item => item.DomainKey).HasMaxLength(160);
        builder.Property(item => item.TaskTypeKey).HasMaxLength(160);
        builder.Property(item => item.ModelProfileId).HasConversion(item => item.Value, value => new CognitiveMemoryModelProfileId(value)).HasMaxLength(160);
        builder.Property(item => item.RiskKey).HasConversion(item => item.Value, value => new CognitiveMemoryRiskKey(value)).HasMaxLength(80);
        builder.Property(item => item.FeaturePatternKey).HasMaxLength(160);
        builder.Property(item => item.ProfileVersion).HasMaxLength(80);
        builder.Property(item => item.ConcurrencyToken).IsConcurrencyToken();
    }
}

internal sealed class CognitiveMemoryCalibrationAggregateRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryCalibrationAggregateRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryCalibrationAggregateRecord> builder)
    {
        builder.ToTable("CognitiveMemory_CalibrationAggregates");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.ProjectId, item.DomainKey, item.TaskTypeKey, item.ModelProfileId, item.RiskKey, item.FeaturePatternKey, item.ProfileVersion }).IsUnique();
        builder.HasIndex(item => item.CalibrationScoreEvaluationTraceId);
        builder.Property(item => item.DomainKey).HasMaxLength(160);
        builder.Property(item => item.TaskTypeKey).HasMaxLength(160);
        builder.Property(item => item.ModelProfileId).HasConversion(item => item.Value, value => new CognitiveMemoryModelProfileId(value)).HasMaxLength(160);
        builder.Property(item => item.RiskKey).HasConversion(item => item.Value, value => new CognitiveMemoryRiskKey(value)).HasMaxLength(80);
        builder.Property(item => item.FeaturePatternKey).HasMaxLength(160);
        builder.Property(item => item.ProfileVersion).HasMaxLength(80);
        builder.Property(item => item.ConcurrencyToken).IsConcurrencyToken();
    }
}

internal sealed class CognitiveMemoryCalibrationBinRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryCalibrationBinRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryCalibrationBinRecord> builder)
    {
        builder.ToTable("CognitiveMemory_CalibrationBins");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.CalibrationAggregateId, item.BinIndex }).IsUnique();
    }
}

internal sealed class CognitiveMemorySelfRegulationAssessmentRecordConfiguration : IEntityTypeConfiguration<CognitiveMemorySelfRegulationAssessmentRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemorySelfRegulationAssessmentRecord> builder)
    {
        builder.ToTable("CognitiveMemory_SelfRegulationAssessments");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.ProjectId, item.State, item.CreatedAtUtc });
        builder.HasIndex(item => item.RecallTraceId);
        builder.HasIndex(item => item.AssessmentScoreEvaluationTraceId);
        builder.Property(item => item.ActorId).HasMaxLength(160);
        builder.Property(item => item.ModelProfileId).HasConversion(item => item.Value, value => new CognitiveMemoryModelProfileId(value)).HasMaxLength(160);
        builder.Property(item => item.DomainKey).HasMaxLength(160);
        builder.Property(item => item.TaskTypeKey).HasMaxLength(160);
        builder.Property(item => item.WarningsJson).HasMaxLength(4000);
        builder.Property(item => item.RequiredOperationsJson).HasMaxLength(4000);
        builder.Property(item => item.AlgorithmVersion).HasMaxLength(80);
        builder.Property(item => item.ConcurrencyToken).IsConcurrencyToken();
    }
}

internal sealed class CognitiveMemoryHumilityTriggerRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryHumilityTriggerRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryHumilityTriggerRecord> builder)
    {
        builder.ToTable("CognitiveMemory_HumilityTriggers");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.SelfRegulationAssessmentId, item.TriggerKind }).IsUnique();
        builder.Property(item => item.Reason).HasMaxLength(2000);
    }
}

internal sealed class CognitiveMemoryConfidenceReinforcementRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryConfidenceReinforcementRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryConfidenceReinforcementRecord> builder)
    {
        builder.ToTable("CognitiveMemory_ConfidenceReinforcements");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.SelfRegulationAssessmentId, item.ReinforcementKind });
        builder.Property(item => item.Reason).HasMaxLength(2000);
    }
}

internal sealed class CognitiveMemoryAnswerPostureDecisionRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryAnswerPostureDecisionRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryAnswerPostureDecisionRecord> builder)
    {
        builder.ToTable("CognitiveMemory_AnswerPostureDecisions");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.ProjectId, item.Posture, item.CreatedAtUtc });
        builder.HasIndex(item => item.SelfRegulationAssessmentId);
        builder.HasIndex(item => item.PostureScoreEvaluationTraceId);
        builder.Property(item => item.RequiredOperationsJson).HasMaxLength(4000);
        builder.Property(item => item.WarningsJson).HasMaxLength(4000);
        builder.Property(item => item.Reason).HasMaxLength(2000);
        builder.Property(item => item.ConcurrencyToken).IsConcurrencyToken();
    }
}

internal sealed class CognitiveMemoryProfessorReviewRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryProfessorReviewRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryProfessorReviewRecord> builder)
    {
        builder.ToTable("CognitiveMemory_ProfessorReviews");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.ProjectId, item.ReviewMode, item.Status, item.CreatedAtUtc });
        builder.HasIndex(item => item.SelfRegulationAssessmentId);
        builder.HasIndex(item => item.RoutingScoreEvaluationTraceId);
        builder.Property(item => item.RequestedByActorId).HasMaxLength(160);
        builder.Property(item => item.ModelProfileId).HasConversion(item => item.Value, value => new CognitiveMemoryModelProfileId(value)).HasMaxLength(160);
        builder.Property(item => item.PromptProfileVersion).HasMaxLength(80);
        builder.Property(item => item.PolicyProfileId).HasMaxLength(160);
        builder.Property(item => item.InputSummary).HasMaxLength(4000);
        builder.Property(item => item.ContextSummary).HasMaxLength(4000);
        builder.Property(item => item.Critique).HasMaxLength(8000);
        builder.Property(item => item.MissingEvidence).HasMaxLength(4000);
        builder.Property(item => item.OutputHash).HasMaxLength(128);
        builder.Property(item => item.ConcurrencyToken).IsConcurrencyToken();
    }
}

internal sealed class CognitiveMemoryProfessorReviewActionRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryProfessorReviewActionRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryProfessorReviewActionRecord> builder)
    {
        builder.ToTable("CognitiveMemory_ProfessorReviewActions");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.ProfessorReviewId, item.SuggestionKind });
        builder.Property(item => item.Summary).HasMaxLength(2000);
    }
}

internal sealed class CognitiveMemoryAnswerGateDecisionRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryAnswerGateDecisionRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryAnswerGateDecisionRecord> builder)
    {
        builder.ToTable("CognitiveMemory_AnswerGateDecisions");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.ProjectId, item.DecisionKind, item.CreatedAtUtc });
        builder.HasIndex(item => item.RecallTraceId);
        builder.HasIndex(item => item.SelfRegulationAssessmentId);
        builder.HasIndex(item => item.ScoreEvaluationTraceId);
        builder.Property(item => item.WarningsJson).HasMaxLength(4000);
        builder.Property(item => item.RequiredOperationsJson).HasMaxLength(4000);
        builder.Property(item => item.Reason).HasMaxLength(2000);
        builder.Property(item => item.DraftAnswerSummary).HasMaxLength(4000);
        builder.Property(item => item.ConcurrencyToken).IsConcurrencyToken();
    }
}

internal sealed class CognitiveMemoryKnowledgeRegionRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryKnowledgeRegionRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryKnowledgeRegionRecord> builder)
    {
        builder.ToTable("CognitiveMemory_KnowledgeRegions");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.ProjectId, item.RegionKind, item.RegionKey }).IsUnique();
        builder.Property(item => item.RegionKey).HasMaxLength(200);
        builder.Property(item => item.DisplayName).HasMaxLength(300);
        builder.Property(item => item.ConcurrencyToken).IsConcurrencyToken();
    }
}

internal sealed class CognitiveMemoryCoverageMapRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryCoverageMapRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryCoverageMapRecord> builder)
    {
        builder.ToTable("CognitiveMemory_CoverageMaps");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.ProjectId, item.KnowledgeRegionId }).IsUnique();
        builder.HasIndex(item => new { item.ProjectId, item.CoverageState, item.RefreshedAtUtc });
        builder.Property(item => item.ConcurrencyToken).IsConcurrencyToken();
    }
}

internal sealed class CognitiveMemoryKnowledgeGapRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryKnowledgeGapRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryKnowledgeGapRecord> builder)
    {
        builder.ToTable("CognitiveMemory_KnowledgeGaps");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.ProjectId, item.KnowledgeRegionId, item.GapKind, item.CreatedAtUtc });
        builder.Property(item => item.Summary).HasMaxLength(2000);
        builder.Property(item => item.EvidenceRefsJson).HasMaxLength(8000);
        builder.Property(item => item.ConcurrencyToken).IsConcurrencyToken();
    }
}

internal sealed class CognitiveMemoryLearningProposalRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryLearningProposalRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryLearningProposalRecord> builder)
    {
        builder.ToTable("CognitiveMemory_LearningProposals");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.ProjectId, item.Status, item.CreatedAtUtc });
        builder.HasIndex(item => item.NeedScoreEvaluationTraceId);
        builder.Property(item => item.Title).HasMaxLength(300);
        builder.Property(item => item.Explanation).HasMaxLength(4000);
        builder.Property(item => item.EvidenceRefsJson).HasMaxLength(8000);
        builder.Property(item => item.Risks).HasConversion(item => item.Value, value => new CognitiveMemoryRiskNotes(value)).HasMaxLength(2000);
        builder.Property(item => item.AcceptanceCriteria).HasMaxLength(2000);
        builder.Property(item => item.DecidedByActorId).HasMaxLength(160);
        builder.Property(item => item.DecisionNotes).HasMaxLength(2000);
        builder.Property(item => item.ConcurrencyToken).IsConcurrencyToken();
    }
}

internal sealed class CognitiveMemoryLearningTaskRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryLearningTaskRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryLearningTaskRecord> builder)
    {
        builder.ToTable("CognitiveMemory_LearningTasks");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.ProjectId, item.LearningProposalId, item.Status });
        builder.Property(item => item.WorkflowExecutorKey).HasMaxLength(200);
        builder.Property(item => item.ApprovalActorId).HasMaxLength(160);
        builder.Property(item => item.ConcurrencyToken).IsConcurrencyToken();
    }
}

internal sealed class CognitiveMemoryLearningOutcomeRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryLearningOutcomeRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryLearningOutcomeRecord> builder)
    {
        builder.ToTable("CognitiveMemory_LearningOutcomes");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.ProjectId, item.LearningTaskId, item.OutcomeKind });
        builder.Property(item => item.Summary).HasMaxLength(4000);
        builder.Property(item => item.SourceRefsJson).HasMaxLength(8000);
    }
}

internal sealed class CognitiveMemoryCrossProjectPromotionCandidateRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryCrossProjectPromotionCandidateRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryCrossProjectPromotionCandidateRecord> builder)
    {
        builder.ToTable("CognitiveMemory_CrossProjectPromotionCandidates");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.SourceProjectId, item.SourceMemoryRecordId, item.Status });
        builder.HasIndex(item => item.PromotionScoreEvaluationTraceId);
        builder.Property(item => item.RequestedByActorId).HasMaxLength(160);
        builder.Property(item => item.Reason).HasMaxLength(2000);
        builder.Property(item => item.DecidedByActorId).HasMaxLength(160);
        builder.Property(item => item.DecisionNotes).HasMaxLength(2000);
        builder.Property(item => item.ConcurrencyToken).IsConcurrencyToken();
    }
}

internal sealed class CognitiveMemoryDistributedWorkerRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryDistributedWorkerRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryDistributedWorkerRecord> builder)
    {
        builder.ToTable("CognitiveMemory_DistributedWorkers");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => item.WorkerId).IsUnique();
        builder.HasIndex(item => new { item.Status, item.LastSeenAtUtc });
        builder.Property(item => item.WorkerId).HasMaxLength(160);
        builder.Property(item => item.MachineName).HasMaxLength(160);
        builder.Property(item => item.CapabilitiesJson).HasMaxLength(2000);
        builder.Property(item => item.ConcurrencyToken).IsConcurrencyToken();
    }
}

internal sealed class CognitiveMemoryDistributedJobRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryDistributedJobRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryDistributedJobRecord> builder)
    {
        builder.ToTable("CognitiveMemory_DistributedJobs");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.ProjectId, item.JobKind, item.State, item.CreatedAtUtc });
        builder.HasIndex(item => new { item.ProjectId, item.JobKind, item.InputHash }).IsUnique();
        builder.HasIndex(item => new { item.LeasedWorkerId, item.LeaseExpiresAtUtc });
        builder.Property(item => item.SourceScopeKey).HasMaxLength(300);
        builder.Property(item => item.InputPayloadJson).HasMaxLength(16000);
        builder.Property(item => item.InputHash).HasMaxLength(128);
        builder.Property(item => item.ExpectedOutputSchema).HasMaxLength(160);
        builder.Property(item => item.AlgorithmVersion).HasMaxLength(80);
        builder.Property(item => item.PolicyProfileId).HasMaxLength(160);
        builder.Property(item => item.LeaseToken).HasMaxLength(160);
        builder.Property(item => item.LeasedWorkerId).HasMaxLength(160);
        builder.Property(item => item.ConcurrencyToken).IsConcurrencyToken();
    }
}

internal sealed class CognitiveMemoryDistributedWorkerResultRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryDistributedWorkerResultRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryDistributedWorkerResultRecord> builder)
    {
        builder.ToTable("CognitiveMemory_DistributedWorkerResults");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.DistributedJobId, item.WorkerId, item.SubmittedAtUtc });
        builder.HasIndex(item => new { item.ProjectId, item.Status, item.SubmittedAtUtc });
        builder.Property(item => item.WorkerId).HasMaxLength(160);
        builder.Property(item => item.InputHash).HasMaxLength(128);
        builder.Property(item => item.OutputHash).HasMaxLength(128);
        builder.Property(item => item.AlgorithmVersion).HasMaxLength(80);
        builder.Property(item => item.OutputSchema).HasMaxLength(160);
        builder.Property(item => item.OutputPayloadJson).HasMaxLength(16000);
        builder.Property(item => item.RejectionReason).HasMaxLength(2000);
        builder.Property(item => item.ConcurrencyToken).IsConcurrencyToken();
    }
}
