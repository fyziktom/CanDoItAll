using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.Modules.CognitiveMemory;

public enum CognitiveMemoryAutomationTriggerKind
{
    Manual = 0,
    Nightly = 1,
    IdleTimeout = 2,
    ScheduledMoment = 3
}

public sealed record CognitiveMemoryProjectionRebuildRequest(
    Guid? ProjectId,
    int Take,
    string ActorId,
    CognitiveMemoryProjectionCollectionName? CollectionName = null);

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
    int Take = 50);

public sealed record CognitiveMemoryScheduledAutomationRunResult(
    CognitiveMemoryAutomationScheduleMode ScheduleMode,
    CognitiveMemoryAutomationTriggerKind TriggerKind,
    bool Executed,
    int SourceIngestionRuns,
    int SourceItemsSeen,
    int SourceItemsCreated,
    int ConsolidationRuns,
    CognitiveMemoryRunStatus? ConsolidationStatus,
    IReadOnlyList<string> Warnings);

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
