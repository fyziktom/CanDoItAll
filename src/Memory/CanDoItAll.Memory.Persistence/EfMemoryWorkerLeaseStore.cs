using System.Collections.Concurrent;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Memory.Persistence.Hosting;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Memory.Persistence;

internal sealed class EfMemoryWorkerLeaseStore(
    IDbContextFactory<AppDbContext> dbContextFactory,
    MemoryWorkerInMemoryLeaseRegistry inMemoryLeaseRegistry) : IMemoryWorkerLeaseStore
{
    private static readonly ConcurrentDictionary<MemoryBackgroundWorkerPhase, SemaphoreSlim> AcquireGates = new();

    public async Task<MemoryWorkerLease?> TryAcquireAsync(
        MemoryBackgroundWorkerPhase phase,
        MemoryWorkerLeaseOwnerId ownerId,
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        MemoryWorkerLeasePersistenceRules.ValidateDuration(leaseDuration);
        var gate = AcquireGates.GetOrAdd(phase, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            return dbContext.Database.ProviderName switch
            {
                MemoryWorkerLeasePersistenceRules.PostgreSqlProviderName =>
                    await PostgreSqlMemoryWorkerLeasePersistence.TryAcquireAsync(
                        dbContext,
                        phase,
                        ownerId,
                        nowUtc,
                        leaseDuration,
                        cancellationToken),
                MemoryWorkerLeasePersistenceRules.InMemoryProviderName =>
                    await InMemoryMemoryWorkerLeasePersistence.TryAcquireAsync(
                        dbContext,
                        phase,
                        ownerId,
                        nowUtc,
                        leaseDuration,
                        inMemoryLeaseRegistry,
                        cancellationToken),
                var provider => throw MemoryWorkerLeasePersistenceRules.UnsupportedProvider(provider)
            };
        }
        finally
        {
            gate.Release();
        }
    }

    public Task<bool> RenewAsync(
        MemoryWorkerLease lease,
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lease);
        MemoryWorkerLeasePersistenceRules.ValidateDuration(leaseDuration);
        return ExecuteForProviderAsync(
            (dbContext, token) => PostgreSqlMemoryWorkerLeasePersistence.RenewAsync(
                dbContext,
                lease,
                nowUtc,
                leaseDuration,
                token),
            (dbContext, token) => InMemoryMemoryWorkerLeasePersistence.RenewAsync(
                dbContext,
                lease,
                nowUtc,
                leaseDuration,
                inMemoryLeaseRegistry,
                token),
            cancellationToken);
    }

    public Task<bool> CompleteAsync(
        MemoryWorkerLease lease,
        DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lease);
        return ExecuteForProviderAsync(
            (dbContext, token) => PostgreSqlMemoryWorkerLeasePersistence.CompleteAsync(
                dbContext,
                lease,
                completedAtUtc,
                token),
            (dbContext, token) => InMemoryMemoryWorkerLeasePersistence.CompleteAsync(
                dbContext,
                lease,
                completedAtUtc,
                inMemoryLeaseRegistry,
                token),
            cancellationToken);
    }

    public Task<bool> ReleaseAsync(
        MemoryWorkerLease lease,
        DateTimeOffset releasedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lease);
        return ExecuteForProviderAsync(
            (dbContext, token) => PostgreSqlMemoryWorkerLeasePersistence.ReleaseAsync(
                dbContext,
                lease,
                releasedAtUtc,
                token),
            (dbContext, token) => InMemoryMemoryWorkerLeasePersistence.ReleaseAsync(
                dbContext,
                lease,
                releasedAtUtc,
                inMemoryLeaseRegistry,
                token),
            cancellationToken);
    }

    private async Task<bool> ExecuteForProviderAsync(
        Func<AppDbContext, CancellationToken, Task<bool>> executePostgreSql,
        Func<AppDbContext, CancellationToken, Task<bool>> executeInMemory,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return dbContext.Database.ProviderName switch
        {
            MemoryWorkerLeasePersistenceRules.PostgreSqlProviderName =>
                await executePostgreSql(dbContext, cancellationToken),
            MemoryWorkerLeasePersistenceRules.InMemoryProviderName =>
                await executeInMemory(dbContext, cancellationToken),
            var provider => throw MemoryWorkerLeasePersistenceRules.UnsupportedProvider(provider)
        };
    }
}
