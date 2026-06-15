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
