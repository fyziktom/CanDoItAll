using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CanDoItAll.Infrastructure.ControlPlane;

internal static class DatabaseTransferSessionGate {
    internal static async Task<TResult> RunAsync<TResult>(DbContext context, IReadOnlyList<long> keys,
        Func<Func<CancellationToken, Task>, Task<TResult>> operation, CancellationToken cancellationToken) {
        if (keys.Count == 0) {
            return await operation(static _ => Task.CompletedTask);
        }
        if (context.Database.CurrentTransaction is not null) {
            throw new InvalidOperationException("Transfer entry gates must precede the target transaction.");
        }
        var connection = (NpgsqlConnection)context.Database.GetDbConnection();
        var pending = new List<long>();
        var acquired = new HashSet<long>();
        var timeout = context.Database.GetCommandTimeout();
        Exception? failure = null;
        await context.Database.OpenConnectionAsync(cancellationToken);
        try {
            context.Database.SetCommandTimeout(TimeSpan.FromSeconds(5));
            foreach (var key in keys) {
                pending.Add(key);
                await context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_lock({key})", cancellationToken);
                acquired.Add(key);
            }
            context.Database.SetCommandTimeout(timeout);
            return await operation(TransferToTransactionAsync);
        } catch (Exception exception) {
            failure = exception;
            throw;
        } finally {
            var cleanupFailures = new List<Exception>();
            context.Database.SetCommandTimeout(TimeSpan.FromSeconds(5));
            foreach (var key in pending.AsEnumerable().Reverse()) {
                try {
                    var released = await context.Database.SqlQuery<bool>($"SELECT pg_advisory_unlock({key}) AS \"Value\"")
                        .SingleAsync(CancellationToken.None);
                    if (!released && acquired.Contains(key)) {
                        throw new InvalidOperationException("The transfer entry gate was no longer held by its original connection.");
                    }
                } catch (Exception exception) {
                    cleanupFailures.Add(exception);
                }
            }
            context.Database.SetCommandTimeout(timeout);
            if (cleanupFailures.Count > 0) {
                ClearPool();
            }
            try {
                await context.Database.CloseConnectionAsync();
            } catch (Exception exception) {
                cleanupFailures.Add(exception);
                ClearPool();
                try {
                    await connection.CloseAsync();
                } catch (Exception closeException) {
                    cleanupFailures.Add(closeException);
                }
            }
            if (cleanupFailures.Count > 0) {
                if (failure is not null) {
                    cleanupFailures.Insert(0, failure);
                }
                throw new AggregateException("Transfer entry-gate cleanup could not be confirmed; the original failure is retained first when present.", cleanupFailures);
            }

            void ClearPool() {
                try {
                    NpgsqlConnection.ClearPool(connection);
                } catch (Exception exception) {
                    cleanupFailures.Add(exception);
                }
            }
        }

        async Task TransferToTransactionAsync(CancellationToken token) {
            if (context.Database.CurrentTransaction is null) {
                throw new InvalidOperationException("Transfer entry gates require their target transaction before handoff.");
            }
            foreach (var key in keys) {
                await context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})", token);
            }
            while (pending.Count > 0) {
                var key = pending[^1];
                var released = await context.Database.SqlQuery<bool>($"SELECT pg_advisory_unlock({key}) AS \"Value\"")
                    .SingleAsync(CancellationToken.None);
                if (!released) {
                    throw new InvalidOperationException("The transfer entry gate was not held during transaction handoff.");
                }
                pending.RemoveAt(pending.Count - 1);
            }
        }
    }
}
