using CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.Entities;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.Repositories;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.ProviderHistory.Persistence;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.DatabaseTransfer;

internal static class LlmChatHistoryTransfer {
    internal static async Task<HistoryPartition> ValidateAsync(AppDbContext target, bool replacesChatData,
        CancellationToken cancellationToken) {
        var partition = await HistoryPartitionStore.GetForWriteAsync(target, cancellationToken);
        if (replacesChatData && (await target.Set<HistorySourceRow>()
                .AnyAsync(row => row.Kind == HistorySourceKind.SimpleChat, cancellationToken) ||
            await target.Set<HistoryOutboxRow>().AnyAsync(cancellationToken))) {
            throw new InvalidOperationException("Chat replacement would invalidate retained history. Use an empty target database and transfer provider history first.");
        }
        return partition;
    }

    internal static void Stage(AppDbContext target, IReadOnlyList<LlmChatInvocationRecordRow> rows,
        HistoryPartition partition, HistoryOutboxWriter outbox) {
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
                outbox.Stage(target, LlmChatHistoryProjection.Create(record, partition));
                ordinal = row.Ordinal;
            }
            if (attempts.Count > 0) {
                var id = operation.Key.ToString("N");
                outbox.Stage(target, new(new(partition, HistorySourceKind.SimpleChat, new(id), new(id)),
                    new(ordinal), HistorySourceMutationKind.Upsert, null, []) { Attempts = attempts });
            }
        }
    }
}
