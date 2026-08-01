using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public enum ProcessDashboardActivityMode
{
    Active,
    RecentFallback
}

public sealed record ProcessDashboardActivityQuery
{
    public const int DefaultTake = ProcessRuntimeActivityQuery.DefaultTake;
    public const int MaximumTake = ProcessRuntimeActivityQuery.MaximumTake;

    public ProcessDashboardActivityQuery(int take = DefaultTake)
    {
        if (take is < 1 or > MaximumTake)
        {
            throw new ArgumentOutOfRangeException(
                nameof(take),
                take,
                $"Process dashboard activity take must be between 1 and {MaximumTake}.");
        }

        Take = take;
    }

    public int Take { get; }
}

public sealed record ProcessDashboardProjectionDetails(
    Guid? ProjectId,
    string ProjectName,
    string ProcessName,
    ProcessProjectedRunStatus Status,
    DateTimeOffset LastEventAtUtc,
    ProcessProjectionFreshness Freshness);

public sealed record ProcessDashboardActivityItem(
    ProcessRunId RootRunId,
    ProcessRunId RunId,
    ProcessRuntimeStatus Status,
    DateTimeOffset UpdatedAtUtc,
    ProcessDashboardProjectionDetails? Projection);

public sealed record ProcessDashboardActivityResult(
    ProcessDashboardActivityMode Mode,
    IReadOnlyList<ProcessDashboardActivityItem> Items);

public interface IProcessDashboardActivityQueryService
{
    Task<ProcessDashboardActivityResult> QueryAsync(
        ProcessDashboardActivityQuery query,
        CancellationToken cancellationToken = default);
}

public sealed class ProcessDashboardActivityQueryService(
    IProcessRuntimeActivityStore runtimeActivityStore,
    IProcessProjectionStore projectionStore,
    ProcessProjectionJsonCodec jsonCodec,
    IProcessRunRecordStore runRecordStore) : IProcessDashboardActivityQueryService
{
    public async Task<ProcessDashboardActivityResult> QueryAsync(
        ProcessDashboardActivityQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var selection = await runtimeActivityStore
            .QueryActivityAsync(new ProcessRuntimeActivityQuery(query.Take), cancellationToken)
            .ConfigureAwait(false);
        var projectionKeys = selection.Runs
            .Select(run => ProcessRuntimeProjectionKeys.Live(run.RunId))
            .ToArray();
        var projectionsByKey = (await projectionStore
                .LoadSnapshotsAsync(
                    ProcessRuntimeProjectionProjector.ProjectorName,
                    projectionKeys,
                    cancellationToken)
                .ConfigureAwait(false))
            .ToDictionary(snapshot => snapshot.ProjectionKey);
        var recordSummariesByRunId = await LoadRecordSummariesAsync(
            selection,
            cancellationToken).ConfigureAwait(false);
        var items = new List<ProcessDashboardActivityItem>(selection.Runs.Count);
        foreach (var run in selection.Runs)
        {
            var projectionKey = ProcessRuntimeProjectionKeys.Live(run.RunId);
            var projection = projectionsByKey.TryGetValue(projectionKey, out var snapshot)
                ? MapProjection(
                    run,
                    snapshot,
                    recordSummariesByRunId.GetValueOrDefault(run.RunId))
                : null;
            items.Add(new ProcessDashboardActivityItem(
                run.RootRunId,
                run.RunId,
                run.Status,
                run.UpdatedAtUtc,
                projection));
        }

        return new ProcessDashboardActivityResult(MapMode(selection.Mode), items);
    }

    private ProcessDashboardProjectionDetails MapProjection(
        ProcessRuntimeActivityRow run,
        ProcessProjectionSnapshot snapshot,
        ProcessRunRecordSummary? recordSummary)
    {
        var projection = jsonCodec.ReadSnapshot<ProcessLiveProcessSnapshot>(snapshot);
        if (projection.RunId != run.RunId || projection.RootRunId != run.RootRunId)
        {
            throw new InvalidOperationException(
                $"Process live projection '{snapshot.ProjectionKey}' does not match canonical run '{run.RunId}'.");
        }

        var projectId = projection.ProjectId ?? recordSummary?.Identity.ProjectId;
        return new ProcessDashboardProjectionDetails(
            projectId,
            ResolveProjectName(projection.ProjectName, projectId),
            ResolveProcessName(
                projection.ProcessName,
                recordSummary?.Identity.DefinitionId,
                run.RunId),
            projection.Status,
            projection.LastEventAtUtc,
            projection.Freshness);
    }

    private async Task<IReadOnlyDictionary<ProcessRunId, ProcessRunRecordSummary>> LoadRecordSummariesAsync(
        ProcessRuntimeActivitySelection selection,
        CancellationToken cancellationToken)
    {
        if (selection.Mode != ProcessRuntimeActivitySelectionMode.RecentFallback ||
            selection.Runs.Count == 0)
        {
            return new Dictionary<ProcessRunId, ProcessRunRecordSummary>();
        }

        var page = await runRecordStore
            .ListAsync(
                new ProcessRunRecordListQuery(selection.Runs.Count)
                {
                    RunIds = selection.Runs
                        .Select(run => run.RunId)
                        .ToArray(),
                    Payload = ProcessRunRecordListPayload.Compact
                },
                cancellationToken)
            .ConfigureAwait(false);
        return page.Records.ToDictionary(summary => summary.Identity.RunId);
    }

    private static string ResolveProjectName(string projectedName, Guid? projectId)
    {
        if (!string.IsNullOrWhiteSpace(projectedName))
        {
            return projectedName.Trim();
        }

        return projectId.HasValue
            ? $"Project {projectId.Value:D}"
            : "Unassigned project";
    }

    private static string ResolveProcessName(
        string projectedName,
        ProcessDefinitionId? definitionId,
        ProcessRunId runId)
    {
        if (!string.IsNullOrWhiteSpace(projectedName))
        {
            return projectedName.Trim();
        }

        return definitionId.HasValue
            ? $"Process definition {definitionId.Value}"
            : $"Process run {runId}";
    }

    private static ProcessDashboardActivityMode MapMode(ProcessRuntimeActivitySelectionMode mode)
        => mode switch
        {
            ProcessRuntimeActivitySelectionMode.Active => ProcessDashboardActivityMode.Active,
            ProcessRuntimeActivitySelectionMode.RecentFallback => ProcessDashboardActivityMode.RecentFallback,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Process runtime activity mode is not defined.")
        };
}
