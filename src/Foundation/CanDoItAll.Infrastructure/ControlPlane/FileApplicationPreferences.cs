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
    public FileApplicationPreference(FileApplicationExtension extension, string executablePath)
    {
        if (string.IsNullOrWhiteSpace(extension.Value))
        {
            throw new ArgumentException("A file extension is required.", nameof(extension));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        Extension = extension;
        ExecutablePath = executablePath.Trim();
    }

    public FileApplicationExtension Extension { get; }

    public string ExecutablePath { get; }
}

public interface IFileApplicationPreferenceService
{
    Task<IReadOnlyList<FileApplicationPreference>> ListAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(FileApplicationPreference preference, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(FileApplicationExtension extension, CancellationToken cancellationToken = default);

    FileApplicationPreference? ResolveForFile(string fileName);
}

public sealed class FileApplicationPreferenceService(
    IControlPlanePathResolver pathResolver,
    ILogger<FileApplicationPreferenceService> logger) : IFileApplicationPreferenceService
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();
    private readonly object sync = new();

    public Task<IReadOnlyList<FileApplicationPreference>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (sync)
        {
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
        var normalized = new FileApplicationPreference(preference.Extension, executablePath);
        lock (sync)
        {
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
            return ReadLocked().FirstOrDefault(preference => preference.Extension == extension);
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
            FileApplicationPreferencesDocument document =
                JsonSerializer.Deserialize<FileApplicationPreferencesDocument>(json, SerializerOptions)
                ?? throw new InvalidOperationException("The preferred file application document is empty.");
            if (document.SchemaVersion != FileApplicationPreferencesDocument.CurrentSchemaVersion)
            {
                throw new InvalidOperationException(
                    $"Unsupported preferred file application schema version '{document.SchemaVersion}'.");
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

                string executablePath = ValidateExecutablePath(record.ExecutablePath, requireExistingFile: false);
                preferences.Add(new FileApplicationPreference(extension, executablePath));
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
        string directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("Unable to resolve the preferred application settings directory.");
        Directory.CreateDirectory(directory);
        var document = new FileApplicationPreferencesDocument
        {
            Applications = preferences
                .OrderBy(preference => preference.Extension.Value, StringComparer.Ordinal)
                .Select(preference => new FileApplicationPreferenceRecord
                {
                    Extension = preference.Extension.Value,
                    ExecutablePath = preference.ExecutablePath
                })
                .ToList()
        };
        string temporaryPath = Path.Combine(
            directory,
            $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(document, SerializerOptions));
        File.Move(temporaryPath, path, true);
    }

    private static string ValidateExecutablePath(string executablePath, bool requireExistingFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        if (!Path.IsPathFullyQualified(executablePath))
        {
            throw new ArgumentException(
                "The preferred application executable path must be absolute.",
                nameof(executablePath));
        }

        string fullPath = Path.GetFullPath(executablePath);
        if (OperatingSystem.IsWindows() && fullPath.StartsWith("\\\\", StringComparison.Ordinal))
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
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        public List<FileApplicationPreferenceRecord>? Applications { get; set; } = [];
    }

    private sealed class FileApplicationPreferenceRecord
    {
        public string Extension { get; set; } = string.Empty;

        public string ExecutablePath { get; set; } = string.Empty;
    }
}
