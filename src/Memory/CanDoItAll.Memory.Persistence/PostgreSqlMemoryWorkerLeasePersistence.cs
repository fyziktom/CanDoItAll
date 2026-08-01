using System.Data;
using System.Data.Common;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Memory.Persistence.Hosting;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Memory.Persistence;

internal static class PostgreSqlMemoryWorkerLeasePersistence
{
    public static async Task<MemoryWorkerLease?> TryAcquireAsync(
        AppDbContext dbContext,
        MemoryBackgroundWorkerPhase phase,
        MemoryWorkerLeaseOwnerId ownerId,
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        var token = MemoryWorkerLeaseToken.New();
        var expiresAtUtc = nowUtc.Add(leaseDuration);
        var connection = dbContext.Database.GetDbConnection();
        var closeConnection = connection.State != ConnectionState.Open;
        if (closeConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                INSERT INTO "Memory_WorkerLeases" AS lease
                    ("Phase", "OwnerId", "LeaseToken", "LeaseExpiresAtUtc", "UpdatedAtUtc")
                VALUES (@phase, @ownerId, @leaseToken, @leaseExpiresAtUtc, @nowUtc)
                ON CONFLICT ("Phase") DO UPDATE
                SET "OwnerId" = EXCLUDED."OwnerId",
                    "LeaseToken" = EXCLUDED."LeaseToken",
                    "LeaseExpiresAtUtc" = EXCLUDED."LeaseExpiresAtUtc",
                    "UpdatedAtUtc" = EXCLUDED."UpdatedAtUtc"
                WHERE lease."LeaseExpiresAtUtc" <= @nowUtc
                RETURNING "LeaseToken";
                """;
            AddParameter(command, "@phase", (int)phase);
            AddParameter(command, "@ownerId", ownerId.Value);
            AddParameter(command, "@leaseToken", token.Value);
            AddParameter(command, "@leaseExpiresAtUtc", expiresAtUtc);
            AddParameter(command, "@nowUtc", nowUtc);
            var acquiredToken = await command.ExecuteScalarAsync(cancellationToken);
            return acquiredToken is Guid value && value == token.Value
                ? new MemoryWorkerLease(phase, ownerId, token, expiresAtUtc)
                : null;
        }
        finally
        {
            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    public static async Task<bool> RenewAsync(
        AppDbContext dbContext,
        MemoryWorkerLease lease,
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        var query = MemoryWorkerLeasePersistenceRules.OwnedQuery(
            dbContext,
            lease,
            nowUtc,
            requireUnexpired: true);
        return await query.ExecuteUpdateAsync(setters => setters
            .SetProperty(item => item.LeaseExpiresAtUtc, nowUtc.Add(leaseDuration))
            .SetProperty(item => item.UpdatedAtUtc, nowUtc), cancellationToken) == 1;
    }

    public static Task<bool> CompleteAsync(
        AppDbContext dbContext,
        MemoryWorkerLease lease,
        DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken) =>
        ReleaseAsync(dbContext, lease, completedAtUtc, requireUnexpired: true, cancellationToken);

    public static Task<bool> ReleaseAsync(
        AppDbContext dbContext,
        MemoryWorkerLease lease,
        DateTimeOffset releasedAtUtc,
        CancellationToken cancellationToken) =>
        ReleaseAsync(dbContext, lease, releasedAtUtc, requireUnexpired: false, cancellationToken);

    private static async Task<bool> ReleaseAsync(
        AppDbContext dbContext,
        MemoryWorkerLease lease,
        DateTimeOffset releasedAtUtc,
        bool requireUnexpired,
        CancellationToken cancellationToken)
    {
        var query = MemoryWorkerLeasePersistenceRules.OwnedQuery(
            dbContext,
            lease,
            releasedAtUtc,
            requireUnexpired);
        return await query.ExecuteUpdateAsync(setters => setters
            .SetProperty(item => item.OwnerId, string.Empty)
            .SetProperty(item => item.LeaseToken, Guid.Empty)
            .SetProperty(item => item.LeaseExpiresAtUtc, MemoryWorkerLeasePersistenceRules.ReleasedAtUtc)
            .SetProperty(item => item.UpdatedAtUtc, releasedAtUtc), cancellationToken) == 1;
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
