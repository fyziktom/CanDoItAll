using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace CanDoItAll.Infrastructure.Persistence;

public sealed class SerializableMutationScope : IAsyncDisposable
{
    private const int InMemoryLockStripeCount = 64;
    private static readonly SemaphoreSlim[] InMemoryLockStripes =
        Enumerable.Range(0, InMemoryLockStripeCount)
            .Select(static _ => new SemaphoreSlim(1, 1))
            .ToArray();

    private readonly IDbContextTransaction? transaction;
    private readonly IReadOnlyList<SemaphoreSlim> processLocks;
    private bool disposed;

    private SerializableMutationScope(
        IDbContextTransaction? transaction,
        IReadOnlyList<SemaphoreSlim>? processLocks)
    {
        this.transaction = transaction;
        this.processLocks = processLocks ?? [];
    }

    public static Task<SerializableMutationScope> BeginAsync(
        DbContext dbContext,
        string scopeKey,
        CancellationToken cancellationToken)
        => BeginAsync(dbContext, [scopeKey], cancellationToken);

    public static async Task<SerializableMutationScope> BeginAsync(
        DbContext dbContext,
        IReadOnlyCollection<string> scopeKeys,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(scopeKeys);
        var normalizedScopeKeys = scopeKeys
            .Select(scopeKey =>
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(scopeKey);
                return scopeKey;
            })
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (normalizedScopeKeys.Length == 0)
        {
            throw new ArgumentException(
                "At least one serializable mutation scope key is required.",
                nameof(scopeKeys));
        }

