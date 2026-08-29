using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.AgentFramework.ProviderHistory.Persistence;

internal static class HistorySourceProjection {
    internal static async Task ReserveAsync(AppDbContext db, HistoryEntryRow entry,
        CanonicalEvidenceReference reference, CancellationToken cancellationToken) {
        if (reference.Partition.StorageLineageId != entry.PartitionId) {
            throw new ProviderHistoryException(HistoryFailure.Conflict, "A history owner cannot cross storage partitions.");
        }
        var source = await FindOrAddAsync(db, reference, cancellationToken);
        var state = source.IsDeleted ? HistoryOwnerState.Deleted
            : source.Version > 0 ? HistoryOwnerState.Linked : HistoryOwnerState.PendingCanonical;
        db.Add(new HistoryOwnerRow {
            PartitionId = entry.PartitionId, SourceId = source.Id, EntryId = entry.Id,
            Role = HistoryOwnerRole.ContentOwner, State = state
        });
        if (state == HistoryOwnerState.Linked) {
            Link(entry, reference.Kind, HistoryOwnerRole.ContentOwner);
        }
    }

    internal static async Task ApplyAsync(AppDbContext db, HistorySourceMutation mutation, CancellationToken cancellationToken) {
        HistorySourceIdentity.Validate(mutation);
        await HistoryPartitionStore.RequireAsync(db, mutation.Source.Partition, cancellationToken);
        var source = await FindOrAddAsync(db, mutation.Source, cancellationToken);
        var hash = HistorySourceIdentity.Hash(mutation);
        if (source.Version > mutation.Version.Value) {
            return;
        }
        if (source.Version == mutation.Version.Value) {
            if (source.MutationHash != hash) {
                throw new ProviderHistoryException(HistoryFailure.Conflict, "A history source version was reused with different evidence.");
            }
            return;
        }
        var updateExistingEvidence = source.Version > 0;
        source.Version = mutation.Version.Value;
        source.MutationHash = hash;
        source.IsDeleted = mutation.Kind == HistorySourceMutationKind.Delete;
        var owners = await db.Set<HistoryOwnerRow>().Where(row => row.SourceId == source.Id)
            .Take(HistorySourceIdentity.MaximumLinkedEntries + 1).ToListAsync(cancellationToken);
        owners.AddRange(db.Set<HistoryOwnerRow>().Local.Where(row => row.SourceId == source.Id && !owners.Contains(row)));
        if (owners.Count > HistorySourceIdentity.MaximumLinkedEntries) {
            throw new ProviderHistoryException(HistoryFailure.Conflict, "The canonical evidence exceeds its bounded owner-link contract.");
        }
        if (source.IsDeleted) {
            await DeleteAsync(db, source, owners, cancellationToken);
            return;
        }

        var ids = mutation.LinkedEntries.Select(id => id.Value).Distinct().ToHashSet();
        await HistoryCanonicalEntryWriter.UpsertAttemptsAsync(db, mutation.Attempts, cancellationToken);
        ids.UnionWith(mutation.Attempts.Select(attempt => attempt.Id.Value));
        var skipAggregate = ids.Count > 0 && mutation.Entry?.Granularity == HistoryGranularity.LegacyAggregate;
        if (mutation.Entry is { } evidence && !skipAggregate) {
            ids.Add(evidence.Id.Value);
            await HistoryCanonicalEntryWriter.UpsertAsync(db, evidence, mutation.Role, updateExistingEvidence, cancellationToken);
        }
        if (owners.Count + ids.Except(owners.Select(owner => owner.EntryId)).Count() > HistorySourceIdentity.MaximumLinkedEntries) {
            throw new ProviderHistoryException(HistoryFailure.Conflict, "Split canonical evidence into bounded source records before linking further attempts.");
        }
        var entries = await db.Set<HistoryEntryRow>().Where(row => ids.Contains(row.Id)).ToDictionaryAsync(row => row.Id, cancellationToken);
        foreach (var added in db.Set<HistoryEntryRow>().Local.Where(row => ids.Contains(row.Id))) {
            entries.TryAdd(added.Id, added);
        }
        foreach (var id in ids) {
            if (!entries.TryGetValue(id, out var entry)) {
                throw new ProviderHistoryException(HistoryFailure.Conflict, "A canonical owner references a missing provider attempt.");
            }
            if (entry.PartitionId != source.PartitionId) {
                throw new ProviderHistoryException(HistoryFailure.Conflict, "A canonical owner references another partition.");
            }
            var owner = owners.SingleOrDefault(row => row.EntryId == id);
            if (owner is null) {
                owner = new() { PartitionId = source.PartitionId, SourceId = source.Id, EntryId = id };
                db.Add(owner);
            }
            owner.Role = skipAggregate ? HistoryOwnerRole.Lineage : mutation.Role;
            owner.State = HistoryOwnerState.Linked;
            Link(entry, source.Kind, owner.Role);
        }
    }

