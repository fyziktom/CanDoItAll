using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.AgentFramework.ProviderHistory.Persistence;

internal static class HistoryPolicyRetention {
    internal static async Task<HistoryRetentionPreview> PreviewAsync(AppDbContext db, Guid partition,
        HistoryPolicy policy, CancellationToken cancellationToken) {
        var metadata = await Metadata(db, partition, policy).OrderBy(row => row.Id)
            .Select(row => row.Id).Take(policy.BatchSize + 1).ToArrayAsync(cancellationToken);
        var detail = await Detail(db, partition, policy).OrderBy(row => row.Id)
            .Select(row => row.Id).Take(policy.BatchSize + 1).ToArrayAsync(cancellationToken);
        return new(metadata.Length, detail.Length, policy.BatchSize,
            metadata.Length > policy.BatchSize || detail.Length > policy.BatchSize);
    }

    internal static async Task ShortenAsync(AppDbContext db, Guid partition, HistoryPolicy policy, CancellationToken cancellationToken) {
        var metadata = await Metadata(db, partition, policy).OrderBy(row => row.Id)
            .Select(row => row.Id).Take(policy.BatchSize + 1).ToArrayAsync(cancellationToken);
        var detail = await Detail(db, partition, policy).OrderBy(row => row.Id)
            .Select(row => row.Id).Take(policy.BatchSize + 1).ToArrayAsync(cancellationToken);
        if (metadata.Length > policy.BatchSize || detail.Length > policy.BatchSize) {
            throw new ProviderHistoryException(HistoryFailure.InvalidQuery,
                "Existing history exceeds the bounded shortening limit. Apply the policy for future requests only.");
        }
        await Metadata(db, partition, policy).Where(row => metadata.Contains(row.Id))
            .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.ExpiresAtUtc,
                row => row.SortAtUtc.AddDays(policy.MetadataRetentionDays)), cancellationToken);
        await Detail(db, partition, policy).Where(row => detail.Contains(row.Id))
            .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.ExpiresAtUtc,
                row => row.CapturedAtUtc.AddDays(policy.DetailRetentionDays)), cancellationToken);
    }

    private static IQueryable<HistoryEntryRow> Metadata(AppDbContext db, Guid partition, HistoryPolicy policy) =>
        db.Set<HistoryEntryRow>().AsNoTracking().Where(row => row.PartitionId == partition &&
            row.RetentionAuthority == HistoryRetentionAuthority.HistoryPolicy &&
            row.ExpiresAtUtc > row.SortAtUtc.AddDays(policy.MetadataRetentionDays));

    private static IQueryable<HistoryDetailRow> Detail(AppDbContext db, Guid partition, HistoryPolicy policy) =>
        db.Set<HistoryDetailRow>().AsNoTracking().Where(row => row.PartitionId == partition &&
            row.ExpiresAtUtc > row.CapturedAtUtc.AddDays(policy.DetailRetentionDays));
}
