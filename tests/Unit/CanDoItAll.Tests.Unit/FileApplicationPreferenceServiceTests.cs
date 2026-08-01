using System.Text.Json;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.Infrastructure.ControlPlane;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit;

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
    }
}
