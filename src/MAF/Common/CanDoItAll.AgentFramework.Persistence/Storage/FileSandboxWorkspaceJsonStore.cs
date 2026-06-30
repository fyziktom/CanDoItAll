using System.Text;
using System.Text.Json;

namespace CanDoItAll.AgentFramework.Persistence;

internal sealed class FileSandboxWorkspaceJsonStore
{
    private static readonly char[] InvalidFileNameCharacters = Path.GetInvalidFileNameChars();
    private static readonly FileShare SharedReadFileShare = FileShare.ReadWrite | FileShare.Delete;
    private static readonly TimeSpan[] AtomicWriteRetryDelays =
    [
        TimeSpan.FromMilliseconds(25),
        TimeSpan.FromMilliseconds(50),
        TimeSpan.FromMilliseconds(100),
        TimeSpan.FromMilliseconds(200),
        TimeSpan.FromMilliseconds(400)
    ];
    public JsonSerializerOptions SerializerOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public bool RequiresSave<T>(T current, T normalized)
    {
        return JsonSerializer.Serialize(current, SerializerOptions) != JsonSerializer.Serialize(normalized, SerializerOptions);
    }

    public string NormalizeFileName(string value)
    {
        return string.Concat(value.Select(character => InvalidFileNameCharacters.Contains(character) ? '-' : character));
    }

    public async Task<T?> ReadJsonAsync<T>(string fullPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
        {
            throw new ArgumentException("Workspace JSON path must be provided.", nameof(fullPath));
        }

        const int maxTransientReadRetries = 3;
        for (var attempt = 0; attempt <= maxTransientReadRetries; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var stream = TryOpenSharedReadStream(fullPath);
                if (stream is null)
                {
                    return default;
                }

                await using (stream)
                {
                    return await JsonSerializer.DeserializeAsync<T>(stream, SerializerOptions, cancellationToken);
                }
            }
            catch (Exception exception) when (IsTransientJsonReadException(exception) && attempt < maxTransientReadRetries)
            {
                await Task.Delay(GetSharedJsonReadRetryDelay(attempt), cancellationToken);
            }
            catch (Exception exception) when (IsTransientJsonReadException(exception))
            {
                throw CreateWorkspaceJsonReadException<T>(fullPath, exception);
            }
            catch (JsonException exception)
            {
                throw CreateWorkspaceJsonReadException<T>(fullPath, exception);
            }
        }

