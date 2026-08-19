using System.Text;
using System.Text.Json;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.AgentFramework.Persistence;

internal enum FileSandboxWorkspaceJsonReadKind
{
    Deserialization,
    RawText
}

internal readonly record struct FileSandboxWorkspacePhysicalJsonRead(
    string FullPath,
    Type? PayloadType,
    FileSandboxWorkspaceJsonReadKind Kind,
    long LengthBytes);

internal sealed class FileSandboxWorkspaceJsonReadDiagnostics
{
    private readonly object sync = new();
    private readonly Action<FileSandboxWorkspacePhysicalJsonRead> record;

    public FileSandboxWorkspaceJsonReadDiagnostics(
        Action<FileSandboxWorkspacePhysicalJsonRead> record)
    {
        ArgumentNullException.ThrowIfNull(record);
        this.record = record;
    }

    public void Record(FileSandboxWorkspacePhysicalJsonRead physicalRead)
    {
        lock (sync)
        {
            record(physicalRead);
        }
    }
}

internal sealed class FileSandboxWorkspaceJsonStore
{
    private static readonly FileShare SharedReadFileShare = FileShare.ReadWrite | FileShare.Delete;
    private readonly FileSandboxWorkspaceJsonReadDiagnostics? readDiagnostics;
    private readonly IPhysicalFileSystemPathPolicyFactory physicalPathPolicyFactory;
    private readonly DurableFileWriter durableFileWriter;
    private readonly string? managedRoot;

    public FileSandboxWorkspaceJsonStore(
        FileSandboxWorkspaceJsonReadDiagnostics? readDiagnostics = null)
        : this(
            readDiagnostics,
            new PhysicalFileSystemPathPolicyFactory(),
            durableFileWriter: null,
            managedRoot: null)
    {
    }

    internal FileSandboxWorkspaceJsonStore(
        FileSandboxWorkspaceJsonReadDiagnostics? readDiagnostics,
        IPhysicalFileSystemPathPolicyFactory physicalPathPolicyFactory,
        DurableFileWriter? durableFileWriter,
        string? managedRoot = null)
    {
        this.readDiagnostics = readDiagnostics;
        this.physicalPathPolicyFactory = physicalPathPolicyFactory ?? throw new ArgumentNullException(nameof(physicalPathPolicyFactory));
        this.durableFileWriter = durableFileWriter ?? new DurableFileWriter(physicalPathPolicyFactory);
        this.managedRoot = string.IsNullOrWhiteSpace(managedRoot)
            ? null
            : physicalPathPolicyFactory.Create(managedRoot).RootPath;
    }

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
        return PortablePhysicalFileNamePolicy.Encode(value).PhysicalName;
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
                    readDiagnostics?.Record(
                        new FileSandboxWorkspacePhysicalJsonRead(
                            fullPath,
                            typeof(T),
                            FileSandboxWorkspaceJsonReadKind.Deserialization,
                            stream.Length));
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
        EnsureSafeFilePath(fullPath, allowMissingLeaf: true);
        var stream = TryOpenSharedReadStream(fullPath);
        if (stream is null)
        {
            return string.Empty;
        }

        await using (stream)
        {
            readDiagnostics?.Record(
                new FileSandboxWorkspacePhysicalJsonRead(
                    fullPath,
                    PayloadType: null,
                    Kind: FileSandboxWorkspaceJsonReadKind.RawText,
                    LengthBytes: stream.Length));
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

        EnsureSafeDirectory(directoryPath);
        var records = new List<T>();
        foreach (var filePath in Directory.EnumerateFiles(directoryPath, "*.json")
                     .OrderBy(Path.GetFileName, StringComparer.Ordinal)
                     .ThenBy(path => path, StringComparer.Ordinal))
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

            DeleteDirectory(directoryPath);
            return true;
        }

        EnsureDirectory(directoryPath);
        var existingFiles = Directory.EnumerateFiles(directoryPath, "*.json")
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .ThenBy(path => path, StringComparer.Ordinal)
            .ToDictionary(path => Path.GetFileName(path)!, StringComparer.Ordinal);
        var changed = false;
        var desiredFiles = new HashSet<string>(StringComparer.Ordinal);

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

            await DeleteFileAsync(existingFile.Value, cancellationToken);
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
        var previousByFileName = previousItems.ToDictionary(fileNameSelector, item => item, StringComparer.Ordinal);
        var currentByFileName = items.ToDictionary(fileNameSelector, item => item, StringComparer.Ordinal);

        if (currentByFileName.Count == 0)
        {
            if (!Directory.Exists(directoryPath))
            {
                return false;
            }

            DeleteDirectory(directoryPath);
            return true;
        }

        EnsureDirectory(directoryPath);
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

        foreach (var removedFileName in previousByFileName.Keys
                     .Except(currentByFileName.Keys, StringComparer.Ordinal)
                     .Order(StringComparer.Ordinal))
        {
            var filePath = Path.Combine(directoryPath, removedFileName);
            if (!File.Exists(filePath))
            {
                continue;
            }

            await DeleteFileAsync(filePath, cancellationToken);
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

    private FileStream? TryOpenSharedReadStream(string fullPath)
    {
        EnsureSafeFilePath(fullPath, allowMissingLeaf: true);
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
        string directory = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("Workspace JSON path does not have a parent directory.");
        await durableFileWriter.WriteTextAsync(
            ResolveManagedRoot(directory),
            fullPath,
            serialized,
            cancellationToken: cancellationToken);
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

    private void EnsureSafeFilePath(string fullPath, bool allowMissingLeaf)
    {
        string directory = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("Workspace JSON path does not have a parent directory.");
        physicalPathPolicyFactory.Create(ResolveManagedRoot(directory)).EnsureSafePath(fullPath, allowMissingLeaf);
    }

    public void EnsureDirectory(string directoryPath)
        => durableFileWriter.EnsureDirectory(
            ResolveManagedRoot(directoryPath),
            directoryPath,
            requirePrivateUnixMode: false);

    private void EnsureSafeDirectory(string directoryPath)
        => physicalPathPolicyFactory.Create(ResolveManagedRoot(directoryPath)).EnsureSafePath(directoryPath);

    public void DeleteDirectory(string directoryPath)
    {
        EnsureSafeDirectory(directoryPath);
        Directory.Delete(directoryPath, recursive: true);
    }

    public async Task DeleteFileAsync(string fullPath, CancellationToken cancellationToken)
    {
        string directory = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("Workspace JSON path does not have a parent directory.");
        await durableFileWriter.DeleteAsync(
            ResolveManagedRoot(directory),
            fullPath,
            cancellationToken: cancellationToken);
    }

    private string ResolveManagedRoot(string fallbackRoot)
        => managedRoot ?? fallbackRoot;
}
