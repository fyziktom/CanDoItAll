using CanDoItAll.Processes.Abstractions;

namespace CanDoItAll.Processes.Projections;

public sealed record ProcessLiveProcessesQuery(
    DateTimeOffset NowUtc,
    TimeSpan Window,
    int Take);

public sealed record ProcessLiveProcessesResult(
    IReadOnlyList<ProcessLiveProcessSnapshot> Runs,
    ProcessProjectionFreshness? Freshness);

public sealed record ProcessRunHistoryQuery(
    ProcessRunId? RunId,
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    int Take,
    int Skip = 0);

public sealed record ProcessRunHistoryResult(
    IReadOnlyList<ProcessTimelineEventProjection> Events,
    ProcessProjectionFreshness? Freshness,
    bool HasMoreEvents = false);

public sealed record ProcessRunDetailQuery(ProcessRunId RunId);

public sealed record ProcessRuntimeWorkspaceQuery(
    DateTimeOffset NowUtc,
    TimeSpan Window,
    int EventPage,
    int EventPageSize,
    int TakeRuns,
    ProcessRunId? SelectedRunId);

public sealed record ProcessRuntimeWorkspaceResult(
    IReadOnlyList<ProcessLiveProcessSnapshot> Runs,
    ProcessRunDetailProjection? SelectedRun,
    IReadOnlyList<ProcessTimelineEventProjection> Events,
    bool HasMoreEvents,
    IReadOnlyList<ProcessRuntimeActiveAgentProjection> ActiveAgents,
    ProcessProjectionFreshness? Freshness);

public sealed record ProcessProjectionHistoryQuery(
    ProcessProjectorName ProjectorName,
    ProcessRunId? RunId,
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    int Take,
    long? AfterGlobalSequence = null,
    int Skip = 0);