    private static async Task<HistorySourceRow> FindOrAddAsync(AppDbContext db,
        CanonicalEvidenceReference reference, CancellationToken cancellationToken) {
        var id = HistorySourceIdentity.Key(reference);
        await HistoryWriteLock.AttemptAsync(db, id, cancellationToken);
        var row = db.Set<HistorySourceRow>().Local.SingleOrDefault(source => source.Id == id)
            ?? await db.Set<HistorySourceRow>().SingleOrDefaultAsync(source => source.Id == id, cancellationToken);
        if (row is not null) {
            HistorySourceIdentity.Require(row, reference);
            return row;
        }
        row = new() {
            Id = id, PartitionId = reference.Partition.StorageLineageId, Kind = reference.Kind,
            OwnerId = reference.Owner.Value, EvidenceId = reference.Evidence.Value
        };
        db.Add(row);
        return row;
    }

    private static void Link(HistoryEntryRow entry, HistorySourceKind kind, HistoryOwnerRole role) {
        if (role == HistoryOwnerRole.Lineage) {
            return;
        }
        entry.IsVisible = true;
        entry.MetadataAuthority = HistoryMetadataAuthority.CanonicalProjection;
        if (kind != HistorySourceKind.SharedRelay) {
            entry.RetentionAuthority = HistoryRetentionAuthority.CanonicalOwner;
            entry.ExpiresAtUtc = null;
        }
        if (role == HistoryOwnerRole.ContentOwner) {
            entry.DetailState = HistoryDetailState.Canonical;
        } else if (entry.DetailState == HistoryDetailState.PendingCanonical) {
            entry.DetailState = HistoryDetailState.Unavailable;
        }
    }

    private static async Task DeleteAsync(AppDbContext db, HistorySourceRow source,
        List<HistoryOwnerRow> owners, CancellationToken cancellationToken) {
        var ids = owners.Select(owner => owner.EntryId).ToArray();
        var candidates = await db.Set<HistoryOwnerRow>().Where(row =>
                ids.Contains(row.EntryId) && row.SourceId != source.Id).ToListAsync(cancellationToken);
        candidates.AddRange(db.Set<HistoryOwnerRow>().Local.Where(row =>
            ids.Contains(row.EntryId) && row.SourceId != source.Id && !candidates.Contains(row)));
        var retained = candidates.Where(row => row.State == HistoryOwnerState.Linked && row.Role != HistoryOwnerRole.Lineage).ToArray();
        var retainedEntries = retained.Select(row => row.EntryId).ToHashSet();
        var retainedContent = retained.Where(row => row.Role == HistoryOwnerRole.ContentOwner).Select(row => row.EntryId).ToHashSet();
        var entries = await db.Set<HistoryEntryRow>().Where(row => ids.Contains(row.Id)).ToDictionaryAsync(row => row.Id, cancellationToken);
        foreach (var local in db.Set<HistoryEntryRow>().Local.Where(row => ids.Contains(row.Id))) {
            entries.TryAdd(local.Id, local);
        }
        foreach (var owner in owners) {
            owner.State = HistoryOwnerState.Deleted;
            var entry = entries[owner.EntryId];
            if (retainedEntries.Contains(owner.EntryId)) {
                if (!retainedContent.Contains(owner.EntryId) && entry.DetailState == HistoryDetailState.Canonical) {
                    entry.DetailState = HistoryDetailState.Unavailable;
                }
                continue;
            }
            if (entry.MetadataAuthority == HistoryMetadataAuthority.CanonicalProjection && owner.Role != HistoryOwnerRole.Lineage) {
                entry.IsVisible = false;
                entry.DetailState = HistoryDetailState.Deleted;
            }
        }
    }
}
