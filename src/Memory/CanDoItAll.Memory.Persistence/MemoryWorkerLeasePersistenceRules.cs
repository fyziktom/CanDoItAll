using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Memory.Persistence.Hosting;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Memory.Persistence;

internal static class MemoryWorkerLeasePersistenceRules
{
    public const string InMemoryProviderName = "Microsoft.EntityFrameworkCore.InMemory";
    public const string PostgreSqlProviderName = "Npgsql.EntityFrameworkCore.PostgreSQL";
    public static readonly DateTimeOffset ReleasedAtUtc = DateTimeOffset.UnixEpoch;

    public static IQueryable<MemoryWorkerLeaseEntity> OwnedQuery(
        AppDbContext dbContext,
        MemoryWorkerLease lease,
        DateTimeOffset nowUtc,
        bool requireUnexpired)
    {
        var query = dbContext.Set<MemoryWorkerLeaseEntity>()
            .Where(item => item.Phase == (int)lease.Phase)
            .Where(item => item.OwnerId == lease.OwnerId.Value)
            .Where(item => item.LeaseToken == lease.Token.Value);
        return requireUnexpired
            ? query.Where(item => item.LeaseExpiresAtUtc > nowUtc)
            : query;
    }

    public static void Release(MemoryWorkerLeaseEntity entity, DateTimeOffset nowUtc)
    {
        entity.OwnerId = string.Empty;
        entity.LeaseToken = Guid.Empty;
        entity.LeaseExpiresAtUtc = ReleasedAtUtc;
        entity.UpdatedAtUtc = nowUtc;
    }

    public static void ValidateDuration(TimeSpan leaseDuration)
    {
        if (leaseDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(leaseDuration),
                "Memory worker lease duration must be positive.");
        }
    }

    public static NotSupportedException UnsupportedProvider(string? providerName) =>
        new(
            "Memory worker distributed leases require PostgreSQL or the process-local " +
            $"InMemory test provider. Provider '{providerName ?? "unknown"}' is not supported.");
}
