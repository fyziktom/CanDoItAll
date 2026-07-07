using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace CanDoItAll.Tests.Support;

public sealed class CanDoItAllTestEnvironment : IAsyncDisposable
{
    private readonly HashSet<string> _profileKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<PostgresTestDatabaseLease> _postgresDatabases = [];

    private CanDoItAllTestEnvironment(string rootPath)
    {
        RootPath = rootPath;
        ControlPlaneRootPath = Path.Combine(rootPath, "control-plane");
        ProfilesRootPath = Path.Combine(rootPath, "profiles");

        Directory.CreateDirectory(ControlPlaneRootPath);
        Directory.CreateDirectory(ProfilesRootPath);
    }

    public string RootPath { get; }

    public string ControlPlaneRootPath { get; }

    public string ProfilesRootPath { get; }

    public static CanDoItAllTestEnvironment Create(string prefix)
    {
        var rootPath = TestFileSystem.CreateTemporaryRoot(prefix);
        return new CanDoItAllTestEnvironment(rootPath);
    }

    public IHostEnvironment CreateHostEnvironment(string applicationName) => new TestHostEnvironment(RootPath, applicationName);

    public TestDatabaseProfile CreateInMemoryProfile(string profileKey, string? databaseName = null)
    {
        var profileRootPath = GetOrCreateProfileRoot(profileKey);
        var workspaceRootPath = Path.Combine(profileRootPath, "workspace");
        var managerArtifactsRootPath = Path.Combine(profileRootPath, "manager-artifacts");

        Directory.CreateDirectory(workspaceRootPath);
        Directory.CreateDirectory(managerArtifactsRootPath);

        return new TestDatabaseProfile(
            profileKey,
            RootPath,
            profileRootPath,
            TestDatabaseProviderKind.InMemory,
            string.IsNullOrWhiteSpace(databaseName) ? $"{SanitizeSegment(profileKey)}-inmemory" : databaseName,
            workspaceRootPath,
            managerArtifactsRootPath);
    }

    public TestDatabaseProfile CreatePostgreSqlProfile(string profileKey)
    {
        var lease = PostgresTestDatabaseLease.Create(profileKey);
        _postgresDatabases.Add(lease);
        return CreatePostgreSqlProfile(profileKey, lease.ConnectionString);
    }

    public TestDatabaseProfile CreatePostgreSqlProfile(string profileKey, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var profileRootPath = GetOrCreateProfileRoot(profileKey);
        var workspaceRootPath = Path.Combine(profileRootPath, "workspace");
        var managerArtifactsRootPath = Path.Combine(profileRootPath, "manager-artifacts");

        Directory.CreateDirectory(workspaceRootPath);
        Directory.CreateDirectory(managerArtifactsRootPath);

        return new TestDatabaseProfile(
            profileKey,
            RootPath,
            profileRootPath,
            TestDatabaseProviderKind.PostgreSql,
            connectionString,
            workspaceRootPath,
            managerArtifactsRootPath);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var database in _postgresDatabases.AsEnumerable().Reverse())
        {
            await database.DisposeAsync();
        }

        TestFileSystem.DeleteDirectoryWithRetry(RootPath);
    }

    private string GetOrCreateProfileRoot(string profileKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileKey);

        if (!_profileKeys.Add(profileKey))
        {
            throw new InvalidOperationException($"Profile '{profileKey}' was already created for this test environment.");
        }

        var profileRootPath = Path.Combine(ProfilesRootPath, SanitizeSegment(profileKey));
        Directory.CreateDirectory(profileRootPath);
        return profileRootPath;
    }

    private static string SanitizeSegment(string value)
    {
        var sanitized = string.Concat(value.Trim().Select(character =>
            Path.GetInvalidFileNameChars().Contains(character) ? '-' : character));

        if (string.IsNullOrWhiteSpace(sanitized))
        {
            throw new ArgumentException("Profile key must contain at least one valid file-name character.", nameof(value));
        }

        return sanitized;
    }
}

public sealed class PostgresTestDatabaseLease : IAsyncDisposable
{
    private PostgresTestDatabaseLease(string databaseName, string connectionString, string adminConnectionString)
    {
        DatabaseName = databaseName;
        ConnectionString = connectionString;
        AdminConnectionString = adminConnectionString;
    }

    public string DatabaseName { get; }

    public string ConnectionString { get; }

    public DbContextOptions<AppDbContext> CreateAppDbContextOptions()
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        AppDbContextOptionsConfigurator.ConfigureModelCacheKey(optionsBuilder);
        optionsBuilder.UseNpgsql(
            ConnectionString,
            builder => builder.MigrationsAssembly(AppDbContextMigrationsAssemblyNames.PostgreSql));
        return optionsBuilder.Options;
    }

    public static PostgresTestDatabaseLease Create(string profileKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileKey);

        var availability = PostgresTestAvailability.EnsureAvailableAsync(FindRepositoryRoot())
            .GetAwaiter()
            .GetResult();
        if (!availability.IsAvailable || string.IsNullOrWhiteSpace(availability.ConnectionString))
        {
            throw new InvalidOperationException(availability.Message);
        }

        var databaseName = CreateDatabaseName(profileKey);
        var connectionString = BuildDatabaseConnectionString(availability.ConnectionString, databaseName);
        var adminConnectionString = BuildAdminConnectionString(availability.ConnectionString);
        CreateDatabase(adminConnectionString, databaseName);
        return new PostgresTestDatabaseLease(databaseName, connectionString, adminConnectionString);
    }

    public async ValueTask DisposeAsync()
    {
        await using var connection = new NpgsqlConnection(AdminConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"""drop database if exists "{EscapeIdentifier(DatabaseName)}" with (force);""";
        await command.ExecuteNonQueryAsync();
    }

    private string AdminConnectionString { get; }

    private static void CreateDatabase(string adminConnectionString, string databaseName)
    {
        using var connection = new NpgsqlConnection(adminConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"""create database "{EscapeIdentifier(databaseName)}";""";
        command.ExecuteNonQuery();
    }

    private static string BuildDatabaseConnectionString(string connectionString, string databaseName)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Database = databaseName,
            IncludeErrorDetail = true,
            Timeout = 5,
            CommandTimeout = 15
        };

        return builder.ConnectionString;
    }

    private static string BuildAdminConnectionString(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.Database))
        {
            builder.Database = "postgres";
        }

        builder.IncludeErrorDetail = true;
        builder.Timeout = 5;
        builder.CommandTimeout = 15;
        return builder.ConnectionString;
    }

    private static string CreateDatabaseName(string profileKey)
    {
        var sanitized = new string(profileKey
            .Trim()
            .ToLowerInvariant()
            .Select(character => char.IsAsciiLetterOrDigit(character) ? character : '_')
            .ToArray());
        sanitized = string.IsNullOrWhiteSpace(sanitized)
            ? "test"
            : sanitized.Trim('_');
        var prefix = $"cditall_{sanitized}";
        if (prefix.Length > 28)
        {
            prefix = prefix[..28].TrimEnd('_');
        }

        var databaseName = $"{prefix}_{Guid.NewGuid():N}";
        return databaseName.Length > 63
            ? databaseName[..63]
            : databaseName;
    }

    private static string EscapeIdentifier(string value)
        => value.Replace("\"", "\"\"", StringComparison.Ordinal);

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the CanDoItAll repository root from the test output directory.");
    }
}
