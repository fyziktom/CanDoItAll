using System.Data;
using CanDoItAll.Processes.Application;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Processes.Persistence;

public sealed class EfProcessDecisionRefreshDeliveryQueue(IDbContextFactory<ProcessPersistenceDbContext> factory) : IProcessDecisionRefreshDeliveryQueue {
    private static readonly TimeSpan OperationLimit = TimeSpan.FromSeconds(5);

    public async Task<ProcessDecisionRefreshDeliveryClaim?> ClaimAsync(TimeSpan leaseDuration, CancellationToken cancellationToken = default) {
        if (leaseDuration < TimeSpan.FromSeconds(1) || leaseDuration > TimeSpan.FromMinutes(15)) {
            throw new ArgumentOutOfRangeException(nameof(leaseDuration), "Refresh delivery requires a lease between one second and fifteen minutes.");
        }
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(OperationLimit);
        var token = deadline.Token;
        await using var context = await OpenAsync(token).ConfigureAwait(false);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, token).ConfigureAwait(false);
        await SetLimitsAsync(context, token).ConfigureAwait(false);
        var lease = Guid.NewGuid();
        var rows = await context.RefreshDeliveries.FromSqlInterpolated($"""
            WITH selected AS MATERIALIZED (
                SELECT "RunId", "AdmissionCommandId" FROM process_decision_refresh_delivery
                WHERE "Disposition" = 0 AND "DueAtUtc" <= statement_timestamp()
                ORDER BY "DueAtUtc", "RunId", "AdmissionCommandId" LIMIT 1 FOR UPDATE SKIP LOCKED
            ), moment AS MATERIALIZED (SELECT clock_timestamp() AS now)
            UPDATE process_decision_refresh_delivery AS delivery SET "LeaseToken" = {lease},
                "LeaseGeneration" = "LeaseGeneration" + 1, "LeaseExpiresAtUtc" = moment.now + {leaseDuration},
                "DueAtUtc" = moment.now + {leaseDuration}, "UpdatedAtUtc" = moment.now
            FROM selected, moment WHERE delivery."RunId" = selected."RunId" AND delivery."AdmissionCommandId" = selected."AdmissionCommandId"
            RETURNING delivery.*
            """).AsNoTracking().ToArrayAsync(token).ConfigureAwait(false);
        var claim = rows.SingleOrDefault()?.ReadClaim();
        await transaction.CommitAsync(token).ConfigureAwait(false);
        return claim;
    }

    public async Task<ProcessDecisionRefreshDeliveryCheckpointStatus> CheckpointAsync(ProcessDecisionRefreshDeliveryClaim claim,
        ProcessDecisionRefreshDeliveryCheckpoint checkpoint, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(claim);
        ArgumentNullException.ThrowIfNull(checkpoint);
        claim.Validate();
        checkpoint.Validate();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(OperationLimit);
        var token = deadline.Token;
        await using var context = await OpenAsync(token).ConfigureAwait(false);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, token).ConfigureAwait(false);
        await SetLimitsAsync(context, token).ConfigureAwait(false);
        var rows = await context.RefreshDeliveries.FromSqlInterpolated($"""
            SELECT * FROM process_decision_refresh_delivery
            WHERE "RunId" = {claim.RunId.Value} AND "AdmissionCommandId" = {claim.AdmissionId.Value} FOR UPDATE SKIP LOCKED
            """).AsNoTracking().ToArrayAsync(token).ConfigureAwait(false);
        var row = rows.SingleOrDefault();
        if (row is null || row.Disposition != ProcessDecisionRefreshDeliveryDisposition.Ready || row.LeaseToken != claim.LeaseToken ||
                row.RootRunId != claim.RootRunId.Value || row.LeaseGeneration != claim.LeaseGeneration ||
                row.ProgressRevision != claim.ProgressRevision || row.WakeRevision < claim.WakeRevision) {
            return ProcessDecisionRefreshDeliveryCheckpointStatus.NotOwned;
        }
        _ = row.ReadClaim();
        var woken = row.WakeRevision > claim.WakeRevision && checkpoint.Disposition is
            ProcessDecisionRefreshDeliveryDisposition.Ready or ProcessDecisionRefreshDeliveryDisposition.AwaitingCapture;
        var disposition = woken ? ProcessDecisionRefreshDeliveryDisposition.Ready : checkpoint.Disposition;
        var reason = woken ? ProcessDecisionRefreshDeliveryReason.Admitted : checkpoint.Reason;
        var delay = woken ? TimeSpan.Zero : checkpoint.RetryAfter ?? TimeSpan.Zero;
        var changed = await context.Database.ExecuteSqlInterpolatedAsync($"""
            WITH moment AS MATERIALIZED (SELECT clock_timestamp() AS now)
            UPDATE process_decision_refresh_delivery SET "Disposition" = {(int)disposition}, "Reason" = {(int)reason},
                "ProgressRevision" = "ProgressRevision" + 1, "LeaseToken" = NULL, "LeaseExpiresAtUtc" = NULL,
                "DueAtUtc" = moment.now + {delay}, "UpdatedAtUtc" = moment.now
            FROM moment WHERE "RunId" = {claim.RunId.Value} AND "AdmissionCommandId" = {claim.AdmissionId.Value}
                AND "RootRunId" = {claim.RootRunId.Value} AND "LeaseToken" = {claim.LeaseToken}
                AND "LeaseGeneration" = {claim.LeaseGeneration} AND "ProgressRevision" = {claim.ProgressRevision}
                AND "LeaseExpiresAtUtc" > moment.now AND "Disposition" = 0
            """, token).ConfigureAwait(false);
        await transaction.CommitAsync(token).ConfigureAwait(false);
        return changed == 1 ? ProcessDecisionRefreshDeliveryCheckpointStatus.Released : ProcessDecisionRefreshDeliveryCheckpointStatus.NotOwned;
    }

    private async Task<ProcessPersistenceDbContext> OpenAsync(CancellationToken cancellationToken) {
        var context = await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        if (!context.Database.IsNpgsql()) {
            await context.DisposeAsync().ConfigureAwait(false);
            throw new NotSupportedException("Durable refresh delivery requires PostgreSQL.");
        }
        context.Database.SetCommandTimeout(2);
        return context;
    }

    private static Task SetLimitsAsync(ProcessPersistenceDbContext context, CancellationToken cancellationToken)
        => context.Database.ExecuteSqlRawAsync("SET LOCAL transaction_timeout = '5s'; SET LOCAL statement_timeout = '2s'; SET LOCAL lock_timeout = '1s'", cancellationToken);
}
