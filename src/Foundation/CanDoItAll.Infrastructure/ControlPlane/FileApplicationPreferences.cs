using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Infrastructure.ControlPlane;

public readonly record struct FileApplicationExtension
{
    public const int MaximumLength = 32;

    public FileApplicationExtension(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        string normalized = value.Trim().ToLowerInvariant();
        if (!normalized.StartsWith('.'))
        {
            normalized = $".{normalized}";
        }

        if (normalized.Length is < 2 or > MaximumLength ||
            normalized.Skip(1).Any(character =>
                !char.IsLetterOrDigit(character) && character is not '-' and not '_'))
        {
            throw new ArgumentException("The file extension is invalid.", nameof(value));
        }

        Value = normalized;
    }

    public string Value { get; }

    public override string ToString() => Value ?? string.Empty;
}

public sealed record FileApplicationPreference
{
    public FileApplicationPreference(
        FileApplicationExtension extension,
        string executablePath,
        HostBoundPathState state = HostBoundPathState.Active)
    {
        if (string.IsNullOrWhiteSpace(extension.Value))
        {
            throw new ArgumentException("A file extension is required.", nameof(extension));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        Extension = extension;
        ExecutablePath = executablePath;
        State = state;
    }

    public FileApplicationExtension Extension { get; }

    public string ExecutablePath { get; }

    public HostBoundPathState State { get; }

    public bool RequiresRebind => State != HostBoundPathState.Active;
}

public interface IFileApplicationPreferenceService
{
    Task<IReadOnlyList<FileApplicationPreference>> ListAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(FileApplicationPreference preference, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(FileApplicationExtension extension, CancellationToken cancellationToken = default);

    Task<bool> RollbackPathMigrationAsync(CancellationToken cancellationToken = default);

    FileApplicationPreference? ResolveForFile(string fileName);
}

public sealed class FileApplicationPreferenceService(
    IControlPlanePathResolver pathResolver,
    DurableFileWriter durableFileWriter,
    ILogger<FileApplicationPreferenceService> logger) : IFileApplicationPreferenceService
{
    private const int CurrentSchemaVersion = 2;
    private const string MigrationDirectoryName = "file-applications-v2";
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();
    private readonly object sync = new();

    public Task<IReadOnlyList<FileApplicationPreference>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (sync)
        {
            using IDisposable coordination = AcquireCoordination(cancellationToken);
            IReadOnlyList<FileApplicationPreference> preferences = ReadLocked()
                .OrderBy(preference => preference.Extension.Value, StringComparer.Ordinal)
                .ToArray();
            return Task.FromResult(preferences);
        }
    }

    public Task SaveAsync(
        FileApplicationPreference preference,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preference);
        cancellationToken.ThrowIfCancellationRequested();
        string executablePath = ValidateExecutablePath(preference.ExecutablePath, requireExistingFile: true);
        var normalized = new FileApplicationPreference(
            preference.Extension,
            executablePath,
            HostBoundPathState.Active);
        lock (sync)
        {
            using IDisposable coordination = AcquireCoordination(cancellationToken);
            List<FileApplicationPreference> preferences = ReadLocked();
            int existingIndex = preferences.FindIndex(item => item.Extension == normalized.Extension);
            if (existingIndex >= 0)
            {
                preferences[existingIndex] = normalized;
            }
            else
            {
                preferences.Add(normalized);
            }

            WriteLocked(preferences);
        }

        logger.LogInformation(
            "Preferred file application saved. Extension={Extension} ExecutableName={ExecutableName}.",
            normalized.Extension.Value,
            Path.GetFileName(normalized.ExecutablePath));
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(
        FileApplicationExtension extension,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        bool removed;
        lock (sync)
        {
            using IDisposable coordination = AcquireCoordination(cancellationToken);
            List<FileApplicationPreference> preferences = ReadLocked();
            removed = preferences.RemoveAll(item => item.Extension == extension) > 0;
            if (removed)
            {
                WriteLocked(preferences);
            }
        }

        if (removed)
        {
            logger.LogInformation(
                "Preferred file application removed. Extension={Extension}.",
                extension.Value);
        }

        return Task.FromResult(removed);
    }

