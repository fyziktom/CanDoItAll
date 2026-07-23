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
    ProcessProjectionJsonCodec jsonCodec) : IProcessDashboardActivityQueryService
{
    public async Task<ProcessDashboardActivityResult> QueryAsync(
        ProcessDashboardActivityQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var selection = await runtimeActivityStore
            .QueryActivityAsync(new ProcessRuntimeActivityQuery(query.Take), cancellationToken)
            .ConfigureAwait(false);
        var items = new List<ProcessDashboardActivityItem>(selection.Runs.Count);
        foreach (var run in selection.Runs)
        {
            var projection = await LoadProjectionAsync(run, cancellationToken).ConfigureAwait(false);
            items.Add(new ProcessDashboardActivityItem(
                run.RootRunId,
                run.RunId,
                run.Status,
                run.UpdatedAtUtc,
                projection));
        }

        return new ProcessDashboardActivityResult(MapMode(selection.Mode), items);
    }

    private async Task<ProcessDashboardProjectionDetails?> LoadProjectionAsync(
        ProcessRuntimeActivityRow run,
        CancellationToken cancellationToken)
    {
        var snapshot = await projectionStore
            .LoadSnapshotAsync(
                ProcessRuntimeProjectionProjector.ProjectorName,
                ProcessRuntimeProjectionKeys.Live(run.RunId),
                cancellationToken)
            .ConfigureAwait(false);
        if (snapshot is null)
        {
            return null;
        }

        var projection = jsonCodec.ReadSnapshot<ProcessLiveProcessSnapshot>(snapshot);
        if (projection.RunId != run.RunId || projection.RootRunId != run.RootRunId)
        {
            throw new InvalidOperationException(
                $"Process live projection '{snapshot.ProjectionKey}' does not match canonical run '{run.RunId}'.");
        }

        return new ProcessDashboardProjectionDetails(
            projection.ProjectId,
            projection.ProjectName,
            projection.ProcessName,
            projection.Status,
            projection.LastEventAtUtc,
            projection.Freshness);
    }

    private static ProcessDashboardActivityMode MapMode(ProcessRuntimeActivitySelectionMode mode)
        => mode switch
        {
            ProcessRuntimeActivitySelectionMode.Active => ProcessDashboardActivityMode.Active,
            ProcessRuntimeActivitySelectionMode.RecentFallback => ProcessDashboardActivityMode.RecentFallback,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Process runtime activity mode is not defined.")
        };
}
