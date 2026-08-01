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
            var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            return new(transaction, processLocks: null);
        }

        if (!string.Equals(
                dbContext.Database.ProviderName,
                "Microsoft.EntityFrameworkCore.InMemory",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Serializable mutations are not supported by database provider '{dbContext.Database.ProviderName ?? "unknown"}'.");
        }

        var locks = normalizedScopeKeys
            .Select(static scopeKey =>
                (scopeKey.GetHashCode(StringComparison.Ordinal) & int.MaxValue) %
                InMemoryLockStripeCount)
            .Distinct()
            .Order()
            .Select(static stripeIndex => InMemoryLockStripes[stripeIndex])
            .ToArray();
        var acquiredLocks = new List<SemaphoreSlim>(locks.Length);
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
            for (var index = acquiredLocks.Count - 1; index >= 0; index--)
            {
                acquiredLocks[index].Release();
            }

            throw;
        }

        return new(transaction: null, locks);
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

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        if (transaction is not null)
        {
            await transaction.DisposeAsync();
        }

        for (var index = processLocks.Count - 1; index >= 0; index--)
        {
            processLocks[index].Release();
        }
    }
}
