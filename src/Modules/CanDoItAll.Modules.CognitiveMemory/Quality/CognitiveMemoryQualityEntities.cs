using CanDoItAll.Infrastructure.Persistence;

namespace CanDoItAll.Modules.CognitiveMemory;

public sealed class CognitiveMemoryQualityClusterRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? ProjectId { get; set; }

    public string ClusterHash { get; set; } = string.Empty;

    public CognitiveMemoryQualityClusterKeyFamily PrimaryKeyFamily { get; set; } = CognitiveMemoryQualityClusterKeyFamily.SemanticTopic;

    public CognitiveMemoryQualityClusterReadiness Readiness { get; set; } = CognitiveMemoryQualityClusterReadiness.Unknown;

    public CognitiveMemoryAccessLevel AccessLevel { get; set; } = CognitiveMemoryAccessLevel.Project;

    public CognitiveMemoryRiskLevel RiskLevel { get; set; } = CognitiveMemoryRiskLevel.Low;

    public string PolicyProfileId { get; set; } = string.Empty;

    public string AlgorithmVersion { get; set; } = string.Empty;

    public int KeyCount { get; set; }

    public int MemberCount { get; set; }

    public int SourceEvidenceCount { get; set; }

    public int ContradictionCount { get; set; }

    public double CohesionScore { get; set; }

    public double SourceIndependenceScore { get; set; }

    public double SourceDiversityScore { get; set; }

    public double SemanticSignalScore { get; set; }

    public double SupportingSignalScore { get; set; }

    public double GuardPenaltyScore { get; set; }

    public double CompositeScore { get; set; }

    public bool AggregateEligible { get; set; }

    public string EligibilityReason { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

public sealed class CognitiveMemoryQualityClusterKeyRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ClusterId { get; set; }

    public Guid? ProjectId { get; set; }

    public CognitiveMemoryQualityClusterKeyFamily KeyFamily { get; set; } = CognitiveMemoryQualityClusterKeyFamily.SemanticTopic;

    public string Key { get; set; } = string.Empty;

    public string DisplayText { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class CognitiveMemoryQualityClusterMemberRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ClusterId { get; set; }

    public Guid? ProjectId { get; set; }

    public CognitiveMemoryQualityClusterMemberKind MemberKind { get; set; } = CognitiveMemoryQualityClusterMemberKind.MemoryRecord;

    public Guid? MemoryRecordId { get; set; }

    public Guid? SourceItemId { get; set; }

    public Guid? EvidenceAnchorId { get; set; }

    public CognitiveMemoryAccessLevel AccessLevel { get; set; } = CognitiveMemoryAccessLevel.Project;

    public CognitiveMemoryRiskLevel RiskLevel { get; set; } = CognitiveMemoryRiskLevel.Low;

    public CognitiveMemoryValidationState ValidationState { get; set; } = CognitiveMemoryValidationState.Draft;

    public CognitiveMemoryStabilityState StabilityState { get; set; } = CognitiveMemoryStabilityState.Unknown;

    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class CognitiveMemoryDreamRunRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? ProjectId { get; set; }

    public CognitiveMemoryConsolidationMode Mode { get; set; } = CognitiveMemoryConsolidationMode.ProjectNightly;

    public CognitiveMemoryConsolidationTriggerKind TriggerKind { get; set; } = CognitiveMemoryConsolidationTriggerKind.Manual;

    public CognitiveMemoryRunStatus Status { get; set; } = CognitiveMemoryRunStatus.Pending;

    public string IdempotencyKey { get; set; } = string.Empty;

    public string PolicyProfileId { get; set; } = string.Empty;

    public string AlgorithmVersion { get; set; } = string.Empty;

    public int ClustersConsidered { get; set; }

    public int ClusterMembersRead { get; set; }

    public int ClaimsExtracted { get; set; }

    public int AggregateCandidatesCreated { get; set; }

    public int AggregateClaimsCreated { get; set; }

    public int AggregateClaimSourceMapsCreated { get; set; }

    public int ValidationRecordsCreated { get; set; }

    public int ReviewItemsCreated { get; set; }

    public int ApprovedCandidates { get; set; }

    public int RejectedCandidates { get; set; }

    public int NeedsReviewCandidates { get; set; }

    public double EvidenceCoverageRatio { get; set; }

    public string WarningsJson { get; set; } = "[]";

    public DateTimeOffset StartedAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public string FailureCode { get; set; } = string.Empty;

    public string FailureMessage { get; set; } = string.Empty;

    public Guid ConcurrencyToken { get; set; }
}

public sealed class CognitiveMemoryDreamRunClusterRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid DreamRunId { get; set; }

    public Guid ClusterId { get; set; }

    public Guid? ProjectId { get; set; }

    public CognitiveMemoryQualityClusterReadiness Readiness { get; set; } = CognitiveMemoryQualityClusterReadiness.Unknown;

    public string SelectionReasonCode { get; set; } = string.Empty;

    public int MemberCount { get; set; }

    public int ClaimCount { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class CognitiveMemoryDreamAggregateCandidateRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid DreamRunId { get; set; }

    public Guid ClusterId { get; set; }

    public Guid? ProjectId { get; set; }

    public CognitiveMemoryConsolidationMode Mode { get; set; } = CognitiveMemoryConsolidationMode.ProjectNightly;

    public CognitiveMemoryDreamAggregateCandidateStatus Status { get; set; } = CognitiveMemoryDreamAggregateCandidateStatus.Proposed;

    public string Title { get; set; } = string.Empty;

    public string SummaryText { get; set; } = string.Empty;

    public string CanonicalText { get; set; } = string.Empty;

    public CognitiveMemoryAccessLevel AccessLevel { get; set; } = CognitiveMemoryAccessLevel.Project;

    public CognitiveMemoryRiskLevel RiskLevel { get; set; } = CognitiveMemoryRiskLevel.Low;

    public string AlgorithmVersion { get; set; } = string.Empty;

    public CognitiveMemoryHashAlgorithm PayloadHashAlgorithm { get; set; } = CognitiveMemoryHashAlgorithm.Sha256;

    public string PayloadHash { get; set; } = string.Empty;

    public Guid? ValidationRecordId { get; set; }

    public Guid? ReviewItemId { get; set; }

    public Guid? MemoryRecordId { get; set; }

    public int ClaimCount { get; set; }

    public int SourceMapCount { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

public sealed class CognitiveMemoryDreamAggregateClaimRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AggregateCandidateId { get; set; }

    public Guid? ProjectId { get; set; }

    public int Sequence { get; set; }

    public CognitiveMemoryClaimKind ClaimKind { get; set; } = CognitiveMemoryClaimKind.Fact;

    public string ClaimText { get; set; } = string.Empty;

    public string SubjectKey { get; set; } = string.Empty;

    public string PredicateKey { get; set; } = string.Empty;

    public string ObjectKey { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class CognitiveMemoryDreamAggregateClaimSourceMapRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AggregateCandidateId { get; set; }

    public Guid AggregateClaimId { get; set; }

    public Guid? ProjectId { get; set; }

    public Guid SourceMemoryRecordId { get; set; }

    public Guid? SourceItemId { get; set; }

    public Guid? EvidenceAnchorId { get; set; }

    public CognitiveMemoryEvidenceDirection Direction { get; set; } = CognitiveMemoryEvidenceDirection.Supports;

    public CognitiveMemoryAccessLevel AccessLevel { get; set; } = CognitiveMemoryAccessLevel.Project;

    public CognitiveMemoryRedactionState RedactionState { get; set; } = CognitiveMemoryRedactionState.Safe;

    public string Summary { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class CognitiveMemoryDreamValidationRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AggregateCandidateId { get; set; }

    public Guid? ProjectId { get; set; }

    public CognitiveMemoryDreamValidationDecision Decision { get; set; } = CognitiveMemoryDreamValidationDecision.NeedsHumanReview;

    public string PolicyProfileId { get; set; } = string.Empty;

    public int IssueCount { get; set; }

    public int ClaimsChecked { get; set; }

    public int SourceMapsChecked { get; set; }

    public string IssuesJson { get; set; } = "[]";

    public DateTimeOffset CreatedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

public sealed class CognitiveMemorySynthesizedRecallRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProjectId { get; set; }

    public Guid RecallTraceId { get; set; }

    public string Brief { get; set; } = string.Empty;

    public bool ReferencesShownByDefault { get; set; }

    public int StatementCount { get; set; }

    public int SourceMapCount { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

public sealed class CognitiveMemorySynthesizedStatementRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SynthesisId { get; set; }

    public Guid ProjectId { get; set; }

    public int Sequence { get; set; }

    public string Text { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class CognitiveMemorySynthesizedStatementSourceMapRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SynthesisId { get; set; }

    public Guid StatementId { get; set; }

    public Guid ProjectId { get; set; }

    public Guid MemoryRecordId { get; set; }

    public Guid? AggregateClaimId { get; set; }

    public Guid? SourceItemId { get; set; }

    public Guid? EvidenceAnchorId { get; set; }

    public string SourceSystem { get; set; } = string.Empty;

    public string Locator { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public CognitiveMemoryAccessLevel AccessLevel { get; set; } = CognitiveMemoryAccessLevel.Project;

    public CognitiveMemoryRedactionState RedactionState { get; set; } = CognitiveMemoryRedactionState.Safe;

    public DateTimeOffset CreatedAtUtc { get; set; }
}
