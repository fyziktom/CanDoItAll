using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.Modules.CognitiveMemory;

public enum CognitiveMemoryAutomationTriggerKind
{
    Manual = 0,
    Nightly = 1,
    IdleTimeout = 2,
    ScheduledMoment = 3
}

public enum CognitiveMemoryRetentionCleanupScope
{
    RecallTraces = 0,
    ConsolidationCandidates = 1,
    ProbeSessions = 2,
    DistributedJobs = 3
}

public sealed class CognitiveMemoryProjectionOptions
{
    public bool Enabled { get; set; }

    public string CollectionName { get; set; } = string.Empty;

    public string ProjectionProfileId { get; set; } = string.Empty;

    public string EmbeddingProfileId { get; set; } = string.Empty;

    public string TargetProviderName { get; set; } = string.Empty;

    public CognitiveMemoryProjectionStoreKind ProjectionStoreKind { get; set; } = CognitiveMemoryProjectionStoreKind.GenericRag;

    public int? VectorDimensions { get; set; }

    public bool CanProjectMissingRecords
        => Enabled &&
           !string.IsNullOrWhiteSpace(CollectionName) &&
           !string.IsNullOrWhiteSpace(ProjectionProfileId) &&
           !string.IsNullOrWhiteSpace(EmbeddingProfileId) &&
           !string.IsNullOrWhiteSpace(TargetProviderName);
}

public sealed record CognitiveMemoryProjectionRebuildRequest(
    Guid? ProjectId,
    int Take,
    string ActorId,
    CognitiveMemoryProjectionCollectionName? CollectionName = null,
    bool ProjectMissingRecords = false,
    CognitiveMemoryProjectionProfileId? ProjectionProfileId = null,
    CognitiveMemoryEmbeddingProfileId? EmbeddingProfileId = null,
    string? TargetProviderName = null,
    CognitiveMemoryProjectionStoreKind? ProjectionStoreKind = null,
    int? ExpectedVectorDimensions = null);

public sealed record CognitiveMemoryProjectionRebuildItemResult(
    Guid ProjectionRecordId,
    Guid MemoryRecordId,
    CognitiveMemoryProjectionLifecycleDecisionKind DecisionKind,
    CognitiveMemoryProjectionStatus Status,
    string ProviderTrace,
    string? FailureMessage);

public sealed record CognitiveMemoryProjectionRebuildResult(
    Guid RunId,
    CognitiveMemoryRunStatus Status,
    int SelectedCount,
    int ProjectedCount,
    int FailedCount,
    int SkippedCount,
    IReadOnlyList<CognitiveMemoryProjectionRebuildItemResult> Items,
    IReadOnlyList<string> Warnings);

public sealed record CognitiveMemoryScheduledAutomationRunRequest(
    Guid? ProjectId,
    CognitiveMemoryAutomationTriggerKind TriggerKind,
    string ActorId,
    int Take = 50,
    string? CycleId = null,
    int MaxCycles = 1,
    bool ContinueUntilIdle = false,
    CognitiveMemoryPolicyContext? PolicyContext = null);

public sealed record CognitiveMemoryScheduledAutomationCycleResult(
    int Sequence,
    string CycleId,
    CognitiveMemoryConsolidationRunId? ConsolidationRunId,
    CognitiveMemoryRunStatus? Status,
    int SourceItemsScanned,
    int CandidatesCreated,
    string? Cursor,
    string? NextCursor,
    IReadOnlyList<string> Warnings);

public sealed record CognitiveMemoryScheduledAutomationRunResult(
    CognitiveMemoryAutomationScheduleMode ScheduleMode,
    CognitiveMemoryAutomationTriggerKind TriggerKind,
    bool Executed,
    int SourceIngestionRuns,
    int SourceItemsSeen,
    int SourceItemsCreated,
    int ConsolidationRuns,
    CognitiveMemoryRunStatus? ConsolidationStatus,
    IReadOnlyList<string> Warnings,
    string CycleId,
    int CyclesExecuted,
    string? FinalCursor,
    IReadOnlyList<CognitiveMemoryScheduledAutomationCycleResult> Cycles);

public sealed record CognitiveMemoryRetentionCleanupRequest(
    Guid? ProjectId,
    DateTimeOffset DeleteBeforeUtc,
    bool DryRun,
    IReadOnlyList<CognitiveMemoryRetentionCleanupScope> Scopes,
    string ActorId)
{
    public static readonly IReadOnlyList<CognitiveMemoryRetentionCleanupScope> DefaultScopes =
    [
        CognitiveMemoryRetentionCleanupScope.RecallTraces,
        CognitiveMemoryRetentionCleanupScope.ConsolidationCandidates,
        CognitiveMemoryRetentionCleanupScope.ProbeSessions,
        CognitiveMemoryRetentionCleanupScope.DistributedJobs
    ];
}

public sealed record CognitiveMemoryRetentionCleanupScopeResult(
    CognitiveMemoryRetentionCleanupScope Scope,
    int MatchedRootRecords,
    int DeletedRecords,
    string Notes);

public sealed record CognitiveMemoryRetentionCleanupResult(
    Guid? ProjectId,
    DateTimeOffset DeleteBeforeUtc,
    bool DryRun,
    string ActorId,
    IReadOnlyList<CognitiveMemoryRetentionCleanupScopeResult> Scopes)
{
    public int TotalMatchedRootRecords => Scopes.Sum(scope => scope.MatchedRootRecords);

    public int TotalDeletedRecords => Scopes.Sum(scope => scope.DeletedRecords);
}

public interface ICognitiveMemoryProjectionRebuildService
{
    ValueTask<CognitiveMemoryProjectionRebuildResult> RebuildAsync(
        CognitiveMemoryProjectionRebuildRequest request,
        CancellationToken cancellationToken = default);
}

public interface ICognitiveMemoryScheduledAutomationRunner
{
    ValueTask<CognitiveMemoryScheduledAutomationRunResult> RunAsync(
        CognitiveMemoryScheduledAutomationRunRequest request,
        CancellationToken cancellationToken = default);
}

public interface ICognitiveMemoryRetentionCleanupService
{
    ValueTask<CognitiveMemoryRetentionCleanupResult> CleanupAsync(
        CognitiveMemoryRetentionCleanupRequest request,
        CancellationToken cancellationToken = default);
}