    public Task<bool> RollbackPathMigrationAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (sync)
        {
            using IDisposable coordination = AcquireCoordination(cancellationToken);
            string migrationRoot = ResolveMigrationRoot();
            string backupPath = Path.Combine(migrationRoot, "preferences.v1.backup.json");
            if (!File.Exists(backupPath))
            {
                return Task.FromResult(false);
            }

            string backupJson = MigrationBackupIntegrity.ReadVerified(backupPath);
            FileApplicationPreferencesDocument backup = DeserializeDocument(backupJson);
            if (backup.SchemaVersion != 1)
            {
                throw new InvalidOperationException(
                    "The preferred file application migration backup has an unexpected schema version.");
            }

            string commitPath = Path.Combine(migrationRoot, "commit.json");
            if (File.Exists(commitPath))
            {
                FileApplicationMigrationManifest commit = DeserializeMigrationManifest(
                    File.ReadAllText(commitPath));
                if (!string.Equals(commit.SourceSha256, ComputeSha256(backupJson), StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "The preferred file application migration backup checksum is invalid.");
                }
            }

            string targetPath = pathResolver.ResolveFileApplicationPreferencesFilePath();
            string preRollbackPath = Path.Combine(migrationRoot, "preferences.v2.pre-rollback.json");
            if (!File.Exists(preRollbackPath) && File.Exists(targetPath))
            {
                durableFileWriter.WriteText(
                    pathResolver.ResolveRootPath(),
                    preRollbackPath,
                    File.ReadAllText(targetPath),
                    CreateNewPrivateWriteOptions());
            }

            durableFileWriter.WriteText(
                pathResolver.ResolveRootPath(),
                targetPath,
                backupJson,
                DurableFileWriteOptions.Private);
            durableFileWriter.WriteText(
                pathResolver.ResolveRootPath(),
                Path.Combine(migrationRoot, "rollback.commit.json"),
                JsonSerializer.Serialize(new
                {
                    formatVersion = 1,
                    state = "RolledBack",
                    rolledBackAtUtc = DateTimeOffset.UtcNow
                }, SerializerOptions),
                DurableFileWriteOptions.Private);
            return Task.FromResult(true);
        }
    }

    public FileApplicationPreference? ResolveForFile(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        string extensionValue = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(extensionValue))
        {
            return null;
        }

