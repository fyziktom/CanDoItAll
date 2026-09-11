using System.Data;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace CanDoItAll.Infrastructure.ControlPlane;

internal static class DatabaseTransferImportScope {
    internal static async Task<TResult> RunAsync<TResult>(DbContext context, IReadOnlyCollection<string> scopeKeys,
        IReadOnlyList<string> tableNames, Func<IDbContextTransaction, Task<TResult>> operation, CancellationToken cancellationToken) {
        if (context.Database.CurrentTransaction is not null) {
            throw new InvalidOperationException("Project import requires a fresh transaction before its exclusion locks.");
        }
        var keys = scopeKeys.Select(key => {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            return key;
        }).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (keys.Length == 0 || tableNames.Count == 0) {
            throw new ArgumentException("Project import requires explicit advisory keys and exclusion tables.");
        }

        var connection = (NpgsqlConnection)context.Database.GetDbConnection();
        var pendingSessionKeys = new List<string>();
        IDbContextTransaction? transaction = null;
        var opened = false;
        Exception? failure = null;
        try {
            await context.Database.OpenConnectionAsync(cancellationToken);
            opened = true;
            foreach (var key in keys) {
                pendingSessionKeys.Add(key);
                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT pg_advisory_lock(hashtextextended({key}, 0))", cancellationToken);
            }
            transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var tableLockCommand = $"LOCK TABLE {string.Join(", ", tableNames)} IN ACCESS EXCLUSIVE MODE NOWAIT";
            await context.Database.ExecuteSqlRawAsync(tableLockCommand, cancellationToken);
            await SerializableMutationScope.AcquireRelationalScopeLocksAsync(context, keys, cancellationToken);
            await ReleaseSessionKeysAsync(context, pendingSessionKeys, requireHeld: true);
            return await operation(transaction);
        } catch (Exception exception) {
            failure = exception;
            throw;
        } finally {
            var cleanupFailures = new List<Exception>();
            if (transaction is not null) {
                try {
                    await transaction.DisposeAsync();
                } catch (Exception exception) {
                    cleanupFailures.Add(exception);
                }
            }
            if (opened) {
                try {
                    await ReleaseSessionKeysAsync(context, pendingSessionKeys, requireHeld: false);
                } catch (Exception exception) {
                    cleanupFailures.Add(exception);
                }
                if (cleanupFailures.Count > 0) {
                    try {
                        NpgsqlConnection.ClearPool(connection);
                    } catch (Exception exception) {
                        cleanupFailures.Add(exception);
                    }
                }
                try {
                    await context.Database.CloseConnectionAsync();
                } catch (Exception exception) {
                    cleanupFailures.Add(exception);
                    try {
                        NpgsqlConnection.ClearPool(connection);
                    } catch (Exception poolException) {
                        cleanupFailures.Add(poolException);
                    }
                }
                if (cleanupFailures.Count > 0) {
                    try {
                        await connection.CloseAsync();
                    } catch (Exception exception) {
                        cleanupFailures.Add(exception);
                    }
                }
            }
            if (cleanupFailures.Count > 0) {
                if (failure is not null) {
                    cleanupFailures.Insert(0, failure);
                }
                throw new AggregateException("Project import cleanup could not be confirmed; the original failure is retained first when present.", cleanupFailures);
            }
        }
    }

    private static async Task ReleaseSessionKeysAsync(DbContext context, List<string> pendingKeys, bool requireHeld) {
        while (pendingKeys.Count > 0) {
            var key = pendingKeys[^1];
            var released = await context.Database.SqlQuery<bool>(
                $"SELECT pg_advisory_unlock(hashtextextended({key}, 0)) AS \"Value\"").SingleAsync(CancellationToken.None);
            if (requireHeld && !released) {
                throw new InvalidOperationException("The original project import advisory gate was not held by its connection.");
            }
            pendingKeys.RemoveAt(pendingKeys.Count - 1);
        }
    }
}
