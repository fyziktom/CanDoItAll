using Npgsql;

namespace CanDoItAll.Infrastructure.ControlPlane;

public interface IDatabaseDriver
{
    DatabaseProviderKind ProviderKind { get; }

    Task EnsureDatabaseAsync(ResolvedDatabaseProfile profile, CancellationToken cancellationToken = default);

    Task CreateEmptyAsync(ResolvedDatabaseProfile profile, CancellationToken cancellationToken = default);
}

public interface IDatabaseDriverRegistry
{
    IDatabaseDriver Resolve(DatabaseProviderKind providerKind);
}

public sealed class DatabaseDriverRegistry(IEnumerable<IDatabaseDriver> drivers) : IDatabaseDriverRegistry
{
    private readonly IReadOnlyDictionary<DatabaseProviderKind, IDatabaseDriver> _drivers = drivers.ToDictionary(
        driver => driver.ProviderKind,
        driver => driver);

    public IDatabaseDriver Resolve(DatabaseProviderKind providerKind)
    {
        if (_drivers.TryGetValue(providerKind, out var driver))
        {
            return driver;
        }

        throw new InvalidOperationException($"No database driver is registered for provider '{providerKind}'.");
    }
}

public sealed class InMemoryDatabaseDriver : IDatabaseDriver
{
    public DatabaseProviderKind ProviderKind => DatabaseProviderKind.InMemory;

    public Task EnsureDatabaseAsync(ResolvedDatabaseProfile profile, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task CreateEmptyAsync(ResolvedDatabaseProfile profile, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

public sealed class PostgreSqlDatabaseDriver : IDatabaseDriver
{
    public DatabaseProviderKind ProviderKind => DatabaseProviderKind.PostgreSql;

    public async Task EnsureDatabaseAsync(ResolvedDatabaseProfile profile, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(profile.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await connection.CloseAsync();
    }

    public async Task CreateEmptyAsync(ResolvedDatabaseProfile profile, CancellationToken cancellationToken = default)
    {
        var descriptor = profile.Profile.PostgreSql
            ?? throw new InvalidOperationException("PostgreSQL profile is missing connection metadata.");
        var adminDatabase = string.IsNullOrWhiteSpace(descriptor.AdminDatabaseName)
            ? "postgres"
            : descriptor.AdminDatabaseName;

        var adminBuilder = new NpgsqlConnectionStringBuilder(profile.ConnectionString)
        {
            Database = adminDatabase
        };

        await using var connection = new NpgsqlConnection(adminBuilder.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var existsCommand = connection.CreateCommand();
        existsCommand.CommandText = "select 1 from pg_database where datname = @databaseName;";
        existsCommand.Parameters.AddWithValue("databaseName", descriptor.DatabaseName);
        var exists = await existsCommand.ExecuteScalarAsync(cancellationToken) is not null;
        if (exists)
        {
            return;
        }

        await using var createCommand = connection.CreateCommand();
        createCommand.CommandText = $"create database {QuoteIdentifier(descriptor.DatabaseName)};";
        await createCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string QuoteIdentifier(string value)
    {
        return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
