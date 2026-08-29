using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CanDoItAll.AgentFramework.ProviderHistory;

public static class HistoryQueryBinding {
    public static string Create(HistoryAccessContext context, ProviderRequestHistoryQuery query) {
        var normalized = new {
            context.Partition,
            context.Fence,
            context.AuthorizationStamp,
            context.Caller,
            AllowedProviders = context.AllowedProviders?.Select(provider => provider.Value).Order().ToArray(),
            Provider = (query.Scope as HistoryProviderScope.SingleProvider)?.Provider.Value,
            From = query.FromUtc.UtcTicks,
            To = query.ToUtc.UtcTicks,
            Model = query.Model?.Value,
            query.Workload,
            query.Operation,
            query.Outcome,
            query.PriceState,
            query.CredentialId,
            query.Issuer,
            query.Subject,
            query.RequestId,
            query.AttemptId,
            query.CorrelationId,
            query.PageSize
        };
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(normalized))));
    }

    public static void RequireScope(HistoryAccessContext context, HistoryProviderScope scope) {
        if (scope is HistoryProviderScope.SingleProvider single &&
            context.AllowedProviders is { } allowed && !allowed.Contains(single.Provider)) {
            throw new ProviderHistoryException(HistoryFailure.Denied, "History access is not authorized for this provider.");
        }
    }

    public static HistoryPagePosition? ReadPosition(string? cursor, string binding,
        ProviderRequestHistoryQuery query, IHistoryCursorProtector protector) {
        if (cursor is null) {
            return null;
        }
        var value = protector.Unprotect(cursor);
        if (value.Version != 1 || value.Binding != binding || value.Position is null ||
            value.Position.EntryId.Value == Guid.Empty ||
            value.Position.SortAtUtc < query.FromUtc || value.Position.SortAtUtc >= query.ToUtc) {
            throw new ProviderHistoryException(HistoryFailure.InvalidCursor, "This history cursor is no longer valid. Run Search again.");
        }
        return value.Position;
    }
}
