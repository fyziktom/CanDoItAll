using System.Text.Json;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.ControlPlane;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit.Storage;

public sealed class FileApplicationPreferenceServiceTests : IDisposable
{
    private readonly string rootPath = Path.Combine(
        Path.GetTempPath(),
        nameof(FileApplicationPreferenceServiceTests),
        Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("xlsx", ".xlsx")]
    [InlineData(" .DOCX ", ".docx")]
    public void File_application_extension_normalizes_user_input(string input, string expected)
    {
        var extension = new FileApplicationExtension(input);

        Assert.Equal(expected, extension.Value);
    }

    [Fact]
    public void File_application_extension_rejects_a_multi_dot_value()
    {
        Assert.Throws<ArgumentException>(() => new FileApplicationExtension(".tar.gz"));
    }

    [Fact]
    public void File_application_preference_preserves_significant_path_whitespace()
    {
        string executablePath = Path.Combine(rootPath, " viewer ");

        var preference = new FileApplicationPreference(
            new FileApplicationExtension(".xlsx"),
            executablePath);

        Assert.Equal(executablePath, preference.ExecutablePath);
    }

    [Theory]
    [InlineData("forecast.xlsx", true)]
    [InlineData("proposal.docx", true)]
    [InlineData("slides.pptx", true)]
    [InlineData("run.ps1", false)]
    [InlineData("installer.exe", false)]
    public void System_association_policy_only_allows_data_file_types(string fileName, bool expected)
    {
        Assert.Equal(expected, FileToolsExternalOpenPolicy.IsAllowedSystemAssociatedFile(fileName));
    }

