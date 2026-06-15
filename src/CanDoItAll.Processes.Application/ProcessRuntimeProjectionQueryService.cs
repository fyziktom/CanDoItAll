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
                    query.Take),
                cancellationToken)
            .ConfigureAwait(false);
        var events = new List<ProcessTimelineEventProjection>(records.Count);

        foreach (var record in records)
        {
            events.Add(jsonCodec.ReadHistory<ProcessTimelineEventProjection>(record));
        }

        return new ProcessRunHistoryResult(events, CombineFreshness(events));
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
}