        if (dbContext.Database.IsRelational())
        {
            var relationalLocks = ResolveProcessLocks(normalizedScopeKeys);
            var acquiredRelationalLocks = await AcquireProcessLocksAsync(
                relationalLocks,
                cancellationToken);
            IDbContextTransaction? transaction = null;
            try {
                transaction = await dbContext.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);
                await AcquireRelationalScopeLocksAsync(
                    dbContext,
                    normalizedScopeKeys,
                    cancellationToken);
                return new(transaction, acquiredRelationalLocks);
            } catch (Exception acquisitionFailure) {
                try {
                    if (transaction is not null) {
                        await transaction.DisposeAsync();
                    }
                } catch (Exception cleanupFailure) {
                    throw new AggregateException(
                        "Mutation scope acquisition and transaction cleanup both failed.",
                        acquisitionFailure,
                        cleanupFailure);
                } finally {
                    ReleaseProcessLocks(acquiredRelationalLocks);
                }
                throw;
            }
        }

        if (!string.Equals(
                dbContext.Database.ProviderName,
                "Microsoft.EntityFrameworkCore.InMemory",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Serializable mutations are not supported by database provider '{dbContext.Database.ProviderName ?? "unknown"}'.");
        }

        var locks = ResolveProcessLocks(normalizedScopeKeys);
        var acquiredLocks = await AcquireProcessLocksAsync(locks, cancellationToken);
        return new(transaction: null, acquiredLocks);
    }

    public static async Task AcquireRelationalScopeLocksAsync(
        DbContext dbContext,
        IReadOnlyCollection<string> scopeKeys,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(scopeKeys);
        if (!string.Equals(
                dbContext.Database.ProviderName,
                "Npgsql.EntityFrameworkCore.PostgreSQL",
                StringComparison.Ordinal))
        {
            return;
        }

        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "PostgreSQL scoped mutations require an active transaction before an advisory lock can be acquired.");
        }

        foreach (var scopeKey in scopeKeys
                     .Select(scopeKey =>
                     {
                         ArgumentException.ThrowIfNullOrWhiteSpace(scopeKey);
                         return scopeKey;
                     })
                     .Distinct(StringComparer.Ordinal)
                     .Order(StringComparer.Ordinal))
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({scopeKey}, 0))",
                cancellationToken);
        }
    }

    public static async Task<bool> TryAcquireRelationalScopeLocksAsync(DbContext context,
        IReadOnlyList<string> orderedKeys, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(orderedKeys);
        if (!context.Database.IsNpgsql() || context.Database.CurrentTransaction is null) {
            throw new InvalidOperationException("Nonwaiting scope exclusion requires an active PostgreSQL transaction.");
        }
        string[] keys = orderedKeys.Take(1025).ToArray();
        if (keys.Length is 0 or > 1024 || keys.Length != orderedKeys.Count ||
            keys.Any(string.IsNullOrWhiteSpace) ||
            !keys.SequenceEqual(keys.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))) {
            throw new ArgumentException("Nonwaiting scope exclusion requires bounded, distinct, ordinal keys.");
        }
        // A false result retains any acquired locks until this transaction ends.
        return await context.Database.SqlQuery<bool>($"""
            SELECT bool_and(held.acquired) AS "Value"
            FROM (SELECT pg_try_advisory_xact_lock(hashtextextended(k.key, 0)) AS acquired
                  FROM unnest({keys}) WITH ORDINALITY AS k(key, position) ORDER BY k.position) AS held
            """).SingleAsync(cancellationToken);
    }

    private static IReadOnlyList<SemaphoreSlim> ResolveProcessLocks(
        IReadOnlyCollection<string> normalizedScopeKeys)
    {
        return normalizedScopeKeys
            .Select(static scopeKey =>
                (scopeKey.GetHashCode(StringComparison.Ordinal) & int.MaxValue) %
                InMemoryLockStripeCount)
            .Distinct()
            .Order()
            .Select(static stripeIndex => InMemoryLockStripes[stripeIndex])
            .ToArray();
    }

    private static async Task<IReadOnlyList<SemaphoreSlim>> AcquireProcessLocksAsync(
        IReadOnlyList<SemaphoreSlim> locks,
        CancellationToken cancellationToken)
    {
        var acquiredLocks = new List<SemaphoreSlim>(locks.Count);
        try
        {
            foreach (var processLock in locks)
            {
                await processLock.WaitAsync(cancellationToken);
                acquiredLocks.Add(processLock);
            }
        }
        catch
        {
            ReleaseProcessLocks(acquiredLocks);

            throw;
        }

        return acquiredLocks;
    }

    private static void ReleaseProcessLocks(IReadOnlyList<SemaphoreSlim> locks)
    {
        for (var index = locks.Count - 1; index >= 0; index--)
        {
            locks[index].Release();
        }
    }

    public Task CommitAsync(CancellationToken cancellationToken)
        => transaction?.CommitAsync(cancellationToken) ?? Task.CompletedTask;

    public static bool IsConflict(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbUpdateConcurrencyException ||
                current is PostgresException
                {
                    SqlState: PostgresErrorCodes.SerializationFailure or
                        PostgresErrorCodes.DeadlockDetected
                })
            {
                return true;
            }

            if (string.Equals(
                    current.GetType().FullName,
                    "Microsoft.Data.Sqlite.SqliteException",
                    StringComparison.Ordinal) &&
                current.GetType().GetProperty("SqliteErrorCode")?.GetValue(current)
                    is int sqliteErrorCode &&
                sqliteErrorCode is 5 or 6)
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsUniqueConstraintConflict(
        Exception exception,
        IReadOnlySet<string> constraintNames)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(constraintNames);
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException
                {
                    SqlState: PostgresErrorCodes.UniqueViolation,
                    ConstraintName: { } constraintName
                } &&
                constraintNames.Contains(constraintName))
            {
                return true;
            }
        }

        return false;
    }

    public async ValueTask DisposeAsync() {
        if (disposed) {
            return;
        }

        disposed = true;
        try {
            if (transaction is not null) {
                await transaction.DisposeAsync();
            }
        } finally {
            ReleaseProcessLocks(processLocks);
        }
    }
}
