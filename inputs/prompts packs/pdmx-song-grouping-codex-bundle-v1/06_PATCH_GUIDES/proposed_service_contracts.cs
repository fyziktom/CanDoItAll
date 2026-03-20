namespace Zyphonote.App.PdmxTool.Services.Grouping;

public sealed record GroupingProfileBuildResult(
    int ProcessedCount,
    int CreatedCount,
    int UpdatedCount,
    int SkippedCount);

public sealed record GroupingEmbeddingBuildResult(
    int ProcessedCount,
    int CreatedCount,
    int UpdatedCount,
    int SkippedCount,
    string ModelName);

public sealed record GroupingRunRequest(
    GroupingRunMode Mode,
    IReadOnlyList<int>? ScoreIds = null,
    bool OnlyUngrouped = false,
    bool OnlyChangedProfiles = false,
    bool OnlyMissingEmbeddings = false,
    bool ForceRebuild = false,
    bool DryRun = true,
    int? RunIdToApply = null,
    string? ThresholdProfile = null,
    string? EmbeddingModelName = null,
    int? MaxScores = null,
    string? ScopeDescription = null);

public sealed record GroupingPairEvidence(
    int LeftIndexedScoreId,
    int RightIndexedScoreId,
    double CompositeScore,
    GroupingConfidenceBand ConfidenceBand,
    string ReasonSummary,
    string ReasonJson);

public sealed record GroupingClusterProposal(
    string ProposedGroupKey,
    SongGroupType GroupType,
    string? DisplayTitle,
    string? DisplayComposer,
    IReadOnlyList<GroupingClusterMember> Members,
    string? ConfidenceSummary);

public sealed record GroupingClusterMember(
    int IndexedScoreId,
    bool IsPrimaryCandidate,
    double? ConfidenceScore,
    GroupingConfidenceBand ConfidenceBand,
    string? ReasonSummary,
    string? ReasonJson);

public interface IScoreGroupingProfileService
{
    Task<GroupingProfileBuildResult> BuildAsync(
        IReadOnlyList<int>? scoreIds,
        bool onlyChangedProfiles,
        bool forceRebuild,
        CancellationToken cancellationToken = default);
}

public interface IScoreGroupingEmbeddingService
{
    Task<GroupingEmbeddingBuildResult> BuildAsync(
        IReadOnlyList<int>? scoreIds,
        bool onlyMissingEmbeddings,
        bool forceRebuild,
        string modelName,
        CancellationToken cancellationToken = default);
}

public interface ISongGroupingRunService
{
    Task<int> StartAsync(GroupingRunRequest request, CancellationToken cancellationToken = default);

    Task ApplyAsync(int runId, CancellationToken cancellationToken = default);
}

public interface ISongGroupingCandidateService
{
    Task<IReadOnlyList<GroupingPairEvidence>> BuildCandidateEvidenceAsync(
        IReadOnlyList<int> scoreIds,
        string thresholdProfile,
        string? embeddingModelName,
        CancellationToken cancellationToken = default);
}

public interface ISongGroupingScoringService
{
    GroupingPairEvidence Score(GroupingScoringInput input);
}

public interface ISongGroupAdminService
{
    Task<int> CreateGroupAsync(CreateSongGroupCommand command, CancellationToken cancellationToken = default);

    Task AddMembershipAsync(AddSongGroupMembershipCommand command, CancellationToken cancellationToken = default);

    Task RemoveMembershipAsync(RemoveSongGroupMembershipCommand command, CancellationToken cancellationToken = default);

    Task MergeGroupsAsync(MergeSongGroupsCommand command, CancellationToken cancellationToken = default);

    Task SplitGroupAsync(SplitSongGroupCommand command, CancellationToken cancellationToken = default);
}

public enum GroupingRunMode
{
    BuildProfilesOnly,
    BuildMissingEmbeddings,
    DryRunGenerate,
    ApplyRun,
    RefreshScoreIds
}

public sealed record CreateSongGroupCommand(
    string GroupKey,
    SongGroupType GroupType,
    string? DisplayTitle,
    string? DisplayComposer,
    int? CanonicalIndexedScoreId,
    string? Notes);

public sealed record AddSongGroupMembershipCommand(
    int IndexedScoreId,
    int SongGroupId,
    SongGroupMembershipRole MembershipRole,
    bool IsLocked,
    string? ReasonSummary,
    string? ReasonJson);

public sealed record RemoveSongGroupMembershipCommand(
    int IndexedScoreId,
    int SongGroupId,
    bool CreateStickyProtectionRule,
    string? CuratorNote);

public sealed record MergeSongGroupsCommand(
    int TargetSongGroupId,
    IReadOnlyList<int> SourceSongGroupIds,
    bool PreserveTargetCanonicalValues);

public sealed record SplitSongGroupCommand(
    int SourceSongGroupId,
    IReadOnlyList<int> IndexedScoreIds,
    string NewGroupKey,
    SongGroupType NewGroupType,
    string? NewDisplayTitle,
    string? NewDisplayComposer);

public sealed record GroupingScoringInput(
    ScoreGroupingProfile LeftProfile,
    ScoreGroupingProfile RightProfile,
    ReadOnlyMemory<float>? LeftEmbedding,
    ReadOnlyMemory<float>? RightEmbedding);
