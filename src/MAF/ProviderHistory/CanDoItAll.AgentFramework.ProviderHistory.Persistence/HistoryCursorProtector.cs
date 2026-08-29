using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace CanDoItAll.AgentFramework.ProviderHistory.Persistence;

public sealed class HistoryCursorProtector(IDataProtectionProvider protection) : IHistoryCursorProtector {
    private readonly IDataProtector protector = protection.CreateProtector("CanDoItAll.ProviderHistory.Cursor.v1");

    public string Protect(HistoryPageCursor cursor) => protector.Protect(JsonSerializer.Serialize(cursor));

    public HistoryPageCursor Unprotect(string cursor) {
        if (string.IsNullOrWhiteSpace(cursor) || cursor.Length > 8192) {
            throw Invalid();
        }
        try {
            return JsonSerializer.Deserialize<HistoryPageCursor>(protector.Unprotect(cursor)) ?? throw Invalid();
        } catch (Exception exception) when (exception is CryptographicException or JsonException or ArgumentException) {
            throw Invalid();
        }
    }

    private static ProviderHistoryException Invalid() =>
        new(HistoryFailure.InvalidCursor, "This history cursor is invalid. Run Search again.");
}
