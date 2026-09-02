namespace CanDoItAll.AgentFramework.ProviderHistory.Persistence;

public interface IHistorySourceMaintenance {
    HistorySourceKind Kind { get; }
    Task<HistorySourceProgress> ProcessAsync(HistoryMaintenanceContext context, string? cursor,
        int maximumItems, CancellationToken cancellationToken);
}

public sealed record HistorySourceProgress(string? Cursor, bool BackfillComplete);
