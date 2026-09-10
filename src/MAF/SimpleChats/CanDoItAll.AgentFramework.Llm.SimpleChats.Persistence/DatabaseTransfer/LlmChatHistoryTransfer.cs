using CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.Entities;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.Repositories;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.ProviderHistory.Persistence;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.DatabaseTransfer;

internal static class LlmChatHistoryTransfer {
    internal static async Task<HistoryPartition> ValidateAsync(HistoryTargetWriteSession history, bool replacesChatData,
        CancellationToken cancellationToken) {
        var partition = await history.Partitions.GetForWriteAsync(cancellationToken);
        if (replacesChatData && await history.HasRetainedSourceOrPendingMutationsAsync(HistorySourceKind.SimpleChat, cancellationToken)) {
            throw new InvalidOperationException("Chat replacement would invalidate retained history. Use an empty target database and transfer provider history first.");
        }
        return partition;
    }

    internal static async Task StageAsync(IReadOnlyList<LlmChatInvocationRecordRow> rows,
        HistoryPartition partition, HistoryOutboxWriter outbox, CancellationToken cancellationToken) {
        foreach (var operation in rows.GroupBy(row => row.OperationId)) {
            var attempts = new List<HistoryEntry>();
            var ordinal = 0;
            foreach (var row in operation.OrderBy(row => row.Ordinal)) {
                var record = LlmChatPersistenceMapper.ToDomain(row);
                if (record.HistoryAttempts.Any(attempt => attempt.Partition != partition)) {
                    throw new InvalidDataException("Canonical chat evidence belongs to another storage lineage. Transfer provider request history before LLM chats.");
                }
                attempts.AddRange(record.HistoryAttempts);
                if (attempts.Count > HistoryAttemptCollection.MaximumAttempts) {
                    throw new InvalidDataException("The imported chat operation exceeds the bounded history evidence contract.");
                }
                await outbox.StageAsync(LlmChatHistoryProjection.Create(record, partition), cancellationToken);
                ordinal = row.Ordinal;
            }
            if (attempts.Count > 0) {
                var id = operation.Key.ToString("N");
                await outbox.StageAsync(new(new(partition, HistorySourceKind.SimpleChat, new(id), new(id)),
                    new(ordinal), HistorySourceMutationKind.Upsert, null, []) { Attempts = attempts }, cancellationToken);
            }
        }
    }
}
