using System.Security.Cryptography;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.AgentFramework.ProviderHistory.Persistence;

public sealed class HistoryDetailStore(HistoryTextProtector protector, TimeProvider clock, ILogger<HistoryDetailStore> logger) {
    internal async Task<(HistoryDetailRow? Row, HistoryDetailState State)> PrepareAsync(
        HistoryAttemptStart start, string? text, HistoryDetailPart part, long inputRevision,
        CancellationToken cancellationToken) {
        if (start.ContentOwner is not null) {
            return (null, HistoryDetailState.PendingCanonical);
        }
        if (start.Policy.Policy.CaptureMode == HistoryCaptureMode.Light) {
            return (null, HistoryDetailState.NotCaptured);
        }
        if (start.StartedAtUtc.AddDays(start.Policy.Policy.DetailRetentionDays) <= clock.GetUtcNow()) {
            return (null, HistoryDetailState.Expired);
        }
        try {
            if (part == HistoryDetailPart.Input) {
                await protector.FreezeAsync(start, cancellationToken);
            }
            if (text is null) {
                return (null, HistoryDetailState.UnsupportedDetailShape);
            }
            var row = await protector.CaptureAsync(start, text, part, inputRevision,
                clock.GetUtcNow(), start.Policy.Policy.MaximumTextBytes, cancellationToken);
            return (row, HistoryDetailState.Captured);
        } catch (Exception exception) when (exception is CryptographicException or ProviderHistoryException) {
            logger.LogWarning("History detail protection unavailable for attempt {AttemptId}; failure type {FailureType}.",
                start.AttemptId.Value, exception.GetType().Name);
            return (null, HistoryDetailState.ProtectionUnavailable);
        }
    }

    internal static async Task AttachAsync(AppDbContext db, HistoryEntryRow entry,
        HistoryDetailRow detail, HistoryPolicyRow policy, CancellationToken cancellationToken) {
        var existing = detail.Part == HistoryDetailPart.Input
            ? await db.Set<HistoryDetailRow>().SingleOrDefaultAsync(row =>
                row.PartitionId == detail.PartitionId && row.RequestId == detail.RequestId &&
                row.InputRevision == detail.InputRevision && row.Part == HistoryDetailPart.Input, cancellationToken)
            : await db.Set<HistoryDetailRow>().SingleOrDefaultAsync(row =>
                row.PartitionId == detail.PartitionId && row.EntryId == detail.EntryId &&
                row.Part == HistoryDetailPart.Response, cancellationToken);
        if (existing is not null) {
            if (detail.Part == HistoryDetailPart.Input) {
                entry.InputDetailId = existing.Id;
                entry.DetailState = existing.ExpiresAtUtc <= detail.CapturedAtUtc ? HistoryDetailState.Expired : existing.State;
            }
            return;
        }
        if (policy.CaptureMode != HistoryCaptureMode.Detailed) {
            Omit(detail, HistoryDetailState.NotCaptured);
        } else if (detail.StoredBytes > policy.DetailQuotaBytes - policy.UsedDetailBytes) {
            Omit(detail, HistoryDetailState.QuotaExceeded);
        }
        policy.UsedDetailBytes = checked(policy.UsedDetailBytes + detail.StoredBytes);
        db.Add(detail);
        if (detail.Part == HistoryDetailPart.Input) {
            entry.InputDetailId = detail.Id;
        }
        entry.DetailState = detail.State;
    }

    internal static void Omit(HistoryDetailRow row, HistoryDetailState state) {
        row.ProtectedText = "";
        row.StoredBytes = 0;
        row.CapturedBytes = 0;
        row.OriginalBytes = 0;
        row.Flags = HistoryDetailFlags.None;
        row.State = state;
    }

    public async Task<HistoryDetail> ReadAsync(AppDbContext db, HistoryEntryRow entry, CancellationToken cancellationToken) {
        var now = clock.GetUtcNow();
        if (entry.ExpiresAtUtc <= now) {
            return new(new(entry.Id), HistoryDetailState.Expired);
        }
        var rows = await db.Set<HistoryDetailRow>().AsNoTracking().Where(row =>
            row.PartitionId == entry.PartitionId && (row.Id == entry.InputDetailId || row.EntryId == entry.Id))
            .Take(2).ToListAsync(cancellationToken);
        if (rows.Count == 0) {
            return new(new(entry.Id), entry.DetailState);
        }
        var input = rows.SingleOrDefault(row => row.Part == HistoryDetailPart.Input && row.ExpiresAtUtc > now && row.StoredBytes > 0);
        var response = rows.SingleOrDefault(row => row.Part == HistoryDetailPart.Response && row.ExpiresAtUtc > now && row.StoredBytes > 0);
        if (input is null && response is null) {
            var state = rows.All(row => row.ExpiresAtUtc <= now) ? HistoryDetailState.Expired
                : rows.First(row => row.ExpiresAtUtc > now).State;
            return new(new(entry.Id), state);
        }
        try {
            return new(new(entry.Id), HistoryDetailState.Captured,
                input is null ? null : protector.Read(input), response is null ? null : protector.Read(response),
                rows.Where(row => row.ExpiresAtUtc > now && row.StoredBytes > 0).Min(row => row.ExpiresAtUtc));
        } catch (ProviderHistoryException) {
            return new(new(entry.Id), HistoryDetailState.ProtectionUnavailable);
        }
    }
}
