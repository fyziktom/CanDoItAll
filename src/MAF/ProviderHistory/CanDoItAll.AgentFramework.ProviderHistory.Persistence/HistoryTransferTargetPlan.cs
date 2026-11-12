using System.Collections.Immutable;

namespace CanDoItAll.AgentFramework.ProviderHistory.Persistence;

public static class HistoryTransferTargetPlan {
    public static ImmutableArray<Type> EntityTypes { get; } = [
        typeof(HistoryPartitionRow), typeof(HistoryStorageIdentity), typeof(HistoryPolicyRow), typeof(HistoryPolicyAuditRow),
        typeof(HistoryCheckpointRow), typeof(HistoryEntryRow), typeof(HistoryDetailRow), typeof(HistoryHostLeaseRow),
        typeof(HistorySourceRow), typeof(HistoryOwnerRow), typeof(HistoryOutboxRow)
    ];
}
