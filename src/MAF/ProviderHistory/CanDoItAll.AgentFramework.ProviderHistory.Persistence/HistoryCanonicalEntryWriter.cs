using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.AgentFramework.ProviderHistory.Persistence;

internal static class HistoryCanonicalEntryWriter {
    internal static async Task UpsertAsync(AppDbContext db, HistoryEntry evidence,
        HistoryOwnerRole role, bool updateExisting, CancellationToken cancellationToken) {
        var row = db.Set<HistoryEntryRow>().Local.SingleOrDefault(entry => entry.Id == evidence.Id.Value)
            ?? await db.Set<HistoryEntryRow>().SingleOrDefaultAsync(
            entry => entry.Id == evidence.Id.Value, cancellationToken);
        Apply(db, row, evidence, role, updateExisting);
    }

    internal static async Task UpsertAttemptsAsync(AppDbContext db, IReadOnlyList<HistoryEntry> attempts,
        CancellationToken cancellationToken) {
        if (attempts.Count == 0) {
            return;
        }
        var ids = attempts.Select(attempt => attempt.Id.Value).ToArray();
        var rows = await db.Set<HistoryEntryRow>().Where(row => ids.Contains(row.Id))
            .ToDictionaryAsync(row => row.Id, cancellationToken);
        foreach (var local in db.Set<HistoryEntryRow>().Local.Where(row => ids.Contains(row.Id))) {
            rows.TryAdd(local.Id, local);
        }
        foreach (var attempt in attempts) {
            Apply(db, rows.GetValueOrDefault(attempt.Id.Value), attempt, HistoryOwnerRole.ContentOwner, false);
        }
    }

    private static void Apply(AppDbContext db, HistoryEntryRow? row, HistoryEntry evidence, HistoryOwnerRole role, bool updateExisting) {
        if (row is null) {
            db.Add(HistoryEntryMapping.From(evidence));
            return;
        }
        if (row.PartitionId != evidence.Partition.StorageLineageId ||
            row.AttemptId != evidence.AttemptId?.Value || row.Granularity != evidence.Granularity) {
            throw new ProviderHistoryException(HistoryFailure.Conflict,
                "Canonical evidence conflicts with the provider attempt identity or granularity.");
        }
        if (!updateExisting) {
            return;
        }
        var canonicalAttempt = role == HistoryOwnerRole.PrimaryEvidence &&
            evidence.MetadataAuthority == HistoryMetadataAuthority.CanonicalProjection &&
            row.MetadataAuthority == HistoryMetadataAuthority.CanonicalProjection;
        if (row.Granularity != HistoryGranularity.LegacyAggregate && !canonicalAttempt) {
            return;
        }
        var replacement = HistoryEntryMapping.From(evidence);
        replacement.SortAtUtc = row.SortAtUtc;
        replacement.TimeBasis = row.TimeBasis;
        replacement.ConcurrencyToken = row.ConcurrencyToken;
        if (row.RetentionAuthority == HistoryRetentionAuthority.HistoryPolicy && row.ExpiresAtUtc < replacement.ExpiresAtUtc) {
            replacement.ExpiresAtUtc = row.ExpiresAtUtc;
        }
        db.Entry(row).CurrentValues.SetValues(replacement);
    }
}
