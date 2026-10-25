using System.Data;
using CanDoItAll.Processes.Application;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Processes.Persistence;

public sealed class EfProcessCancellationRecoveryQueue(IDbContextFactory<ProcessPersistenceDbContext> factory) : IProcessCancellationRecoveryQueue {
    public async Task<ProcessCancellationRecoveryClaim?> ClaimAsync(ProcessCancellationRecoveryOrigin origin, TimeSpan leaseDuration,
        CancellationToken cancellationToken = default) {
        if (!Enum.IsDefined(origin) || leaseDuration < TimeSpan.FromSeconds(1) || leaseDuration > TimeSpan.FromMinutes(15)) {
            throw new ArgumentOutOfRangeException(nameof(leaseDuration), "Recovery requires a supported origin and a lease between one second and fifteen minutes.");
        }
        await using var context = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken).ConfigureAwait(false);
        var selected = await context.CancellationRecoveries.FromSqlInterpolated($"""
            SELECT * FROM process_cancellation_recovery
            WHERE "Origin" = {(int)origin} AND NOT "Completed" AND "DueAtUtc" <= statement_timestamp()
            ORDER BY "DueAtUtc", "RootRunId" LIMIT 1 FOR UPDATE SKIP LOCKED
            """).AsNoTracking().ToArrayAsync(cancellationToken).ConfigureAwait(false);
        if (selected.Length == 0) {
            return null;
        }
        var root = selected[0].RootRunId;
        var token = Guid.NewGuid();
        var changed = await context.CancellationRecoveries.FromSqlInterpolated($"""
            WITH moment AS MATERIALIZED (SELECT clock_timestamp() AS now)
            UPDATE process_cancellation_recovery SET "LeaseToken" = {token}, "LeaseGeneration" = "LeaseGeneration" + 1,
                "LeaseExpiresAtUtc" = moment.now + {leaseDuration}, "DueAtUtc" = moment.now + {leaseDuration}, "UpdatedAtUtc" = moment.now
            FROM moment WHERE "RootRunId" = {root} RETURNING process_cancellation_recovery.*
            """).AsNoTracking().ToArrayAsync(cancellationToken).ConfigureAwait(false);
        ProcessCancellationRecoveryClaim claim;
        try {
            claim = ProcessCancellationRecoveryCodec.ReadClaim(changed.Single());
            if (claim.Position.CoverageVersion < ProcessCancellationRecoveryPosition.CurrentCoverageVersion && claim.Intent is null) {
                var initial = ProcessCancellationRecoveryCodec.WritePosition(ProcessCancellationRecoveryPosition.Initial);
                var upgraded = await context.CancellationRecoveries.FromSqlInterpolated($"""
                    UPDATE process_cancellation_recovery SET "PositionJson" = {initial}, "Pending" = 0,
                        "ProgressRevision" = "ProgressRevision" + 1, "Cycle" = "Cycle" + 1
                    WHERE "RootRunId" = {root} AND "LeaseToken" = {token} RETURNING *
                    """).AsNoTracking().ToArrayAsync(cancellationToken).ConfigureAwait(false);
                claim = ProcessCancellationRecoveryCodec.ReadClaim(upgraded.Single());
            }
        } catch (InvalidDataException error) {
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                WITH moment AS MATERIALIZED (SELECT clock_timestamp() AS now)
                UPDATE process_cancellation_recovery SET "LeaseToken" = NULL, "LeaseExpiresAtUtc" = NULL,
                    "Pending" = {(int)ProcessCancellationRecoveryPending.InvalidEvidence}, "ProgressRevision" = "ProgressRevision" + 1,
                    "UpdatedAtUtc" = moment.now, "DueAtUtc" = moment.now + {TimeSpan.FromMinutes(1)}
                FROM moment WHERE "RootRunId" = {root}
                """, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            throw new ProcessCancellationRecoveryCorruptRecordException(new(root), error);
        }
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return claim;
    }

    public async Task<ProcessCancellationCheckpointResult> CheckpointAsync(ProcessCancellationRecoveryClaim claim,
        ProcessCancellationRecoveryCheckpoint checkpoint, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(claim);
        ArgumentNullException.ThrowIfNull(checkpoint);
        claim.Validate();
        await using var context = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken).ConfigureAwait(false);
        var rows = await context.CancellationRecoveries.FromSqlInterpolated($"""
            SELECT * FROM process_cancellation_recovery WHERE "RootRunId" = {claim.RootRunId.Value} FOR UPDATE SKIP LOCKED
            """).AsNoTracking().ToArrayAsync(cancellationToken).ConfigureAwait(false);
        var row = rows.SingleOrDefault();
        if (row is null || row.Completed || row.LeaseToken != claim.LeaseToken || row.LeaseGeneration != claim.LeaseGeneration ||
                row.ProgressRevision != claim.ProgressRevision) {
            return new(ProcessCancellationCheckpointStatus.NotOwned);
        }
        var saved = ProcessCancellationRecoveryCodec.ReadClaim(row);
        checkpoint.Validate(saved);
        var upgrade = saved.Position.CoverageVersion < ProcessCancellationRecoveryPosition.CurrentCoverageVersion &&
            saved.Intent is not null && checkpoint.Intent is null;
        if (upgrade) {
            checkpoint = new(ProcessCancellationRecoveryPosition.Initial, RetryAfter: TimeSpan.Zero);
        }
        var position = ProcessCancellationRecoveryCodec.WritePosition(checkpoint.Position);
        var intent = ProcessCancellationRecoveryCodec.WriteIntent(saved.RootRunId, checkpoint.Intent);
        var completed = checkpoint.Position.Stage == ProcessCancellationRecoveryStage.Completed;
        var release = completed || checkpoint.RetryAfter is not null;
        var delay = checkpoint.RetryAfter ?? TimeSpan.Zero;
        var cycle = checked(saved.Cycle + (upgrade || saved.Position.Stage == ProcessCancellationRecoveryStage.Quiescence &&
            checkpoint.Position.Stage == ProcessCancellationRecoveryStage.Runtime ? 1 : 0));
        var changed = await context.CancellationRecoveries.FromSqlInterpolated($"""
            WITH moment AS MATERIALIZED (SELECT clock_timestamp() AS now)
            UPDATE process_cancellation_recovery SET "PositionJson" = {position}, "IntentJson" = {intent}, "Pending" = {(int)checkpoint.Pending},
                "Completed" = {completed}, "Cycle" = {cycle}, "ProgressRevision" = "ProgressRevision" + 1, "UpdatedAtUtc" = moment.now,
                "LeaseToken" = CASE WHEN {release} THEN NULL ELSE "LeaseToken" END,
                "LeaseExpiresAtUtc" = CASE WHEN {release} THEN NULL ELSE "LeaseExpiresAtUtc" END,
                "DueAtUtc" = CASE WHEN {release} THEN moment.now + {delay} ELSE "LeaseExpiresAtUtc" END
            FROM moment WHERE "RootRunId" = {claim.RootRunId.Value} AND "LeaseToken" = {claim.LeaseToken}
                AND "LeaseGeneration" = {claim.LeaseGeneration} AND "ProgressRevision" = {claim.ProgressRevision}
                AND "LeaseExpiresAtUtc" > moment.now AND NOT "Completed"
            RETURNING process_cancellation_recovery.*
            """).AsNoTracking().ToArrayAsync(cancellationToken).ConfigureAwait(false);
        if (changed.Length == 0) {
            return new(ProcessCancellationCheckpointStatus.NotOwned);
        }
        var next = release ? null : ProcessCancellationRecoveryCodec.ReadClaim(changed.Single());
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new(release ? ProcessCancellationCheckpointStatus.Released : ProcessCancellationCheckpointStatus.Applied, next);
    }

    public async Task<ProcessCancellationSeedResult> SeedLegacyAsync(int take = 16, CancellationToken cancellationToken = default) {
        if (take is < 1 or > 64) {
            throw new ArgumentOutOfRangeException(nameof(take), "A legacy recovery page must contain between one and sixty-four roots.");
        }
        await using var context = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken).ConfigureAwait(false);
        var rows = await context.CancellationRecoverySeeds.FromSqlInterpolated($"""
            SELECT * FROM process_cancellation_recovery_seed WHERE "Id" = {ProcessCancellationRecoverySeedEntity.SingletonId} FOR UPDATE SKIP LOCKED
            """).AsNoTracking().ToArrayAsync(cancellationToken).ConfigureAwait(false);
        if (rows.Length == 0) {
            if (!await context.CancellationRecoverySeeds.AnyAsync(cancellationToken).ConfigureAwait(false)) {
                throw new InvalidDataException("The durable cancellation recovery seed checkpoint is missing.");
            }
            return new(ProcessCancellationSeedStatus.Busy, 0, 0);
        }
        var saved = rows.Single();
        if (saved.Completed) {
            return new(ProcessCancellationSeedStatus.Completed, 0, 0);
        }
        FormattableString selection = saved.LastRootRunId is { } after ? (FormattableString)$"""
            SELECT "RunId" AS "Value" FROM process_runtime_states
            WHERE "RootRunId" = "RunId" AND "Status" IN ('CancelRequested', 'Cancelled') AND "RunId" > {after}
            ORDER BY "RunId" LIMIT {take}
            """ : (FormattableString)$"""
            SELECT "RunId" AS "Value" FROM process_runtime_states
            WHERE "RootRunId" = "RunId" AND "Status" IN ('CancelRequested', 'Cancelled') ORDER BY "RunId" LIMIT {take}
            """;
        var roots = await context.Database.SqlQuery<Guid>(selection).ToArrayAsync(cancellationToken).ConfigureAwait(false);
        var inserted = 0;
        var reopened = 0;
        if (roots.Length > 0) {
            var position = ProcessCancellationRecoveryCodec.WritePosition(ProcessCancellationRecoveryPosition.Initial);
            inserted = await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO process_cancellation_recovery
                    ("RootRunId", "Origin", "LeaseGeneration", "ProgressRevision", "Cycle", "PositionJson", "Pending", "Completed")
                SELECT root, {(int)ProcessCancellationRecoveryOrigin.LegacySeed}, 0, 0, 0, {position}, {(int)ProcessCancellationRecoveryPending.None}, FALSE
                FROM unnest({roots}) AS root ON CONFLICT ("RootRunId") DO NOTHING
                """, cancellationToken).ConfigureAwait(false);
            var historical = await context.CancellationRecoveries.FromSqlInterpolated($"""
                SELECT * FROM process_cancellation_recovery WHERE "RootRunId" = ANY({roots}) AND "Completed"
                ORDER BY "RootRunId" LIMIT {take} FOR UPDATE
                """).AsNoTracking().ToArrayAsync(cancellationToken).ConfigureAwait(false);
            foreach (var candidate in historical) {
                ProcessCancellationRecoveryPosition previousPosition;
                try {
                    previousPosition = ProcessCancellationRecoveryCodec.ReadPosition(candidate.PositionJson);
                    if (candidate.IntentJson is not null || candidate.LeaseToken is not null || candidate.LeaseExpiresAtUtc is not null ||
                            candidate.Pending != ProcessCancellationRecoveryPending.None || previousPosition.Stage != ProcessCancellationRecoveryStage.Completed) {
                        throw new InvalidDataException("Historical completed recovery has inconsistent retained evidence.");
                    }
                } catch (InvalidDataException failure) {
                    throw new ProcessCancellationRecoveryCorruptRecordException(new(candidate.RootRunId), failure);
                }
                if (previousPosition.CoverageVersion == ProcessCancellationRecoveryPosition.CurrentCoverageVersion) {
                    continue;
                }
                reopened += await context.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE process_cancellation_recovery SET "Completed" = FALSE, "PositionJson" = {position},
                        "ProgressRevision" = "ProgressRevision" + 1, "Cycle" = "Cycle" + 1,
                        "UpdatedAtUtc" = clock_timestamp(), "DueAtUtc" = clock_timestamp()
                    WHERE "RootRunId" = {candidate.RootRunId} AND "PositionJson" = {candidate.PositionJson} AND "Completed"
                    """, cancellationToken).ConfigureAwait(false);
            }
        }
        var last = roots.Length == 0 ? saved.LastRootRunId : roots[^1];
        var completed = roots.Length == 0;
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE process_cancellation_recovery_seed SET "LastRootRunId" = {last}, "Completed" = {completed}, "Revision" = "Revision" + 1
            WHERE "Id" = {ProcessCancellationRecoverySeedEntity.SingletonId}
            """, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new(completed ? ProcessCancellationSeedStatus.Completed : ProcessCancellationSeedStatus.Advanced, roots.Length, inserted, reopened);
    }

    private async Task<ProcessPersistenceDbContext> OpenAsync(CancellationToken cancellationToken) {
        var context = await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        if (!context.Database.IsNpgsql()) {
            await context.DisposeAsync().ConfigureAwait(false);
            throw new InvalidOperationException("Durable cancellation recovery requires PostgreSQL database leases.");
        }
        context.Database.SetCommandTimeout(5);
        return context;
    }
}