        var extension = new FileApplicationExtension(extensionValue);
        lock (sync)
        {
            using IDisposable coordination = AcquireCoordination();
            return ReadLocked().FirstOrDefault(preference =>
                preference.Extension == extension &&
                preference.State == HostBoundPathState.Active);
        }
    }

    private List<FileApplicationPreference> ReadLocked()
    {
        string path = pathResolver.ResolveFileApplicationPreferencesFilePath();
        if (!File.Exists(path))
        {
            return [];
        }

        string json = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            FileApplicationPreferencesDocument document = DeserializeDocument(json);
            if (document.SchemaVersion > CurrentSchemaVersion)
            {
                throw new InvalidOperationException(
                    $"Unsupported preferred file application schema version '{document.SchemaVersion}'.");
            }

            if (document.SchemaVersion < CurrentSchemaVersion)
            {
                document = MigrateDocumentLocked(path, json, document);
            }
            else
            {
                EnsureMigrationCommitMarkerLocked(json, document);
            }

            var seenExtensions = new HashSet<FileApplicationExtension>();
            List<FileApplicationPreferenceRecord> records = document.Applications
                ?? throw new InvalidOperationException("The preferred file application list is missing.");
            var preferences = new List<FileApplicationPreference>(records.Count);
            foreach (FileApplicationPreferenceRecord record in records)
            {
                var extension = new FileApplicationExtension(record.Extension);
                if (!seenExtensions.Add(extension))
                {
                    throw new InvalidOperationException(
                        $"Preferred file application extension '{extension.Value}' is duplicated.");
                }

                HostBoundPathRecord? executable = record.Executable;
                if (executable is null)
                {
                    throw new InvalidOperationException(
                        $"Preferred file application extension '{extension.Value}' is missing its host-bound executable.");
                }

                HostBoundPathState state = HostBoundPathPolicy.TryResolve(
                    executable,
                    HostPathContext.CaptureCurrent(),
                    out string resolvedPath,
                    out _)
                    ? HostBoundPathState.Active
                    : HostBoundPathState.NeedsRebind;
                string executablePath = state == HostBoundPathState.Active
                    ? ValidateExecutablePath(resolvedPath, requireExistingFile: false)
                    : executable.Path;
                preferences.Add(new FileApplicationPreference(extension, executablePath, state));
            }

            return preferences;
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                "The preferred file application document is invalid.",
                exception);
        }
    }

    private void WriteLocked(IReadOnlyList<FileApplicationPreference> preferences)
    {
        string path = pathResolver.ResolveFileApplicationPreferencesFilePath();
        var document = new FileApplicationPreferencesDocument
        {
            Applications = preferences
                .OrderBy(preference => preference.Extension.Value, StringComparer.Ordinal)
                .Select(preference => new FileApplicationPreferenceRecord
                {
                    Extension = preference.Extension.Value,
                    Executable = preference.State == HostBoundPathState.Active
                        ? HostBoundPathPolicy.BindCurrent(
                            preference.ExecutablePath,
                            DateTimeOffset.UtcNow)
                        : HostBoundPathPolicy.ImportLegacy(
                            preference.ExecutablePath,
                            HostPathContext.CaptureCurrent())
                })
                .ToList()
        };
        durableFileWriter.WriteText(
            pathResolver.ResolveRootPath(),
            path,
            JsonSerializer.Serialize(document, SerializerOptions),
            DurableFileWriteOptions.Private);
    }

    private FileApplicationPreferencesDocument MigrateDocumentLocked(
        string path,
        string sourceJson,
        FileApplicationPreferencesDocument document)
    {
        string migrationRoot = ResolveMigrationRoot();
        durableFileWriter.EnsureDirectory(
            pathResolver.ResolveRootPath(),
            migrationRoot,
            requirePrivateUnixMode: true);
        string backupPath = Path.Combine(migrationRoot, "preferences.v1.backup.json");
        string backupJson = MigrationBackupIntegrity.CreateOrVerify(
            durableFileWriter,
            pathResolver.ResolveRootPath(),
            backupPath,
            sourceJson);

        HostPathContext currentHost = HostPathContext.CaptureCurrent();
        foreach (FileApplicationPreferenceRecord record in document.Applications ?? [])
        {
            if (record.Executable is not null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(record.LegacyExecutablePath))
            {
                throw new InvalidOperationException(
                    "A legacy preferred file application record is missing its executable path.");
            }

            record.Executable = HostBoundPathPolicy.ImportLegacy(
                record.LegacyExecutablePath,
                currentHost);
            record.LegacyExecutablePath = null;
        }

        document.SchemaVersion = CurrentSchemaVersion;
        string targetJson = JsonSerializer.Serialize(document, SerializerOptions);
        DeserializeDocument(targetJson);
        durableFileWriter.WriteText(
            pathResolver.ResolveRootPath(),
            Path.Combine(migrationRoot, "preferences.v2.staged.json"),
            targetJson,
            DurableFileWriteOptions.Private);
        durableFileWriter.WriteText(
            pathResolver.ResolveRootPath(),
            path,
            targetJson,
            DurableFileWriteOptions.Private);
        WriteMigrationCommitMarkerLocked(backupJson, targetJson, document.Applications?.Count ?? 0);
        return document;
    }

    private void EnsureMigrationCommitMarkerLocked(
        string targetJson,
        FileApplicationPreferencesDocument document)
    {
        string migrationRoot = ResolveMigrationRoot();
        string backupPath = Path.Combine(migrationRoot, "preferences.v1.backup.json");
        string stagedPath = Path.Combine(migrationRoot, "preferences.v2.staged.json");
        string commitPath = Path.Combine(migrationRoot, "commit.json");
        if (!File.Exists(backupPath) || File.Exists(commitPath))
        {
            return;
        }

        if (!File.Exists(stagedPath))
        {
            throw new InvalidOperationException(
                "The preferred file application migration is missing its staged payload.");
        }

        string stagedJson = File.ReadAllText(stagedPath);
        if (!string.Equals(ComputeSha256(stagedJson), ComputeSha256(targetJson), StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The preferred file application migration stage does not match the committed settings.");
        }

        string backupJson = MigrationBackupIntegrity.ReadVerified(backupPath);
        WriteMigrationCommitMarkerLocked(
            backupJson,
            targetJson,
            document.Applications?.Count ?? 0);
    }

    private void WriteMigrationCommitMarkerLocked(string sourceJson, string targetJson, int recordCount)
    {
        string manifestJson = JsonSerializer.Serialize(new FileApplicationMigrationManifest
        {
            SourceSha256 = ComputeSha256(sourceJson),
            TargetSha256 = ComputeSha256(targetJson),
            RecordCount = recordCount,
            CommittedAtUtc = DateTimeOffset.UtcNow
        }, SerializerOptions);
        durableFileWriter.WriteText(
            pathResolver.ResolveRootPath(),
            Path.Combine(ResolveMigrationRoot(), "commit.json"),
            manifestJson,
            DurableFileWriteOptions.Private);
    }

    private string ResolveMigrationRoot()
        => Path.Combine(pathResolver.ResolveRootPath(), "migrations", MigrationDirectoryName);

    private static DurableFileWriteOptions CreateNewPrivateWriteOptions()
        => new()
        {
            CommitMode = DurableFileCommitMode.CreateNew,
            RequirePrivateUnixMode = true
        };

    private static FileApplicationPreferencesDocument DeserializeDocument(string json)
        => JsonSerializer.Deserialize<FileApplicationPreferencesDocument>(json, SerializerOptions)
            ?? throw new InvalidOperationException("The preferred file application document is empty.");

    private static FileApplicationMigrationManifest DeserializeMigrationManifest(string json)
    {
        FileApplicationMigrationManifest manifest = JsonSerializer.Deserialize<FileApplicationMigrationManifest>(
            json,
            SerializerOptions)
            ?? throw new InvalidOperationException(
                "The preferred file application migration commit marker is empty.");
        if (manifest.FormatVersion != 1 || manifest.State != FileApplicationMigrationState.PointerCommitted)
        {
            throw new InvalidOperationException(
                "The preferred file application migration commit marker is invalid.");
        }

        return manifest;
    }

    private static string ComputeSha256(string value)
        => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private IDisposable AcquireCoordination(CancellationToken cancellationToken = default)
        => ControlPlaneFileCoordination.Acquire(
            durableFileWriter,
            pathResolver.ResolveRootPath(),
            ControlPlaneCoordinationScope.FileApplicationPreferences,
            cancellationToken);

    private static string ValidateExecutablePath(string executablePath, bool requireExistingFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        try
        {
            PhysicalPathSyntaxPolicy.EnsureNativeOrRelative(
                executablePath,
                "preferred application executable path");
        }
        catch (InvalidOperationException exception)
        {
            throw new ArgumentException(
                "The preferred application executable path uses syntax that is not valid for this host.",
                nameof(executablePath),
                exception);
        }
        if (!Path.IsPathFullyQualified(executablePath))
        {
            throw new ArgumentException(
                "The preferred application executable path must be absolute.",
                nameof(executablePath));
        }

        string fullPath = Path.GetFullPath(executablePath);
        if (PhysicalPathSyntaxPolicy.Classify(fullPath) == CanDoItAll.SharedKernel.PhysicalPathSyntax.WindowsUnc)
        {
            throw new ArgumentException(
                "A preferred application executable must be installed on the local machine.",
                nameof(executablePath));
        }

        if (requireExistingFile && !File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                "The preferred application executable does not exist.",
                fullPath);
        }

        return fullPath;
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed class FileApplicationPreferencesDocument
    {
        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        public List<FileApplicationPreferenceRecord>? Applications { get; set; } = [];
    }

    private enum FileApplicationMigrationState
    {
        PointerCommitted
    }

    private sealed class FileApplicationMigrationManifest
    {
        public int FormatVersion { get; set; } = 1;

        public FileApplicationMigrationState State { get; set; } = FileApplicationMigrationState.PointerCommitted;

        public string SourceSha256 { get; set; } = string.Empty;

        public string TargetSha256 { get; set; } = string.Empty;

        public int RecordCount { get; set; }

        public DateTimeOffset CommittedAtUtc { get; set; }
    }

    private sealed class FileApplicationPreferenceRecord
    {
        public string Extension { get; set; } = string.Empty;

        public HostBoundPathRecord? Executable { get; set; }

        [JsonPropertyName("executablePath")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? LegacyExecutablePath { get; set; }
    }
}
