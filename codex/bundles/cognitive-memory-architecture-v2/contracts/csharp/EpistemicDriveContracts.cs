using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CanDoItAll.CognitiveMemory.Abstractions;

public enum KnowledgeNeedCategory
{
    Unknown = 0,
    HighTensionHighRoi = 1,
    HighTensionLowRoi = 2,
    LowTensionHighRoi = 3,
    LowTensionLowRoi = 4,
    HighUncertaintyLowUsage = 5,
    HighUsageHighConfidence = 6
}

public enum LearningProposalState
{
    Draft = 0,
    NeedsApproval = 1,
    Approved = 2,
    Rejected = 3,
    Snoozed = 4,
    ProbingRequested = 5,
    ConvertedToBundle = 6,
    InProgress = 7,
    Completed = 8,
    Cancelled = 9
}

public enum LearningTaskState
{
    Planned = 0,
    WaitingForApproval = 1,
    Ready = 2,
    Running = 3,
    QaReview = 4,
    HumanReview = 5,
    Completed = 6,
    Failed = 7,
    Cancelled = 8
}

public enum SourceTrustLevel
{
    Unknown = 0,
    LocalProjectSource = 1,
    InternalApprovedSource = 2,
    OfficialVendorDocumentation = 3,
    CommunitySource = 4,
    UntrustedSource = 5
}

