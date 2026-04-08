using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;

namespace CanDoItAll.Tests.Support;

public sealed class CanDoItAllTestEnvironment : IAsyncDisposable
{
    private readonly HashSet<string> _profileKeys = new(StringComparer.OrdinalIgnoreCase);

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

    public TestDatabaseProfile CreateManagedSqliteProfile(string profileKey)
    {
        var profileRootPath = GetOrCreateProfileRoot(profileKey);
        var databasePath = Path.Combine(profileRootPath, "database", $"{SanitizeSegment(profileKey)}.db");
        var workspaceRootPath = Path.Combine(profileRootPath, "workspace");
        var managerArtifactsRootPath = Path.Combine(profileRootPath, "manager-artifacts");

        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        Directory.CreateDirectory(workspaceRootPath);
        Directory.CreateDirectory(managerArtifactsRootPath);

        return new TestDatabaseProfile(
            profileKey,
            RootPath,
            profileRootPath,
            TestDatabaseProviderKind.Sqlite,
            $"Data Source={databasePath}",
            workspaceRootPath,
            managerArtifactsRootPath,
            databasePath);
    }

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

    public ValueTask DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        TestFileSystem.DeleteDirectoryWithRetry(RootPath);
        return ValueTask.CompletedTask;
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
