using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.AgentFramework.ProviderHistory.Persistence;

public sealed class HistoryCoverageReader(IEnumerable<IHistorySourceMaintenance> sources) {
    private const string ProjectionFailedCode = "ProjectionFailed";
    private readonly HistorySourceKind[] kinds = sources.Select(source => source.Kind).Distinct().Order().ToArray();

    public async Task<HistoryCoverage> ReadAsync(AppDbContext db, HistoryPartition partition, CancellationToken cancellationToken) {
        if (kinds.Length == 0) {
            return new(HistoryCoverageState.Pending, null);
        }
        var rows = await db.Set<HistoryCheckpointRow>().AsNoTracking()
            .Where(row => row.PartitionId == partition.StorageLineageId && kinds.Contains(row.SourceKind))
            .Select(row => new { row.Coverage, row.IndexedThroughUtc }).ToArrayAsync(cancellationToken);
        var queued = await db.Set<HistoryOutboxRow>().AnyAsync(row => row.PartitionId == partition.StorageLineageId, cancellationToken);
        var state = rows.Any(row => row.Coverage == HistoryCoverageState.Failed) ? HistoryCoverageState.Failed
            : rows.Length != kinds.Length || rows.Any(row => row.Coverage == HistoryCoverageState.Pending) ? HistoryCoverageState.Pending
            : queued || rows.Any(row => row.Coverage != HistoryCoverageState.Current) ? HistoryCoverageState.Partial
            : HistoryCoverageState.Current;
        var through = rows.Length == 0 || rows.Any(row => row.IndexedThroughUtc == null)
            ? (DateTimeOffset?)null : rows.Min(row => row.IndexedThroughUtc);
        return new(state, through, state == HistoryCoverageState.Failed ? ProjectionFailedCode : null);
    }
}
