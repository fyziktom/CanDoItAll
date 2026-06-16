using CanDoItAll.Processes.Projections;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Processes.Persistence;

public sealed class EfProcessProjectionStore(ProcessPersistenceDbContext dbContext) : IProcessProjectionStore
{
    public async Task UpsertSnapshotAsync(
        ProcessProjectionSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var existing = await dbContext.ProjectionSnapshots
            .FindAsync(new object[] { snapshot.ProjectorName.Value, snapshot.ProjectionKey.Value }, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            dbContext.ProjectionSnapshots.Add(new ProcessProjectionSnapshotEntity
            {
                ProjectorName = snapshot.ProjectorName.Value,
                ProjectionKey = snapshot.ProjectionKey.Value,
                SchemaVersion = snapshot.SchemaVersion,
                PayloadJson = snapshot.PayloadJson,
                PayloadHash = snapshot.PayloadHash,
                UpdatedAtUtc = snapshot.UpdatedAtUtc
            });
        }
        else
        {
            existing.SchemaVersion = snapshot.SchemaVersion;
            existing.PayloadJson = snapshot.PayloadJson;
            existing.PayloadHash = snapshot.PayloadHash;
            existing.UpdatedAtUtc = snapshot.UpdatedAtUtc;
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<ProcessProjectionSnapshot?> LoadSnapshotAsync(
        ProcessProjectorName projectorName,
        ProcessProjectionKey projectionKey,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.ProjectionSnapshots
            .AsNoTracking()
            .SingleOrDefaultAsync(
                snapshot => snapshot.ProjectorName == projectorName.Value && snapshot.ProjectionKey == projectionKey.Value,
                cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : ProcessPersistenceMappers.ToProjectionSnapshot(entity);
    }

    public async Task<IReadOnlyList<ProcessProjectionSnapshot>> ReadSnapshotsAsync(
        ProcessProjectorName projectorName,
        ProcessProjectionKeyPrefix projectionKeyPrefix,
        int take,
        CancellationToken cancellationToken = default)
    {
        if (take <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(take), take, "Projection snapshot read size must be positive.");
        }

        var rows = await dbContext.ProjectionSnapshots
            .AsNoTracking()
            .Where(snapshot =>
                snapshot.ProjectorName == projectorName.Value &&
                snapshot.ProjectionKey.StartsWith(projectionKeyPrefix.Value))
            .OrderByDescending(snapshot => snapshot.UpdatedAtUtc)
            .ThenBy(snapshot => snapshot.ProjectionKey)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var snapshots = new List<ProcessProjectionSnapshot>(rows.Count);
        foreach (var row in rows)
        {
            snapshots.Add(ProcessPersistenceMappers.ToProjectionSnapshot(row));
        }

        return snapshots;
    }

    public async Task AppendHistoryAsync(
        ProcessProjectionHistoryRecord history,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(history);

        var existing = await dbContext.ProjectionHistory
            .FindAsync(new object[] { history.ProjectorName.Value, history.ProjectionKey.Value, history.GlobalSequence }, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            dbContext.ProjectionHistory.Add(ProcessPersistenceMappers.ToHistoryEntity(history));
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ProcessProjectionHistoryRecord>> ReadHistoryAsync(
        ProcessProjectionHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.Take <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(query.Take), query.Take, "Projection history read size must be positive.");
        }

        if (query.Skip < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(query.Skip), query.Skip, "Projection history skip count cannot be negative.");
        }

        var rowsQuery = dbContext.ProjectionHistory
            .AsNoTracking()
            .Where(history =>
                history.ProjectorName == query.ProjectorName.Value &&
                history.OccurredAtUtc >= query.FromUtc &&
                history.OccurredAtUtc < query.ToUtc);

        if (query.RunId is { } runId)
        {
            rowsQuery = rowsQuery.Where(history => history.RunId == runId.Value);
        }

        if (query.AfterGlobalSequence is { } afterGlobalSequence)
        {
            rowsQuery = rowsQuery.Where(history => history.GlobalSequence > afterGlobalSequence);
        }

        var rows = await rowsQuery
            .OrderBy(history => history.GlobalSequence)
            .Skip(query.Skip)
            .Take(query.Take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var records = new List<ProcessProjectionHistoryRecord>(rows.Count);
        foreach (var row in rows)
        {
            records.Add(ProcessPersistenceMappers.ToHistoryRecord(row));
        }

        return records;
    }

    public async Task SaveOffsetAsync(
        ProcessProjectorOffset offset,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(offset);

        var entity = await dbContext.ProjectorOffsets
            .FindAsync(new object[] { offset.ProjectorName.Value, offset.ShardKey.Value }, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            dbContext.ProjectorOffsets.Add(new ProcessProjectorOffsetEntity
            {
                ProjectorName = offset.ProjectorName.Value,
                ShardKey = offset.ShardKey.Value,
                GlobalSequence = offset.GlobalSequence,
                UpdatedAtUtc = offset.UpdatedAtUtc
            });
        }
        else if (offset.GlobalSequence >= entity.GlobalSequence)
        {
            entity.GlobalSequence = offset.GlobalSequence;
            entity.UpdatedAtUtc = offset.UpdatedAtUtc;
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<ProcessProjectorOffset?> LoadOffsetAsync(
        ProcessProjectorName projectorName,
        ProcessProjectionShardKey shardKey,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.ProjectorOffsets
            .AsNoTracking()
            .SingleOrDefaultAsync(
                offset => offset.ProjectorName == projectorName.Value && offset.ShardKey == shardKey.Value,
                cancellationToken)
            .ConfigureAwait(false);

        return entity is null
            ? null
            : new ProcessProjectorOffset(
                new ProcessProjectorName(entity.ProjectorName),
                new ProcessProjectionShardKey(entity.ShardKey),
                entity.GlobalSequence,
                entity.UpdatedAtUtc);
    }

    public async Task WriteDeadLetterAsync(
        ProcessProjectionDeadLetter deadLetter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(deadLetter);

        dbContext.ProjectionDeadLetters.Add(new ProcessProjectionDeadLetterEntity
        {
            DeadLetterId = deadLetter.DeadLetterId.Value,
            ProjectorName = deadLetter.ProjectorName.Value,
            ShardKey = deadLetter.ShardKey.Value,
            EventId = deadLetter.EventId.Value,
            GlobalSequence = deadLetter.GlobalSequence,
            ErrorClass = deadLetter.ErrorClass,
            DiagnosticReference = deadLetter.DiagnosticReference,
            RetryPolicy = deadLetter.RetryPolicy,
            DeadLetteredAtUtc = deadLetter.DeadLetteredAtUtc
        });

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ProcessProjectionDeadLetter>> ReadDeadLettersAsync(
        ProcessProjectorName projectorName,
        ProcessProjectionShardKey shardKey,
        int take,
        CancellationToken cancellationToken = default)
    {
        if (take <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(take), take, "Dead-letter read size must be positive.");
        }

        var rows = await dbContext.ProjectionDeadLetters
            .AsNoTracking()
            .Where(deadLetter => deadLetter.ProjectorName == projectorName.Value && deadLetter.ShardKey == shardKey.Value)
            .OrderBy(deadLetter => deadLetter.GlobalSequence)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var results = new List<ProcessProjectionDeadLetter>(rows.Count);
        foreach (var row in rows)
        {
            results.Add(ProcessPersistenceMappers.ToDeadLetter(row));
        }

        return results;
    }
}
