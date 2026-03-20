namespace Zyphonote.App.PdmxTool.Data;

public sealed class ScoreGroupingProfile
{
    public int IndexedScoreId { get; set; }

    public IndexedScore IndexedScore { get; set; } = null!;

    public int PipelineVersion { get; set; }

    public int NormalizationVersion { get; set; }

    public string NormalizedTitleLoose { get; set; } = string.Empty;

    public string NormalizedTitleStrict { get; set; } = string.Empty;

    public string NormalizedComposerLoose { get; set; } = string.Empty;

    public string NormalizedComposerStrict { get; set; } = string.Empty;

    public string? ComposerSurnameKey { get; set; }

    public string? ComposerForenameKey { get; set; }

    public string? CatalogTokensCsv { get; set; }

    public string? PrimaryCatalogSystem { get; set; }

    public string? PrimaryCatalogValue { get; set; }

    public string? OpusNumber { get; set; }

    public string? WorkNumber { get; set; }

    public string? MovementNumber { get; set; }

    public string? MovementLabel { get; set; }

    public string? KeySignatureKey { get; set; }

    public string? WorkTypeKey { get; set; }

    public string WorkSignatureLoose { get; set; } = string.Empty;

    public string WorkSignatureStrict { get; set; } = string.Empty;

    public string? AliasTitlesJson { get; set; }

    public string? AliasComposersJson { get; set; }

    public string? EmbeddingInputText { get; set; }

    public DateTime UpdatedUtc { get; set; }
}

public sealed class ScoreEmbeddingVector
{
    public int Id { get; set; }

    public int IndexedScoreId { get; set; }

    public IndexedScore IndexedScore { get; set; } = null!;

    public GroupingEmbeddingKind EmbeddingKind { get; set; }

    public string ModelName { get; set; } = string.Empty;

    public int VectorDimensions { get; set; }

    public string InputHash { get; set; } = string.Empty;

    public byte[] VectorBlob { get; set; } = Array.Empty<byte>();

    public string? QuantizationKind { get; set; }

    public DateTime UpdatedUtc { get; set; }
}

public sealed class SongGroupMembership
{
    public int Id { get; set; }

    public int IndexedScoreId { get; set; }

    public IndexedScore IndexedScore { get; set; } = null!;

    public int SongGroupId { get; set; }

    public SongGroup SongGroup { get; set; } = null!;

    public SongGroupMembershipRole MembershipRole { get; set; }

    public SongGroupMembershipSource MembershipSource { get; set; }

    public double? ConfidenceScore { get; set; }

    public GroupingConfidenceBand ConfidenceBand { get; set; }

    public string? ReasonSummary { get; set; }

    public string? ReasonJson { get; set; }

    public bool IsLocked { get; set; }

    public bool IsHidden { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime UpdatedUtc { get; set; }
}

public sealed class SongGroupingRun
{
    public int Id { get; set; }

    public SongGroupingRunKind RunKind { get; set; }

    public SongGroupingRunStatus Status { get; set; }

    public string? ScopeDescription { get; set; }

    public string? RequestedBy { get; set; }

    public DateTime RequestedUtc { get; set; }

    public DateTime? CompletedUtc { get; set; }

    public int NormalizationVersion { get; set; }

    public string? EmbeddingModel { get; set; }

    public string? ThresholdProfile { get; set; }

    public int ScoreCount { get; set; }

    public int CandidatePairCount { get; set; }

    public int ProposedGroupCount { get; set; }

    public int AppliedGroupCount { get; set; }

    public int AutoAcceptedCount { get; set; }

    public int ReviewRequiredCount { get; set; }

    public int RejectedCount { get; set; }

    public string? StatsJson { get; set; }

    public string? Error { get; set; }

    public string? CursorJson { get; set; }

    public List<SongGroupingRunGroup> ProposedGroups { get; set; } = [];
}

public sealed class SongGroupingRunGroup
{
    public int Id { get; set; }

    public int RunId { get; set; }

    public SongGroupingRun Run { get; set; } = null!;

    public string ProposedGroupKey { get; set; } = string.Empty;

    public SongGroupType GroupType { get; set; }

    public string? DisplayTitle { get; set; }

    public string? DisplayComposer { get; set; }

    public int MemberCount { get; set; }

    public string? ConfidenceSummary { get; set; }

    public GroupReviewState ReviewState { get; set; }

    public string? SummaryJson { get; set; }

    public List<SongGroupingRunMember> Members { get; set; } = [];
}

public sealed class SongGroupingRunMember
{
    public int Id { get; set; }

    public int RunGroupId { get; set; }

    public SongGroupingRunGroup RunGroup { get; set; } = null!;

    public int IndexedScoreId { get; set; }

    public bool IsPrimaryCandidate { get; set; }

    public double? ConfidenceScore { get; set; }

    public GroupingConfidenceBand ConfidenceBand { get; set; }

    public string? ReasonSummary { get; set; }

    public string? ReasonJson { get; set; }

    public GroupingProposalDisposition Disposition { get; set; }
}

public enum GroupingEmbeddingKind
{
    Work,
    WorkNoComposer,
    DescriptionAux
}

public enum SongGroupType
{
    ExactWork,
    WorkFamily,
    Arrangement,
    Excerpt
}

public enum SongGroupMembershipRole
{
    Primary,
    Secondary,
    Related
}

public enum SongGroupMembershipSource
{
    Manual,
    Auto,
    RunApply,
    Imported
}

public enum GroupingConfidenceBand
{
    Rejected,
    Low,
    Review,
    High,
    Definite
}

public enum GroupReviewState
{
    AutoDraft,
    NeedsReview,
    Curated,
    Locked,
    Deprecated
}

public enum SongGroupingRunKind
{
    ProfileRefresh,
    EmbeddingRefresh,
    DryRun,
    Apply
}

public enum SongGroupingRunStatus
{
    Queued,
    Running,
    Completed,
    Failed,
    Cancelled
}

public enum GroupingProposalDisposition
{
    AutoAccepted,
    NeedsReview,
    Rejected
}

public enum GroupingLockMode
{
    None,
    ProtectManual,
    DoNotAutoAssign
}
