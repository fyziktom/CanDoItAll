using CanDoItAll.Processes.Projections;
using CanDoItAll.Workbench.Insights.UI;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectManagerActivitySource(ProjectManagerSummarySnapshot report,
    ProjectManagerSummaryQueryService reports, Func<CancellationToken, Task> requireCurrent, ILogger logger) : IManagerActivitySource {
    private readonly Dictionary<(ProjectManagerActivityKind, ProjectManagerActivityStatusFilter), ProjectManagerActivityAggregate> aggregates = [];
    private readonly Dictionary<(ProjectManagerActivityStatusFilter, int), ProcessRunRecordCursor> cursors = [];
    private long generation;
    private bool disposed;

    public async Task<ManagerActivityPresentation> ReadAsync(ManagerActivityQuery query, CancellationToken cancellationToken) {
        ObjectDisposedException.ThrowIf(disposed, this);
        var request = Interlocked.Increment(ref generation);
        await requireCurrent(cancellationToken);
        RequireRequest(request, cancellationToken);
        var key = (query.Kind, query.Status);
        ProcessRunRecordCursor? cursor = null;
        if (query.Kind == ProjectManagerActivityKind.Process && query.PageIndex > 0) {
            cursor = cursors.GetValueOrDefault((query.Status, query.PageIndex))
                ?? throw new InvalidOperationException("The requested Process page has no accepted preceding cursor.");
        }
        var page = await reports.QueryActivityPageAsync(new(report, query.Kind, query.Status, query.PageIndex, ManagerActivitySession.PageSize) {
            ProcessCursor = cursor,
            KnownAggregate = aggregates.GetValueOrDefault(key)
        }, cancellationToken);
        await requireCurrent(cancellationToken);
        RequireRequest(request, cancellationToken);
        aggregates[key] = page.Aggregate;
        if (query.Kind == ProjectManagerActivityKind.Process && page.NextProcessCursor is { } next) {
            cursors[(query.Status, query.PageIndex + 1)] = next;
        }
        return new(page.Items.Select(item => item with { Tags = item.Tags.ToArray() }).ToArray(), page.PageIndex,
            page.PageSize, page.TotalCount, page.Totals, page.TotalDurationMilliseconds,
            query.Kind == ProjectManagerActivityKind.Process ? page.NextProcessCursor is not null :
                checked((page.PageIndex + 1) * page.PageSize) < page.TotalCount);
    }

    private void RequireRequest(long request, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(disposed, this);
        if (request != generation) {
            throw new InvalidOperationException("The Activity request was superseded.");
        }
    }

    public string DescribeFailure(Exception exception) {
        logger.LogError(exception, "Manager activity read failed for original project {ProjectId} at report cutoff {AsOfUtc}.", report.ProjectId, report.AsOfUtc);
        return "The filtered activity page could not be loaded.";
    }

    public void Dispose() {
        disposed = true;
        Interlocked.Increment(ref generation);
        aggregates.Clear();
        cursors.Clear();
    }
}
