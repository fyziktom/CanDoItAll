using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.AgentFramework.ProviderHistory.Persistence;

public sealed class HistoryReadStore(
    IDbContextFactory<AppDbContext> factory,
    HistoryCoverageReader coverage,
    HistoryDetailStore details,
    TimeProvider clock) : IHistoryReadStore {
    public async Task<HistoryIndexPage> SearchAsync(HistoryAccessContext context, ProviderRequestHistoryQuery query,
        HistoryPagePosition? position, CancellationToken cancellationToken) {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await HistoryPartitionStore.RequireAsync(db, context.Partition, cancellationToken);
        var now = clock.GetUtcNow();
        var rows = await HistoryIndexQuery.Page(db, context, query, position, now)
            .Select(HistoryEntrySqlProjection.For(context.Partition)).ToArrayAsync(cancellationToken);
        return new(rows, await coverage.ReadAsync(db, context.Partition, cancellationToken), now);
    }

    public async Task<HistoryMetadata?> GetMetadataAsync(HistoryAccessContext context, HistoryEntryId entryId,
        CancellationToken cancellationToken) {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await HistoryPartitionStore.RequireAsync(db, context.Partition, cancellationToken);
        var entry = await HistoryIndexQuery.Authorized(db, context, clock.GetUtcNow()).Where(row => row.Id == entryId.Value)
            .Select(HistoryEntrySqlProjection.For(context.Partition)).SingleOrDefaultAsync(cancellationToken);
        if (entry is null) {
            return null;
        }
        var owners = await (from owner in db.Set<HistoryOwnerRow>().AsNoTracking()
            join source in db.Set<HistorySourceRow>().AsNoTracking() on owner.SourceId equals source.Id
            where owner.EntryId == entryId.Value && owner.PartitionId == context.Partition.StorageLineageId &&
                source.PartitionId == context.Partition.StorageLineageId
            orderby owner.Role, source.Kind, source.Id
            select new { source.Kind, source.OwnerId, source.EvidenceId, source.Version, owner.Role, owner.State })
            .Take(17).ToArrayAsync(cancellationToken);
        return new(entry, owners.Take(16).Select(owner => new HistoryOwnerLink(entryId,
            new(context.Partition, owner.Kind, new(owner.OwnerId), new(owner.EvidenceId)),
            new(owner.Version), owner.Role, owner.State)).ToArray(), owners.Length > 16);
    }

    public async Task<HistoryDetail> ReadDetailAsync(HistoryAccessContext context, HistoryEntryId entryId, CancellationToken cancellationToken) {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await HistoryPartitionStore.RequireAsync(db, context.Partition, cancellationToken);
        var entry = await HistoryIndexQuery.Authorized(db, context, clock.GetUtcNow()).SingleOrDefaultAsync(row => row.Id == entryId.Value, cancellationToken);
        return entry is null ? new(entryId, HistoryDetailState.Unavailable)
            : await details.ReadAsync(db, entry, cancellationToken);
    }

    public async Task<bool> IsCurrentAsync(HistoryAccessContext context, HistoryMetadata metadata,
        CanonicalEvidenceReference? owner, CancellationToken cancellationToken) {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await HistoryPartitionStore.RequireAsync(db, context.Partition, cancellationToken);
        var entry = metadata.Entry;
        if (!await HistoryIndexQuery.Authorized(db, context, clock.GetUtcNow()).AnyAsync(row =>
            row.Id == entry.Id.Value && row.Version == entry.Version && row.DetailState == entry.DetailState &&
            row.ExpiresAtUtc == entry.ExpiresAtUtc, cancellationToken)) {
            return false;
        }
        if (owner is null) {
            return true;
        }
        var link = metadata.Owners.SingleOrDefault(link => link.Source == owner && link.State == HistoryOwnerState.Linked);
        if (link is null) {
            return false;
        }
        var sourceId = HistorySourceIdentity.Key(owner);
        return await (from retained in db.Set<HistoryOwnerRow>().AsNoTracking()
            join source in db.Set<HistorySourceRow>().AsNoTracking() on retained.SourceId equals source.Id
            where retained.PartitionId == context.Partition.StorageLineageId &&
                retained.EntryId == entry.Id.Value && source.Id == sourceId &&
                !source.IsDeleted && source.Version == link.Version.Value &&
                retained.State == HistoryOwnerState.Linked && retained.Role == HistoryOwnerRole.ContentOwner
            select source.Id).AnyAsync(cancellationToken);
    }
}