        throw CreateWorkspaceJsonReadException<T>(fullPath, null);
    }

    public async Task<string> ReadTextAsync(string fullPath, CancellationToken cancellationToken)
    {
        var stream = TryOpenSharedReadStream(fullPath);
        if (stream is null)
        {
            return string.Empty;
        }

        await using (stream)
        {
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            return await reader.ReadToEndAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyList<T>> LoadRecordsFromDirectoryAsync<T>(
        string directoryPath,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(directoryPath))
        {
            return [];
        }

        var records = new List<T>();
        foreach (var filePath in Directory.EnumerateFiles(directoryPath, "*.json").OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
        {
            var record = await ReadJsonAsync<T>(filePath, cancellationToken);
            if (record is not null)
            {
                records.Add(record);
            }
        }

        return records;
    }

    public async Task<bool> PersistRecordDirectoryAsync<T>(
        string directoryPath,
        IEnumerable<T> items,
        Func<T, string> fileNameSelector,
        CancellationToken cancellationToken)
    {
        var materialized = items.ToList();
        if (materialized.Count == 0)
        {
            if (!Directory.Exists(directoryPath))
            {
                return false;
            }

            Directory.Delete(directoryPath, recursive: true);
            return true;
        }

        Directory.CreateDirectory(directoryPath);
        var existingFiles = Directory.EnumerateFiles(directoryPath, "*.json")
            .ToDictionary(path => Path.GetFileName(path)!, StringComparer.OrdinalIgnoreCase);
        var changed = false;
        var desiredFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in materialized)
        {
            var fileName = fileNameSelector(item);
            desiredFiles.Add(fileName);
            changed |= await WriteJsonIfChangedAsync(Path.Combine(directoryPath, fileName), item, cancellationToken);
        }

        foreach (var existingFile in existingFiles)
        {
            if (existingFile.Key is null || desiredFiles.Contains(existingFile.Key))
            {
                continue;
            }

            File.Delete(existingFile.Value);
            changed = true;
        }

        return changed;
    }

    public async Task<bool> PersistRecordDirectoryDiffAsync<T>(
        string directoryPath,
        IEnumerable<T> previousItems,
        IEnumerable<T> items,
        Func<T, string> fileNameSelector,
        CancellationToken cancellationToken)
    {
        var previousByFileName = previousItems.ToDictionary(fileNameSelector, item => item, StringComparer.OrdinalIgnoreCase);
        var currentByFileName = items.ToDictionary(fileNameSelector, item => item, StringComparer.OrdinalIgnoreCase);

        if (currentByFileName.Count == 0)
        {
            if (!Directory.Exists(directoryPath))
            {
                return false;
            }

            Directory.Delete(directoryPath, recursive: true);
            return true;
        }

        Directory.CreateDirectory(directoryPath);
        var changed = false;

        foreach (var current in currentByFileName)
        {
            var filePath = Path.Combine(directoryPath, current.Key);
            if (previousByFileName.TryGetValue(current.Key, out var previous) &&
                EqualityComparer<T>.Default.Equals(previous, current.Value) &&
                File.Exists(filePath))
            {
                continue;
            }

            changed |= await WriteJsonIfChangedAsync(filePath, current.Value, cancellationToken);
        }

        foreach (var removedFileName in previousByFileName.Keys.Except(currentByFileName.Keys, StringComparer.OrdinalIgnoreCase))
        {
            var filePath = Path.Combine(directoryPath, removedFileName);
            if (!File.Exists(filePath))
            {
                continue;
            }

            File.Delete(filePath);
            changed = true;
        }

        return changed;
    }

    public async Task<bool> WriteJsonIfChangedAsync<T>(string fullPath, T payload, CancellationToken cancellationToken)
    {
        var serialized = JsonSerializer.Serialize(payload, SerializerOptions);
        if (File.Exists(fullPath))
        {
            var existing = await ReadTextAsync(fullPath, cancellationToken);
            if (string.Equals(existing, serialized, StringComparison.Ordinal))
            {
                return false;
            }
        }

        await WriteJsonAtomicallyAsync(fullPath, serialized, cancellationToken);
        return true;
    }

    public async Task WriteJsonAtomicallyAsync<T>(string fullPath, T payload, CancellationToken cancellationToken)
    {
        await WriteJsonAtomicallyAsync(
            fullPath,
            JsonSerializer.Serialize(payload, SerializerOptions),
            cancellationToken);
    }

    private static FileStream? TryOpenSharedReadStream(string fullPath)
    {
        if (!File.Exists(fullPath))
        {
            return null;
        }

        try
        {
            return OpenSharedReadStream(fullPath);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }

    private static bool IsTransientJsonReadException(Exception exception)
    {
        return exception is IOException or NullReferenceException;
    }

    private static TimeSpan GetSharedJsonReadRetryDelay(int attempt)
    {
        return attempt switch
        {
            0 => TimeSpan.FromMilliseconds(25),
            1 => TimeSpan.FromMilliseconds(50),
            _ => TimeSpan.FromMilliseconds(100)
        };
    }

    private static InvalidDataException CreateWorkspaceJsonReadException<T>(string fullPath, Exception? innerException)
    {
        return new InvalidDataException(
            $"Could not read workspace JSON file '{fullPath}' as {typeof(T).Name}.",
            innerException);
    }

    private async Task WriteJsonAtomicallyAsync(string fullPath, string serialized, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(directory);

        var tempPath = Path.Combine(directory, $"{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllTextAsync(tempPath, serialized, Encoding.UTF8, cancellationToken);
            await ReplaceAtomicallyWithRetryAsync(tempPath, fullPath, cancellationToken);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static FileStream OpenSharedReadStream(string fullPath)
    {
        return new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            SharedReadFileShare,
            bufferSize: 4096,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);
    }

    private static async Task ReplaceAtomicallyWithRetryAsync(
        string tempPath,
        string fullPath,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (File.Exists(fullPath))
                {
                    File.Replace(tempPath, fullPath, destinationBackupFileName: null, ignoreMetadataErrors: true);
                }
                else
                {
                    File.Move(tempPath, fullPath);
                }

                return;
            }
            catch (IOException) when (attempt < AtomicWriteRetryDelays.Length)
            {
                await Task.Delay(AtomicWriteRetryDelays[attempt], cancellationToken);
            }
            catch (IOException)
            {
                await ReplaceByOverwriteWithRetryAsync(tempPath, fullPath, cancellationToken);
                return;
            }
        }
    }

    private static async Task ReplaceByOverwriteWithRetryAsync(
        string tempPath,
        string fullPath,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                File.Copy(tempPath, fullPath, overwrite: true);
                return;
            }
            catch (IOException) when (attempt < AtomicWriteRetryDelays.Length)
            {
                await Task.Delay(AtomicWriteRetryDelays[attempt], cancellationToken);
            }
        }
    }
}
