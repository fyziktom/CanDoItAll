namespace CanDoItAll.AgentFramework.ProviderHistory;

public interface IProviderHistoryPartition {
    Task<HistoryPartition> GetAsync(CancellationToken cancellationToken);
}