    [Fact]
    public async Task SaveAsync_persists_a_normalized_preference_for_a_new_service_instance()
    {
        string executablePath = CreateExecutable("office-viewer.exe");
        var sut = CreateService();

        await sut.SaveAsync(new FileApplicationPreference(
            new FileApplicationExtension(" XLSX "),
            executablePath));

        FileApplicationPreference? resolved = CreateService().ResolveForFile("quarterly-report.XLSX");

        Assert.NotNull(resolved);
        Assert.Equal(".xlsx", resolved.Extension.Value);
        Assert.Equal(Path.GetFullPath(executablePath), resolved.ExecutablePath);
        string json = await File.ReadAllTextAsync(SettingsPath);
        Assert.Contains("\"schemaVersion\": 2", json, StringComparison.Ordinal);
        Assert.Contains("\"executable\"", json, StringComparison.Ordinal);
        Assert.Contains("\"hostBindingId\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"executablePath\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResolveForFile_returns_a_configured_preference_when_its_executable_becomes_stale()
    {
        string executablePath = CreateExecutable("spreadsheet-viewer.exe");
        var sut = CreateService();
        await sut.SaveAsync(new FileApplicationPreference(
            new FileApplicationExtension(".xlsx"),
            executablePath));
        File.Delete(executablePath);

        FileApplicationPreference? resolved = CreateService().ResolveForFile("forecast.xlsx");

        Assert.NotNull(resolved);
        Assert.Equal(Path.GetFullPath(executablePath), resolved.ExecutablePath);
    }

    [Fact]
    public async Task ListAsync_throws_an_explicit_error_for_invalid_json()
    {
        Directory.CreateDirectory(rootPath);
        await File.WriteAllTextAsync(SettingsPath, "{ invalid-json");
        var sut = CreateService();

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ListAsync());

        Assert.Equal("The preferred file application document is invalid.", exception.Message);
        Assert.IsType<JsonException>(exception.InnerException);
    }

    [Fact]
    public async Task SaveAsync_fails_for_a_missing_executable_without_replacing_the_existing_preference()
    {
        string existingExecutablePath = CreateExecutable("existing-viewer.exe");
        var sut = CreateService();
        await sut.SaveAsync(new FileApplicationPreference(
            new FileApplicationExtension(".pptx"),
            existingExecutablePath));
        string missingExecutablePath = Path.Combine(rootPath, "missing-viewer.exe");

        await Assert.ThrowsAsync<FileNotFoundException>(() => sut.SaveAsync(
            new FileApplicationPreference(
                new FileApplicationExtension("PPTX"),
                missingExecutablePath)));

        FileApplicationPreference? resolved = CreateService().ResolveForFile("presentation.pptx");
        Assert.NotNull(resolved);
        Assert.Equal(Path.GetFullPath(existingExecutablePath), resolved.ExecutablePath);
    }

    [Fact]
    public async Task SaveAsync_rejects_foreign_executable_syntax_before_filesystem_lookup()
    {
        string foreignExecutable = OperatingSystem.IsWindows()
            ? "/Applications/ForeignViewer.app/Contents/MacOS/ForeignViewer"
            : @"C:\Program Files\ForeignViewer\viewer.exe";
        var sut = CreateService();

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.SaveAsync(new FileApplicationPreference(
                new FileApplicationExtension(".xlsx"),
                foreignExecutable)));

        Assert.Contains("syntax", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Foreign_legacy_preference_requires_explicit_rebind_and_is_not_resolved_for_launch()
    {
        Directory.CreateDirectory(rootPath);
        string foreignExecutable = OperatingSystem.IsWindows()
            ? "/Applications/ForeignViewer.app/Contents/MacOS/ForeignViewer"
            : @"C:\Program Files\ForeignViewer\viewer.exe";
        await File.WriteAllTextAsync(
            SettingsPath,
            $$"""
            {
              "schemaVersion": 1,
              "applications": [
                {
                  "extension": ".xlsx",
                  "executablePath": {{JsonSerializer.Serialize(foreignExecutable)}}
                }
              ]
            }
            """);
        FileApplicationPreferenceService sut = CreateService();

        FileApplicationPreference unresolved = Assert.Single(await sut.ListAsync());

        Assert.True(unresolved.RequiresRebind);
        Assert.Null(sut.ResolveForFile("forecast.xlsx"));
        string migratedJson = await File.ReadAllTextAsync(SettingsPath);
        Assert.Contains("needsRebind", migratedJson, StringComparison.OrdinalIgnoreCase);
        using (JsonDocument migratedDocument = JsonDocument.Parse(migratedJson))
        {
            string? persistedExecutable = migratedDocument.RootElement
                .GetProperty("applications")[0]
                .GetProperty("executable")
                .GetProperty("path")
                .GetString();
            Assert.Equal(foreignExecutable, persistedExecutable);
        }

        string reboundExecutable = CreateExecutable("rebound-viewer.exe");
        await sut.SaveAsync(new FileApplicationPreference(
            new FileApplicationExtension(".xlsx"),
            reboundExecutable));

        FileApplicationPreference? rebound = sut.ResolveForFile("forecast.xlsx");
        Assert.NotNull(rebound);
        Assert.False(rebound.RequiresRebind);
        Assert.Equal(Path.GetFullPath(reboundExecutable), rebound.ExecutablePath);
    }

    [Fact]
    public async Task Legacy_preference_migration_creates_backup_commit_and_rollback_records()
    {
        string executablePath = CreateExecutable("legacy-viewer.exe");
        await File.WriteAllTextAsync(
            SettingsPath,
            $$"""
            {
              "schemaVersion": 1,
              "applications": [
                {
                  "extension": ".docx",
                  "executablePath": {{JsonSerializer.Serialize(executablePath)}}
                }
              ]
            }
            """);
        FileApplicationPreferenceService sut = CreateService();

        FileApplicationPreference migrated = Assert.Single(await sut.ListAsync());
        string migrationRoot = Path.Combine(rootPath, "migrations", "file-applications-v2");

        Assert.True(migrated.RequiresRebind);
        Assert.Null(sut.ResolveForFile("document.docx"));
        Assert.True(File.Exists(Path.Combine(migrationRoot, "preferences.v1.backup.json")));
        Assert.True(File.Exists(Path.Combine(migrationRoot, "preferences.v1.backup.json.integrity.json")));
        Assert.True(File.Exists(Path.Combine(migrationRoot, "preferences.v2.staged.json")));
        Assert.True(File.Exists(Path.Combine(migrationRoot, "commit.json")));

        File.Delete(Path.Combine(migrationRoot, "commit.json"));
        FileApplicationPreference repaired = Assert.Single(await CreateService().ListAsync());
        Assert.True(repaired.RequiresRebind);
        Assert.True(File.Exists(Path.Combine(migrationRoot, "commit.json")));

        File.Delete(Path.Combine(migrationRoot, "commit.json"));
        Assert.True(await sut.RollbackPathMigrationAsync());
        using JsonDocument rolledBack = JsonDocument.Parse(await File.ReadAllTextAsync(SettingsPath));
        Assert.Equal(1, rolledBack.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.True(File.Exists(Path.Combine(migrationRoot, "preferences.v2.pre-rollback.json")));
        Assert.True(File.Exists(Path.Combine(migrationRoot, "rollback.commit.json")));
    }

    [Fact]
    public async Task Legacy_preference_rollback_rejects_a_modified_backup()
    {
        string executablePath = CreateExecutable("checksum-viewer.exe");
        await File.WriteAllTextAsync(
            SettingsPath,
            $$"""
            {
              "schemaVersion": 1,
              "applications": [
                {
                  "extension": ".pptx",
                  "executablePath": {{JsonSerializer.Serialize(executablePath)}}
                }
              ]
            }
            """);
        FileApplicationPreferenceService sut = CreateService();
        Assert.Single(await sut.ListAsync());
        string backupPath = Path.Combine(
            rootPath,
            "migrations",
            "file-applications-v2",
            "preferences.v1.backup.json");
        File.Delete(Path.Combine(Path.GetDirectoryName(backupPath)!, "commit.json"));
        await File.AppendAllTextAsync(backupPath, Environment.NewLine);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.RollbackPathMigrationAsync());

        Assert.Contains("checksum", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(executablePath, exception.Message, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(rootPath))
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    private string SettingsPath => Path.Combine(rootPath, "file-application-preferences.json");

    private FileApplicationPreferenceService CreateService()
        => new(
            new StaticControlPlanePathResolver(rootPath),
            new DurableFileWriter(TestWorkspaceServices.PhysicalPathPolicyFactory),
            NullLogger<FileApplicationPreferenceService>.Instance);

    private string CreateExecutable(string fileName)
    {
        Directory.CreateDirectory(rootPath);
        string path = Path.Combine(rootPath, fileName);
        File.WriteAllText(path, string.Empty);
        return path;
    }

    private sealed class StaticControlPlanePathResolver(string rootPath) : IControlPlanePathResolver
    {
        public string ResolveRootPath() => rootPath;

        public string ResolveDatabaseProfilesRootPath() => Path.Combine(rootPath, "database-profiles");

        public string ResolveCatalogFilePath() => Path.Combine(ResolveDatabaseProfilesRootPath(), "catalog.json");

        public string ResolveActiveProfileStateFilePath()
            => Path.Combine(ResolveDatabaseProfilesRootPath(), "active-profile.json");

        public string ResolveFileApplicationPreferencesFilePath()
            => Path.Combine(rootPath, "file-application-preferences.json");

        public string ResolveDataProtectionKeysPath() => Path.Combine(rootPath, "dataprotection-keys");

        public string ResolveStateRootPath() => Path.Combine(rootPath, "state");

        public string ResolveLogsRootPath() => Path.Combine(rootPath, "logs");

        public string ResolveRuntimeTemporaryRootPath() => Path.Combine(rootPath, "runtime");
    }
}