public enum KnowledgeGapSeverity
{
    Unknown = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public sealed record EpistemicDriveScanRequest(
    Guid ProjectId,
    IReadOnlyList<Guid> ActiveProjectDirectionIds,
    DateTimeOffset EvidenceSinceUtc,
    bool IncludeCrossProjectSignals,
    bool AllowExternalSourceCandidates,
    IReadOnlyDictionary<string, string> Options);

public sealed record EpistemicDriveScanResult(
    Guid RunId,
    Guid ProjectId,
    IReadOnlyList<EpistemicTensionResult> TensionResults,
    IReadOnlyList<KnowledgeNeedProposal> Proposals,
    IReadOnlyList<string> Warnings,
    DateTimeOffset CompletedAtUtc);

public sealed record KnowledgeRegion(
    Guid Id,
    Guid? ProjectId,
    Guid? ParentRegionId,
    string TopicKey,
    string DisplayName,
    string Scope,
    IReadOnlyList<string> Tags,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record ProjectDirectionVector(
    Guid Id,
    Guid ProjectId,
    string DirectionKey,
    string DisplayName,
    double StrategicWeight,
    double RiskWeight,
    double TimeHorizonWeight,
    IReadOnlyList<Guid> SourceMemoryItemIds,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record KnowledgeNeedVector(
    double UsageFrequency,
    double ConfidenceWeakness,
    double RiskImpact,
    double Staleness,
    double FailureRecurrence,
    double StrategicAlignment,
    double QuestionDensity,
    double BusinessValue,
    double EstimatedLearningEffort,
    double SourceAvailability,
    double SourceQuality,
    double ContradictionPressure,
    double UserInterestSignal,
    double Volatility,
    double ExpectedReuse);

public sealed record LearningRoiEstimate(
    double ExpectedBenefit,
    double EstimatedEffort,
    double Confidence,
    string Explanation,
    IReadOnlyDictionary<string, string> Assumptions);

public sealed record KnowledgeGapEvidenceRef(
    string EvidenceKind,
    Guid EvidenceId,
    string Summary,
    double Weight,
    DateTimeOffset ObservedAtUtc,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record KnowledgeGapRecord(
    Guid Id,
    Guid ProjectId,
    Guid KnowledgeRegionId,
    string GapKey,
    string Title,
    string Description,
    KnowledgeGapSeverity Severity,
    double ConfidenceWeakness,
    double CoverageWeakness,
    IReadOnlyList<KnowledgeGapEvidenceRef> EvidenceRefs,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record KnowledgeCoverageSubregion(
    Guid KnowledgeRegionId,
    string DisplayName,
    double Coverage,
    double Confidence,
    double Staleness,
    double RiskImpact,
    int SourceRefCount,
    int OpenQuestionCount);

public sealed record KnowledgeCoverageMapRecord(
    Guid Id,
    Guid ProjectId,
    Guid RootKnowledgeRegionId,
    IReadOnlyList<KnowledgeCoverageSubregion> Subregions,
    IReadOnlyList<KnowledgeGapEvidenceRef> EvidenceRefs,
    string AlgorithmVersion,
    DateTimeOffset CalculatedAtUtc);

public sealed record EpistemicTensionResult(
    Guid Id,
    Guid ProjectId,
    Guid KnowledgeRegionId,
    KnowledgeNeedVector Vector,
    KnowledgeNeedCategory Category,
    int ParetoRank,
    double? DisplayPriorityScore,
    LearningRoiEstimate RoiEstimate,
    IReadOnlyList<Guid> IntersectingProjectDirectionIds,
    IReadOnlyList<KnowledgeGapEvidenceRef> EvidenceRefs,
    string Explanation,
    DateTimeOffset CalculatedAtUtc);

public sealed record SourceCandidateRef(
    Guid? SourceManifestId,
    Guid? SourceItemId,
    string SourceSystem,
    string Title,
    string Locator,
    SourceTrustLevel TrustLevel,
    bool RequiresApproval);

public sealed record ProbingQuestionSetRecord(
    Guid Id,
    Guid ProjectId,
    Guid KnowledgeRegionId,
    IReadOnlyList<string> Questions,
    IReadOnlyList<KnowledgeGapEvidenceRef> EvidenceRefs,
    string Purpose,
    DateTimeOffset CreatedAtUtc);

public sealed record OpenQuestionSetRecord(
    Guid Id,
    Guid ProjectId,
    Guid KnowledgeRegionId,
    IReadOnlyList<string> Questions,
    IReadOnlyList<KnowledgeGapEvidenceRef> EvidenceRefs,
    DateTimeOffset UpdatedAtUtc);

public sealed record KnowledgeNeedProposal(
    Guid Id,
    Guid ProjectId,
    Guid KnowledgeRegionId,
    string Topic,
    string Summary,
    KnowledgeNeedVector Vector,
    KnowledgeNeedCategory Category,
    LearningRoiEstimate RoiEstimate,
    IReadOnlyList<KnowledgeCoverageSubregion> CoverageMap,
    IReadOnlyList<KnowledgeGapEvidenceRef> EvidenceRefs,
    IReadOnlyList<Guid> RelatedProjectDirectionIds,
    IReadOnlyList<SourceCandidateRef> SuggestedSources,
    IReadOnlyList<string> SuggestedOutputs,
    IReadOnlyList<string> SuggestedAcceptanceCriteria,
    IReadOnlyList<string> SuggestedProbingQuestions,
    string ProposedDepth,
    string RiskSummary,
    bool RequiresHumanApproval,
    LearningProposalState State,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record LearningTaskRecord(
    Guid Id,
    Guid ProjectId,
    Guid ProposalId,
    string Title,
    string Scope,
    IReadOnlyList<SourceCandidateRef> ApprovedSources,
    IReadOnlyList<string> ExpectedOutputs,
    IReadOnlyList<string> AcceptanceCriteria,
    string AssignedTo,
    LearningTaskState State,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc);

public sealed record LearningOutcomeRecord(
    Guid Id,
    Guid ProjectId,
    Guid LearningTaskId,
    string Summary,
    IReadOnlyList<Guid> DraftMemoryItemIds,
    IReadOnlyList<Guid> DraftProcedureItemIds,
    IReadOnlyList<Guid> ProbingQuestionSetIds,
    IReadOnlyList<Guid> ValidationProbeSessionIds,
    IReadOnlyList<KnowledgeGapEvidenceRef> SourceEvidenceRefs,
    IReadOnlyList<string> QaFindings,
    MemoryValidationState ValidationState,
    DateTimeOffset CreatedAtUtc);

public sealed record KnowledgeGapDetectionRequest(
    Guid ProjectId,
    IReadOnlyList<Guid> KnowledgeRegionIds,
    DateTimeOffset EvidenceSinceUtc,
    IReadOnlyDictionary<string, string> Options);

public sealed record LearningProposalDecisionRequest(
    Guid ProposalId,
    string Decision,
    string? Reason,
    string? ScopeOverride,
    IReadOnlyList<SourceCandidateRef> AddedSources,
    DateTimeOffset? SnoozedUntilUtc);

public interface IKnowledgeGapDetector
{
    Task<IReadOnlyList<KnowledgeGapRecord>> DetectAsync(
        KnowledgeGapDetectionRequest request,
        CancellationToken cancellationToken = default);
}

public interface IEpistemicDriveEngine
{
    Task<EpistemicDriveScanResult> ScanAsync(
        EpistemicDriveScanRequest request,
        CancellationToken cancellationToken = default);
}

public interface ILearningProposalService
{
    Task<IReadOnlyList<KnowledgeNeedProposal>> CreateProposalsAsync(
        EpistemicDriveScanResult scanResult,
        CancellationToken cancellationToken = default);

    Task<KnowledgeNeedProposal> DecideAsync(
        LearningProposalDecisionRequest request,
        CancellationToken cancellationToken = default);
}

public interface ILearningTaskPlanner
{
    Task<LearningTaskRecord> PlanAsync(
        KnowledgeNeedProposal proposal,
        CancellationToken cancellationToken = default);
}

public interface IKnowledgeCoverageService
{
    Task<KnowledgeCoverageMapRecord> RefreshCoverageMapAsync(
        Guid projectId,
        Guid rootKnowledgeRegionId,
        CancellationToken cancellationToken = default);

    Task<KnowledgeCoverageMapRecord?> GetCoverageMapAsync(
        Guid projectId,
        Guid rootKnowledgeRegionId,
        CancellationToken cancellationToken = default);
}

public sealed record EpistemicProbeRequest(
    Guid ProjectId,
    Guid KnowledgeRegionId,
    IReadOnlyList<Guid> ActiveProjectDirectionIds,
    MemoryProbeSessionMode PreferredMode,
    int QuestionLimit,
    string Purpose,
    IReadOnlyDictionary<string, string> Options);

public interface IEpistemicProbePlanner
{
    Task<IReadOnlyList<MemoryProbeQuestion>> PlanProbeQuestionsAsync(
        EpistemicProbeRequest request,
        CancellationToken cancellationToken = default);
}
