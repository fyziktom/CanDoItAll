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
    int Take);

public sealed record ProcessRunHistoryResult(
    IReadOnlyList<ProcessTimelineEventProjection> Events,
    ProcessProjectionFreshness? Freshness);

public sealed record ProcessRunDetailQuery(ProcessRunId RunId);

public sealed record ProcessProjectionHistoryQuery(
    ProcessProjectorName ProjectorName,
    ProcessRunId? RunId,
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    int Take,
    long? AfterGlobalSequence = null);
