using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence;

internal static class LlmChatHistoryDetail {
    internal static async Task<HistoryDetail> ReadAsync(IDbContextFactory<AppDbContext> factory,
        CanonicalEvidenceReference source, HistoryEntryId entryId, CancellationToken cancellationToken) {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await ProviderHistory.Persistence.HistoryPartitionStore.RequireAsync(db, source.Partition, cancellationToken);
        var operation = Guid.ParseExact(source.Owner.Value, "N");
        var rows = await db.Database.SqlQuery<TurnText>($"""
            SELECT message."Role" AS "Role", left(message."Text", 131072) AS "Text",
                   octet_length(message."Text")::bigint AS "OriginalBytes"
            FROM "LlmChats_Messages" AS message
            JOIN "LlmChats_Operations" AS operation ON operation."Id" = {operation}
                AND operation."ConversationId" = message."ConversationId"
            WHERE message."TurnId" = {operation}
            ORDER BY message."Sequence"
            LIMIT 3
            """).ToArrayAsync(cancellationToken);
        if (rows.Length > 2) {
            return new(entryId, HistoryDetailState.UnsupportedDetailShape);
        }
        var input = rows.SingleOrDefault(row => row.Role == LlmMessageRole.User);
        var response = rows.SingleOrDefault(row => row.Role == LlmMessageRole.Assistant);
        return new(entryId, rows.Length == 0 ? HistoryDetailState.Unavailable : HistoryDetailState.Canonical,
            Capture(input), Capture(response));
    }

    private static HistoryCapturedText? Capture(TurnText? row) {
        if (row is null) {
            return null;
        }
        var captured = HistoryTextCapture.Capture(row.Text, 32 * 1024, []);
        return captured with {
            OriginalBytes = row.OriginalBytes,
            Flags = captured.Flags | (captured.CapturedBytes < row.OriginalBytes ? HistoryDetailFlags.Truncated : HistoryDetailFlags.None)
        };
    }

    private sealed class TurnText {
        public LlmMessageRole Role { get; set; }
        public string Text { get; set; } = "";
        public long OriginalBytes { get; set; }
    }
}
