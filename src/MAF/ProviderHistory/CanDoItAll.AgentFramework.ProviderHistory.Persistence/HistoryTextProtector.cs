using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;

namespace CanDoItAll.AgentFramework.ProviderHistory.Persistence;

public sealed class HistoryTextProtector(IDataProtectionProvider protection, IProviderHistorySecrets secrets) {
    private readonly ConditionalWeakTable<HistoryAttemptStart, Lazy<Task<IReadOnlyList<string>>>> snapshots = new();

    public Task FreezeAsync(HistoryAttemptStart start, CancellationToken cancellationToken) =>
        snapshots.GetValue(start, key => new(() => ReadSecretsAsync(key.Provider.Id!.Value, cancellationToken))).Value;

    private async Task<IReadOnlyList<string>> ReadSecretsAsync(ProviderIdentity provider, CancellationToken cancellationToken) {
        var values = await secrets.GetKnownSecretsAsync(provider, cancellationToken);
        if (values.Count > 128 || values.Any(value => value.Length > 128 * 1024)) {
            throw new ProviderHistoryException(HistoryFailure.Unavailable, "The credential redaction context exceeds its safety bounds.");
        }
        return values.ToArray();
    }

    public async Task<HistoryDetailRow> CaptureAsync(HistoryAttemptStart start, string text,
        HistoryDetailPart part, long inputRevision, DateTimeOffset now, int maximumBytes,
        CancellationToken cancellationToken) {
        if (part == HistoryDetailPart.Input) {
            await FreezeAsync(start, cancellationToken);
        }
        if (!snapshots.TryGetValue(start, out var snapshot)) {
            throw new ProviderHistoryException(HistoryFailure.Unavailable, "The original credential redaction context is unavailable.");
        }
        var knownSecrets = await snapshot.Value;
        var captured = HistoryTextCapture.Capture(text, maximumBytes, knownSecrets);
        var row = new HistoryDetailRow {
            PartitionId = start.Partition.StorageLineageId,
            RequestId = start.RequestId.Value,
            EntryId = part == HistoryDetailPart.Response ? start.EntryId.Value : null,
            InputRevision = inputRevision,
            Part = part,
            CapturedAtUtc = start.StartedAtUtc,
            ExpiresAtUtc = part == HistoryDetailPart.Input ? start.InputExpiresAtUtc
                : start.StartedAtUtc.AddDays(start.Policy.Policy.DetailRetentionDays),
            CapturedBytes = captured.CapturedBytes,
            OriginalBytes = captured.OriginalBytes,
            Flags = captured.Flags
        };
        row.ProtectedText = Protector(row).Protect(captured.Text);
        row.StoredBytes = Encoding.UTF8.GetByteCount(row.ProtectedText);
        return row;
    }

    public HistoryCapturedText Read(HistoryDetailRow row) {
        try {
            return new(Protector(row).Unprotect(row.ProtectedText), row.OriginalBytes, row.CapturedBytes, row.Flags);
        } catch (CryptographicException) {
            throw new ProviderHistoryException(HistoryFailure.Unavailable, "History detail protection keys are unavailable.");
        }
    }

    private IDataProtector Protector(HistoryDetailRow row)
        => protection.CreateProtector("CanDoItAll.ProviderHistory.Detail.v1", row.PartitionId.ToString("N"),
            row.Id.ToString("N"), ((int)row.Part).ToString(CultureInfo.InvariantCulture));
}
