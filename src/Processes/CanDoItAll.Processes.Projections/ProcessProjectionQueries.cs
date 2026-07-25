using CanDoItAll.Processes.Abstractions;

namespace CanDoItAll.Processes.Projections;

public sealed record ProcessLiveProcessesQuery(
    DateTimeOffset NowUtc,
    TimeSpan Window,
    int Take,
    ProcessLiveProcessesLoadOptions? LoadOptions = null);

public sealed record ProcessLiveProcessesLoadOptions
{
    public static ProcessLiveProcessesLoadOptions Full { get; } = new();

    public static ProcessLiveProcessesLoadOptions SnapshotOnly { get; } = new()
    {
        IncludeAttentionReconciliation = false,
        IncludeOperatorActions = false,
        IncludeCurrentSteps = false,
        IncludeChildRunWaits = false,
        IncludeDiagnostics = false
    };

    public bool IncludeAttentionReconciliation { get; init; } = true;

    public bool IncludeOperatorActions { get; init; } = true;

    public bool IncludeCurrentSteps { get; init; } = true;

    public bool IncludeChildRunWaits { get; init; } = true;

    public bool IncludeDiagnostics { get; init; } = true;
}

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
    ProcessRunId? SelectedRunId,
    bool AutoSelectRun = true,
    ProcessRuntimeWorkspaceLoadOptions? LoadOptions = null);

public sealed record ProcessRuntimeWorkspaceLoadOptions
{
    public static ProcessRuntimeWorkspaceLoadOptions Full { get; } = new();

    public static ProcessRuntimeWorkspaceLoadOptions ListOnly { get; } = new()
    {
        LiveProcesses = ProcessLiveProcessesLoadOptions.SnapshotOnly,
        IncludeSelectedRun = false,
        IncludeRunRecord = false,
        IncludeHistory = false,
        IncludeMetricHistory = false,
        IncludeActiveAgents = false,
        IncludeUsageTelemetry = false
    };

    public ProcessLiveProcessesLoadOptions LiveProcesses { get; init; } = ProcessLiveProcessesLoadOptions.Full;

    public bool IncludeSelectedRun { get; init; } = true;

    public bool IncludeRunRecord { get; init; } = true;

    public bool IncludeHistory { get; init; } = true;

    public bool IncludeMetricHistory { get; init; } = true;

    public bool IncludeActiveAgents { get; init; } = true;

    public bool IncludeUsageTelemetry { get; init; } = true;
}

public sealed record ProcessRuntimeWorkspaceResult(
    IReadOnlyList<ProcessLiveProcessSnapshot> Runs,
    ProcessRunDetailProjection? SelectedRun,
    IReadOnlyList<ProcessTimelineEventProjection> Events,
    IReadOnlyList<ProcessTimelineEventProjection> MetricEvents,
    bool HasMoreEvents,
    IReadOnlyList<ProcessRuntimeActiveAgentProjection> ActiveAgents,
    ProcessProjectionFreshness? Freshness)
{
    public ProcessRunRecord? SelectedRunRecord { get; init; }
}

public sealed record ProcessProjectionHistoryQuery(
    ProcessProjectorName ProjectorName,
    ProcessRunId? RunId,
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    int Take,
    long? AfterGlobalSequence = null,
    int Skip = 0);
