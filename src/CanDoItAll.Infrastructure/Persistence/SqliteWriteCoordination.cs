using System.Collections.Concurrent;
using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CanDoItAll.Infrastructure.Persistence;

public static class SqliteWriteCoordination
{
    private static readonly TimeSpan[] BusyRetryDelays =
    [
        TimeSpan.FromMilliseconds(50),
        TimeSpan.FromMilliseconds(100),
        TimeSpan.FromMilliseconds(200),
        TimeSpan.FromMilliseconds(400),
        TimeSpan.FromMilliseconds(800)
    ];

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> WriteGates = new(StringComparer.OrdinalIgnoreCase);
    private static readonly DbConnectionInterceptor PragmasInterceptor = new SqlitePragmasConnectionInterceptor();

    internal const int BusyTimeoutMilliseconds = 5000;

    public static DbConnectionInterceptor ConnectionInterceptor => PragmasInterceptor;

    public static int RetryAttemptCount => BusyRetryDelays.Length;

    public static string NormalizeConnectionString(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var builder = new SqliteConnectionStringBuilder(connectionString);
        var minimumTimeoutSeconds = Math.Max(1, BusyTimeoutMilliseconds / 1000);
        if (builder.DefaultTimeout < minimumTimeoutSeconds)
        {
            builder.DefaultTimeout = minimumTimeoutSeconds;
        }

        return builder.ToString();
    }

    public static TimeSpan GetRetryDelay(int attempt)
    {
        if ((uint)attempt >= (uint)BusyRetryDelays.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(attempt));
        }

        return BusyRetryDelays[attempt];
    }

    public static bool IsBusy(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch
        {
            SqliteException sqliteException => sqliteException.SqliteErrorCode is 5 or 6,
            DbUpdateException dbUpdateException when dbUpdateException.InnerException is not null => IsBusy(dbUpdateException.InnerException),
            _ when exception.InnerException is not null => IsBusy(exception.InnerException),
            _ => false
        };
    }

    public static SemaphoreSlim GetWriteGate(string? connectionString)
    {
        var key = string.IsNullOrWhiteSpace(connectionString)
            ? "__sqlite-default__"
            : NormalizeConnectionString(connectionString);
        return WriteGates.GetOrAdd(key, static _ => new SemaphoreSlim(1, 1));
    }

    private sealed class SqlitePragmasConnectionInterceptor : DbConnectionInterceptor
    {
        public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
        {
            ApplyPragmas(connection);
            base.ConnectionOpened(connection, eventData);
        }

        public override async Task ConnectionOpenedAsync(
            DbConnection connection,
            ConnectionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            await ApplyPragmasAsync(connection, cancellationToken);
            await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
        }

        private static void ApplyPragmas(DbConnection connection)
        {
            if (connection is not SqliteConnection sqliteConnection)
            {
                return;
            }

            ExecutePragma(sqliteConnection, $"PRAGMA busy_timeout={BusyTimeoutMilliseconds};");
            TryEnableWalMode(sqliteConnection);
        }

        private static async Task ApplyPragmasAsync(DbConnection connection, CancellationToken cancellationToken)
        {
            if (connection is not SqliteConnection sqliteConnection)
            {
                return;
            }

            await ExecutePragmaAsync(sqliteConnection, $"PRAGMA busy_timeout={BusyTimeoutMilliseconds};", cancellationToken);
            await TryEnableWalModeAsync(sqliteConnection, cancellationToken);
        }

        private static void TryEnableWalMode(SqliteConnection connection)
        {
            try
            {
                ExecutePragma(connection, "PRAGMA journal_mode=WAL;");
            }
            catch (Exception ex) when (IsPragmaFailureSafeToIgnore(ex))
            {
            }
        }

        private static async Task TryEnableWalModeAsync(SqliteConnection connection, CancellationToken cancellationToken)
        {
            try
            {
                await ExecutePragmaAsync(connection, "PRAGMA journal_mode=WAL;", cancellationToken);
            }
            catch (Exception ex) when (IsPragmaFailureSafeToIgnore(ex))
            {
            }
        }

        private static bool IsPragmaFailureSafeToIgnore(Exception exception)
        {
            return exception is SqliteException { SqliteErrorCode: 8 } ||
                IsBusy(exception);
        }

        private static void ExecutePragma(SqliteConnection connection, string commandText)
        {
            using var command = connection.CreateCommand();
            command.CommandText = commandText;
            command.ExecuteNonQuery();
        }

        private static async Task ExecutePragmaAsync(
            SqliteConnection connection,
            string commandText,
            CancellationToken cancellationToken)
        {
            using var command = connection.CreateCommand();
            command.CommandText = commandText;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
