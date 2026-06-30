using System.Diagnostics;
using System.Net.Sockets;
using Npgsql;

namespace CanDoItAll.Tests.Support;

public sealed record PostgresAvailabilityResult(
    bool IsAvailable,
    bool ProvisionedByDocker,
    string? ConnectionString,
    string Message);

public static class PostgresTestAvailability
{
    private const string ConnectionOverrideVariable = "CANDOITALL_TESTS_POSTGRES_CONNECTION";
    private const string LocalDefaultConnectionString = "Host=127.0.0.1;Port=5432;Database=candoitall_development;Username=candoitall;Password=candoitall;Include Error Detail=true;Timeout=3;Command Timeout=5";
    private const string DockerComposeConnectionString = "Host=127.0.0.1;Port=5432;Database=candoitall_development;Username=candoitall;Password=candoitall;Include Error Detail=true;Timeout=3;Command Timeout=5";

    public static async Task<PostgresAvailabilityResult> EnsureAvailableAsync(
        string repositoryRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);

        var overrideConnectionString = Environment.GetEnvironmentVariable(ConnectionOverrideVariable);
        if (!string.IsNullOrWhiteSpace(overrideConnectionString))
        {
            return await TryConnectAsync(
                overrideConnectionString,
                provisionedByDocker: false,
                sourceDescription: $"environment variable {ConnectionOverrideVariable}",
                cancellationToken);
        }

        var localDefaultResult = await TryConnectAsync(
            LocalDefaultConnectionString,
            provisionedByDocker: false,
            sourceDescription: "local PostgreSQL service with project default credentials",
            cancellationToken,
            timeout: TimeSpan.FromSeconds(3));
        if (localDefaultResult.IsAvailable)
        {
            return localDefaultResult;
        }

        var composeFilePath = Path.Combine(repositoryRoot, "docker-compose.yml");
        if (!File.Exists(composeFilePath))
        {
            return new PostgresAvailabilityResult(false, false, null, $"Missing docker compose file at '{composeFilePath}'.");
        }

        var composeVersionResult = await RunProcessAsync("docker", "compose version", repositoryRoot, cancellationToken);
        if (composeVersionResult.ExitCode != 0)
        {
            return new PostgresAvailabilityResult(
                false,
                false,
                null,
                $"Docker Compose is unavailable. {composeVersionResult.DescribeFailure()}");
        }

        var composeUpResult = await RunProcessAsync("docker", "compose up -d postgres", repositoryRoot, cancellationToken);
        if (composeUpResult.ExitCode != 0)
        {
            return new PostgresAvailabilityResult(
                false,
                false,
                null,
                $"Failed to provision the postgres compose service. {composeUpResult.DescribeFailure()}");
        }

        return await TryConnectAsync(
            DockerComposeConnectionString,
            provisionedByDocker: true,
            sourceDescription: "docker compose postgres service",
            cancellationToken);
    }

    private static async Task<PostgresAvailabilityResult> TryConnectAsync(
        string connectionString,
        bool provisionedByDocker,
        string sourceDescription,
        CancellationToken cancellationToken,
        TimeSpan? timeout = null)
    {
        Exception? lastError = null;
        var timeoutAt = DateTimeOffset.UtcNow.Add(timeout ?? TimeSpan.FromSeconds(30));

        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            try
            {
                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync(cancellationToken);
                await using var command = connection.CreateCommand();
                command.CommandText = "select 1;";
                await command.ExecuteScalarAsync(cancellationToken);

                return new PostgresAvailabilityResult(
                    true,
                    provisionedByDocker,
                    connectionString,
                    $"PostgreSQL is available via {sourceDescription}.");
            }
            catch (Exception exception) when (exception is NpgsqlException or TimeoutException or IOException or SocketException)
            {
                lastError = exception;
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }

        return new PostgresAvailabilityResult(
            false,
            provisionedByDocker,
            null,
            $"PostgreSQL was not reachable via {sourceDescription}. {lastError?.Message}");
    }

    private static async Task<ProcessExecutionResult> RunProcessAsync(
        string fileName,
        string arguments,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(fileName, arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start process '{fileName} {arguments}'.");

        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync(cancellationToken);

        return new ProcessExecutionResult(
            process.ExitCode,
            (await standardOutputTask).Trim(),
            (await standardErrorTask).Trim());
    }

    private sealed record ProcessExecutionResult(int ExitCode, string StandardOutput, string StandardError)
    {
        public string DescribeFailure()
        {
            var output = string.Join(
                " ",
                new[] { StandardOutput, StandardError }
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value.Trim()));

            return string.IsNullOrWhiteSpace(output)
                ? $"Exit code {ExitCode}."
                : $"Exit code {ExitCode}. {output}";
        }
    }
}
