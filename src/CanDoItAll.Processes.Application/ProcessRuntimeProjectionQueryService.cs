using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Projections;

namespace CanDoItAll.Processes.Application;

public sealed class ProcessRuntimeProjectionQueryService(
    IProcessProjectionStore projectionStore,
    ProcessProjectionJsonCodec jsonCodec,
    IProcessProjectionClock clock)
{
    private const int LiveSnapshotReadLimit = 500;

    public async Task<ProcessLiveProcessesResult> GetLiveProcessesAsync(
        ProcessLiveProcessesQuery query,
        CancellationToken cancellationToken = default)
    {
        ValidateTake(query.Take, nameof(query.Take));
        if (query.Window <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(query.Window), query.Window, "Live process window must be positive.");
        }

        var nowUtc = query.NowUtc == default ? clock.GetUtcNow() : query.NowUtc;
        var windowStartUtc = nowUtc - query.Window;
        var snapshots = await projectionStore
            .ReadSnapshotsAsync(ProcessRuntimeProjectionProjector.ProjectorName, ProcessRuntimeProjectionKeys.LivePrefix, LiveSnapshotReadLimit, cancellationToken)
            .ConfigureAwait(false);
        var runs = new List<ProcessLiveProcessSnapshot>();

        foreach (var snapshot in snapshots)
        {
            var run = jsonCodec.ReadSnapshot<ProcessLiveProcessSnapshot>(snapshot);
            if (!run.IsActive && run.LastEventAtUtc < windowStartUtc)
            {
                continue;
            }

            runs.Add(run);
        }

        runs.Sort(static (left, right) =>
        {
            var lastEventComparison = right.LastEventAtUtc.CompareTo(left.LastEventAtUtc);
            return lastEventComparison != 0
                ? lastEventComparison
                : string.CompareOrdinal(left.RunId.ToString(), right.RunId.ToString());
        });

        if (runs.Count > query.Take)
        {
            runs.RemoveRange(query.Take, runs.Count - query.Take);
        }

        return new ProcessLiveProcessesResult(runs, CombineFreshness(runs));
    }

    public async Task<ProcessRunHistoryResult> GetRunHistoryAsync(
        ProcessRunHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        ValidateTake(query.Take, nameof(query.Take));
        if (query.Skip < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(query.Skip), query.Skip, "Projection history skip count cannot be negative.");
        }

        if (query.FromUtc >= query.ToUtc)
        {
            throw new ArgumentException("History query range must have FromUtc earlier than ToUtc.", nameof(query));
        }

        var records = await projectionStore
            .ReadHistoryAsync(
                new ProcessProjectionHistoryQuery(
                    ProcessRuntimeProjectionProjector.ProjectorName,
                    query.RunId,
                    query.FromUtc,
                    query.ToUtc,
                    query.Take,
                    Skip: query.Skip),
                cancellationToken)
            .ConfigureAwait(false);
        var events = new List<ProcessTimelineEventProjection>(records.Count);

        foreach (var record in records)
        {
            events.Add(jsonCodec.ReadHistory<ProcessTimelineEventProjection>(record));
        }

        return new ProcessRunHistoryResult(events, CombineFreshness(events));
    }

    public async Task<ProcessRuntimeWorkspaceResult> GetRuntimeWorkspaceAsync(
        ProcessRuntimeWorkspaceQuery query,
        CancellationToken cancellationToken = default)
    {
        ValidateTake(query.TakeRuns, nameof(query.TakeRuns));
        ValidateTake(query.EventPageSize, nameof(query.EventPageSize));
        if (query.EventPage < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(query.EventPage), query.EventPage, "Runtime event page cannot be negative.");
        }

        if (query.Window <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(query.Window), query.Window, "Runtime workspace window must be positive.");
        }

        var nowUtc = query.NowUtc == default ? clock.GetUtcNow() : query.NowUtc;
        var liveProcesses = await GetLiveProcessesAsync(
            new ProcessLiveProcessesQuery(nowUtc, query.Window, query.TakeRuns),
            cancellationToken).ConfigureAwait(false);
        var selectedRunId = ResolveSelectedRunId(liveProcesses.Runs, query.SelectedRunId);
        ProcessRunDetailProjection? selectedRun = null;
        if (selectedRunId is not null)
        {
            selectedRun = await GetRunDetailAsync(new ProcessRunDetailQuery(selectedRunId.Value), cancellationToken)
                .ConfigureAwait(false);
        }

        var history = await GetRunHistoryAsync(
            new ProcessRunHistoryQuery(
                selectedRunId,
                nowUtc - query.Window,
                nowUtc,
                Take: query.EventPageSize + 1,
                Skip: checked(query.EventPage * query.EventPageSize)),
            cancellationToken).ConfigureAwait(false);
        var events = history.Events.Take(query.EventPageSize).ToArray();
        var freshness = CombineFreshness(liveProcesses.Freshness, history.Freshness, selectedRun?.Freshness);

        return new ProcessRuntimeWorkspaceResult(
            liveProcesses.Runs,
            selectedRun,
            events,
            history.Events.Count > query.EventPageSize,
            freshness);
    }

    public async Task<ProcessRunDetailProjection?> GetRunDetailAsync(
        ProcessRunDetailQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var snapshot = await projectionStore
            .LoadSnapshotAsync(
                ProcessRuntimeProjectionProjector.ProjectorName,
                ProcessRuntimeProjectionKeys.RunDetail(query.RunId),
                cancellationToken)
            .ConfigureAwait(false);

        return snapshot is null
            ? null
            : jsonCodec.ReadSnapshot<ProcessRunDetailProjection>(snapshot);
    }

    private static void ValidateTake(int take, string parameterName)
    {
        if (take <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, take, "Projection query size must be positive.");
        }
    }

    private static ProcessRunId? ResolveSelectedRunId(
        IReadOnlyList<ProcessLiveProcessSnapshot> runs,
        ProcessRunId? requestedRunId)
    {
        if (requestedRunId is not null && runs.Any(run => run.RunId == requestedRunId))
        {
            return requestedRunId;
        }

        return runs
            .OrderByDescending(run => run.Status == ProcessProjectedRunStatus.NeedsAttention)
            .ThenByDescending(run => run.IsActive)
            .ThenByDescending(run => run.LastEventAtUtc)
            .Select(run => (ProcessRunId?)run.RunId)
            .FirstOrDefault();
    }

    private static ProcessProjectionFreshness? CombineFreshness(IReadOnlyList<ProcessLiveProcessSnapshot> runs)
    {
        if (runs.Count == 0)
        {
            return null;
        }

        var latest = runs[0].Freshness;
        for (var index = 1; index < runs.Count; index++)
        {
            if (runs[index].Freshness.SourceGlobalSequence > latest.SourceGlobalSequence)
            {
                latest = runs[index].Freshness;
            }
        }

        return latest;
    }

    private static ProcessProjectionFreshness? CombineFreshness(IReadOnlyList<ProcessTimelineEventProjection> events)
    {
        if (events.Count == 0)
        {
            return null;
        }

        var latestEvent = events[^1];
        return new ProcessProjectionFreshness(
            latestEvent.OccurredAtUtc,
            latestEvent.GlobalSequence,
            new ProcessProjectionLag(latestEvent.GlobalSequence, latestEvent.GlobalSequence, 0));
    }

    private static ProcessProjectionFreshness? CombineFreshness(params ProcessProjectionFreshness?[] freshnessValues)
    {
        ProcessProjectionFreshness? latest = null;
        foreach (var freshness in freshnessValues)
        {
            if (freshness is null)
            {
                continue;
            }

            if (latest is null || freshness.SourceGlobalSequence > latest.SourceGlobalSequence)
            {
                latest = freshness;
            }
        }

        return latest;
    }
}
