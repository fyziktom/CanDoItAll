using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Operations;
using CanDoItAll.Modules.LlmChats.Persistence.Entities;
using CanDoItAll.Modules.LlmChats.Ports;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.LlmChats.Persistence;

public sealed class DatabaseProfileLlmChatExecutionLeaseHeartbeatStore(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IDatabaseRuntimeWriteFence writeFence) : ILlmChatExecutionLeaseHeartbeatStore
{
    public Task<LlmChatExecutionLeaseObservation> RenewAndObserveAsync(
        LlmChatExecutionLeaseIdentity lease,
        LlmChatRuntimeIdentity runtimeIdentity,
        DateTimeOffset observedAtUtc,
        DateTimeOffset leaseExpiresAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (leaseExpiresAtUtc <= observedAtUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(leaseExpiresAtUtc),
                "An execution lease heartbeat must extend into the future.");
        }

        return ExecuteAsync(runtimeIdentity, async token =>
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(token).ConfigureAwait(false);
            var affected = await dbContext.Set<LlmChatOperationRow>()
                .Where(row => row.Id == lease.OperationId.Value &&
                              row.ExecutionOwnerId == lease.OwnerId.Value &&
                              row.ExecutionEpoch == lease.Epoch &&
                              row.LeaseExpiresAtUtc > observedAtUtc &&
                              (row.Status == LlmChatOperationStatus.Running ||
                               row.Status == LlmChatOperationStatus.CancellationRequested))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(row => row.HeartbeatAtUtc, observedAtUtc)
                    .SetProperty(row => row.LeaseExpiresAtUtc, leaseExpiresAtUtc)
                    .SetProperty(row => row.ConcurrencyToken, row => row.ConcurrencyToken + 1),
                    token).ConfigureAwait(false);
            return affected == 0
                ? new LlmChatExecutionLeaseObservation(false, false)
                : await ObserveCoreAsync(dbContext, lease, observedAtUtc, token).ConfigureAwait(false);
        }, cancellationToken);
    }

    public Task<LlmChatExecutionLeaseObservation> ObserveAsync(
        LlmChatExecutionLeaseIdentity lease,
        LlmChatRuntimeIdentity runtimeIdentity,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(runtimeIdentity, async token =>
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(token).ConfigureAwait(false);
            return await ObserveCoreAsync(dbContext, lease, observedAtUtc, token).ConfigureAwait(false);
        }, cancellationToken);

    private async Task<LlmChatExecutionLeaseObservation> ExecuteAsync(
        LlmChatRuntimeIdentity runtimeIdentity,
        Func<CancellationToken, Task<LlmChatExecutionLeaseObservation>> operation,
        CancellationToken cancellationToken)
    {
        try
        {
            return await writeFence.ExecuteAsync(
                new DatabaseRuntimeSnapshot(
                    runtimeIdentity.ProfileId,
                    runtimeIdentity.Fingerprint,
                    runtimeIdentity.Generation),
                operation,
                cancellationToken).ConfigureAwait(false);
        }
        catch (DatabaseRuntimeProfileChangedException)
        {
            throw new LlmChatRuntimeProfileChangedException();
        }
    }

    private static async Task<LlmChatExecutionLeaseObservation> ObserveCoreAsync(
        AppDbContext dbContext,
        LlmChatExecutionLeaseIdentity lease,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken)
    {
        var observation = await dbContext.Set<LlmChatOperationRow>()
            .AsNoTracking()
            .Where(row => row.Id == lease.OperationId.Value &&
                          row.ExecutionOwnerId == lease.OwnerId.Value &&
                          row.ExecutionEpoch == lease.Epoch &&
                          row.LeaseExpiresAtUtc > observedAtUtc &&
                          (row.Status == LlmChatOperationStatus.Running ||
                           row.Status == LlmChatOperationStatus.CancellationRequested))
            .Select(row => new
            {
                row.CancellationGeneration,
                row.Status
            })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return observation is null
            ? new LlmChatExecutionLeaseObservation(false, false)
            : new LlmChatExecutionLeaseObservation(
                true,
                observation.CancellationGeneration > 0 ||
                observation.Status == LlmChatOperationStatus.CancellationRequested);
    }
}
