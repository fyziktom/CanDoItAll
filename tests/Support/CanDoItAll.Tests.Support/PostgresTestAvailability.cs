using System.Net.Sockets;
using Npgsql;

namespace CanDoItAll.Tests.Support;

public sealed record PostgresAvailabilityResult(
    bool IsAvailable,
    bool ProvisionedByDocker,
    string? ConnectionString,
    string Message);

public static class PostgresTestAvailability {
    private const string ConnectionOverrideVariable = "CANDOITALL_TESTS_POSTGRES_CONNECTION";
    public const int RequiredMajorVersion = 18;

    public static async Task<PostgresAvailabilityResult> EnsureAvailableAsync(
        string repositoryRoot,
        CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);

        var overrideConnectionString = Environment.GetEnvironmentVariable(ConnectionOverrideVariable);
        if (!string.IsNullOrWhiteSpace(overrideConnectionString)) {
            return await TryConnectAsync(
                overrideConnectionString,
                provisionedByDocker: false,
                sourceDescription: $"environment variable {ConnectionOverrideVariable}",
                cancellationToken);
        }

        return new PostgresAvailabilityResult(
            false,
            false,
            null,
            $"Set {ConnectionOverrideVariable} to an isolated PostgreSQL {RequiredMajorVersion} test server. Development and installed databases are not test fixtures; see docs/testing.md.");
    }

    private static async Task<PostgresAvailabilityResult> TryConnectAsync(
        string connectionString,
        bool provisionedByDocker,
        string sourceDescription,
        CancellationToken cancellationToken) {
        Exception? lastError = null;
        var timeoutAt = DateTimeOffset.UtcNow.AddSeconds(30);

        while (DateTimeOffset.UtcNow < timeoutAt) {
            try {
                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync(cancellationToken);
                await using var command = connection.CreateCommand();
                command.CommandText = "select current_setting('server_version_num')::int;";
                var serverVersion = (int)(await command.ExecuteScalarAsync(cancellationToken))!;
                if (serverVersion / 10000 != RequiredMajorVersion) {
                    return new PostgresAvailabilityResult(
                        false,
                        provisionedByDocker,
                        null,
                        $"PostgreSQL server_version_num={serverVersion} does not satisfy the required major {RequiredMajorVersion} test gate via {sourceDescription}.");
                }

                return new PostgresAvailabilityResult(
                    true,
                    provisionedByDocker,
                    connectionString,
                    $"PostgreSQL server_version_num={serverVersion} is available via {sourceDescription}.");
            } catch (Exception exception) when (exception is NpgsqlException or TimeoutException or IOException or SocketException) {
                lastError = exception;
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }

        return new PostgresAvailabilityResult(
            false,
            provisionedByDocker,
            null,
            $"PostgreSQL was not reachable via {sourceDescription}. Failure type: {lastError?.GetType().Name}.");
    }

}
