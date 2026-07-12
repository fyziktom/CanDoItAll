using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Memory.Persistence.Hosting;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Memory.Persistence;

internal static class InMemoryMemoryWorkerLeasePersistence
{
    public static Task<MemoryWorkerLease?> TryAcquireAsync(
        AppDbContext dbContext,
        MemoryBackgroundWorkerPhase phase,
        MemoryWorkerLeaseOwnerId ownerId,
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        MemoryWorkerInMemoryLeaseRegistry registry,
        CancellationToken cancellationToken) =>
        registry.ExecuteAsync<MemoryWorkerLease?>(
            async leases =>
            {
                if (leases.TryGetValue(phase, out var active) && active.ExpiresAtUtc > nowUtc)
                {
                    return null;
                }

                var entity = await dbContext.Set<MemoryWorkerLeaseEntity>()
                    .SingleOrDefaultAsync(item => item.Phase == (int)phase, cancellationToken);
                if (entity is not null && entity.LeaseExpiresAtUtc > nowUtc)
                {
                    leases[phase] = ToLease(entity, phase);
                    return null;
                }

                var token = MemoryWorkerLeaseToken.New();
                var expiresAtUtc = nowUtc.Add(leaseDuration);
                if (entity is null)
                {
                    entity = new MemoryWorkerLeaseEntity { Phase = (int)phase };
                    dbContext.Add(entity);
                }

                entity.OwnerId = ownerId.Value;
                entity.LeaseToken = token.Value;
                entity.LeaseExpiresAtUtc = expiresAtUtc;
                entity.UpdatedAtUtc = nowUtc;
                await dbContext.SaveChangesAsync(cancellationToken);
                var acquired = new MemoryWorkerLease(phase, ownerId, token, expiresAtUtc);
                leases[phase] = acquired;
                return acquired;
            },
            cancellationToken);

    public static Task<bool> RenewAsync(
        AppDbContext dbContext,
        MemoryWorkerLease lease,
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        MemoryWorkerInMemoryLeaseRegistry registry,
        CancellationToken cancellationToken) =>
        UpdateOwnedAsync(
            dbContext,
            lease,
            nowUtc,
            requireUnexpired: true,
            registry,
            entity =>
            {
                entity.LeaseExpiresAtUtc = nowUtc.Add(leaseDuration);
                entity.UpdatedAtUtc = nowUtc;
            },
            persisted => persisted with { ExpiresAtUtc = nowUtc.Add(leaseDuration) },
            cancellationToken);

    public static Task<bool> CompleteAsync(
        AppDbContext dbContext,
        MemoryWorkerLease lease,
        DateTimeOffset completedAtUtc,
        MemoryWorkerInMemoryLeaseRegistry registry,
        CancellationToken cancellationToken) =>
        ReleaseAsync(dbContext, lease, completedAtUtc, requireUnexpired: true, registry, cancellationToken);

    public static Task<bool> ReleaseAsync(
        AppDbContext dbContext,
        MemoryWorkerLease lease,
        DateTimeOffset releasedAtUtc,
        MemoryWorkerInMemoryLeaseRegistry registry,
        CancellationToken cancellationToken) =>
        ReleaseAsync(dbContext, lease, releasedAtUtc, requireUnexpired: false, registry, cancellationToken);

    private static Task<bool> ReleaseAsync(
        AppDbContext dbContext,
        MemoryWorkerLease lease,
        DateTimeOffset releasedAtUtc,
        bool requireUnexpired,
        MemoryWorkerInMemoryLeaseRegistry registry,
        CancellationToken cancellationToken) =>
        UpdateOwnedAsync(
            dbContext,
            lease,
            releasedAtUtc,
            requireUnexpired,
            registry,
            entity => MemoryWorkerLeasePersistenceRules.Release(entity, releasedAtUtc),
            _ => null,
            cancellationToken);

    private static Task<bool> UpdateOwnedAsync(
        AppDbContext dbContext,
        MemoryWorkerLease lease,
        DateTimeOffset nowUtc,
        bool requireUnexpired,
        MemoryWorkerInMemoryLeaseRegistry registry,
        Action<MemoryWorkerLeaseEntity> update,
        Func<MemoryWorkerLease, MemoryWorkerLease?> updateRegistry,
        CancellationToken cancellationToken) =>
        registry.ExecuteAsync(
            async leases =>
            {
                var entity = await MemoryWorkerLeasePersistenceRules.OwnedQuery(
                        dbContext,
                        lease,
                        nowUtc,
                        requireUnexpired)
                    .SingleOrDefaultAsync(cancellationToken);
                if (entity is null)
                {
                    return false;
                }

                update(entity);
                await dbContext.SaveChangesAsync(cancellationToken);
                var next = updateRegistry(lease);
                if (next is null)
                {
                    leases.Remove(lease.Phase);
                }
                else
                {
                    leases[lease.Phase] = next;
                }

                return true;
            },
            cancellationToken);

    private static MemoryWorkerLease ToLease(
        MemoryWorkerLeaseEntity entity,
        MemoryBackgroundWorkerPhase phase) =>
        new(
            phase,
            MemoryWorkerLeaseOwnerId.Parse(entity.OwnerId),
            MemoryWorkerLeaseToken.Parse(entity.LeaseToken),
            entity.LeaseExpiresAtUtc);
}
