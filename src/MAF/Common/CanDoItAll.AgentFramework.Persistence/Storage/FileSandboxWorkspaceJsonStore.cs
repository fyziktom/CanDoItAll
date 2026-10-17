using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.AgentFramework.Persistence;

internal enum FileSandboxWorkspaceJsonReadKind
{
    Deserialization,
    RawText,
    SnapshotValidation,
    PreparedRunValidation
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
    private readonly FileProviderHistoryJournal? historyJournal;
    private readonly FileSandboxWorkspaceSnapshotCache snapshots = new();
    internal FileProviderUsageIndex? UsageIndex { get; set; }

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
        string? managedRoot = null,
        FileProviderHistoryJournal? historyJournal = null)
    {
        this.readDiagnostics = readDiagnostics;
        this.historyJournal = historyJournal;
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

    public bool RequiresSave<T>(T current, T normalized) {
        using var activity = AgentFrameworkTelemetry.ActivitySource.StartActivity("workspace.json.compare");
        if (ReferenceEquals(current, normalized)) {
            return false;
        }
        if (current is ExecutionRunRecord previousRun && normalized is ExecutionRunRecord nextRun &&
            previousRun.Revision != nextRun.Revision) {
            return true;
        }
        if (current is ExecutionRunDetail previous && normalized is ExecutionRunDetail next) {
            if (previous.Run.Revision != next.Run.Revision) {
                return true;
            }
            if (previous with {
                    ExecutionLog = next.ExecutionLog, Metrics = next.Metrics, Approvals = next.Approvals,
                    Artifacts = next.Artifacts, Checkpoints = next.Checkpoints, ToolReceipts = next.ToolReceipts,
                    UsageObservations = next.UsageObservations
                } == next &&
                previous.ExecutionLog.SequenceEqual(next.ExecutionLog) && previous.Metrics.SequenceEqual(next.Metrics) &&
                previous.Approvals.SequenceEqual(next.Approvals) && previous.Artifacts.SequenceEqual(next.Artifacts) &&
                previous.Checkpoints.SequenceEqual(next.Checkpoints) && previous.ToolReceipts.SequenceEqual(next.ToolReceipts) &&
                previous.UsageObservations.SequenceEqual(next.UsageObservations)) {
                return false;
            }
        }
        return Serialize(current) != Serialize(normalized);
    }

    public string NormalizeFileName(string value)
    {
        return PortablePhysicalFileNamePolicy.Encode(value).PhysicalName;
    }

    public async Task<T?> ReadJsonAsync<T>(string fullPath, CancellationToken cancellationToken)
        => await ReadJsonAsync<T>(fullPath, cancellationToken, readPolicy: null);

    internal async Task<FileSandboxWorkspaceRunReadSnapshot?> ReadRunSnapshotAsync(
        string fullPath, CancellationToken cancellationToken) {
        FileSandboxWorkspaceRunReadSnapshot? snapshot = null;
        var run = await ReadJsonAsync<ExecutionRunRecord>(fullPath, cancellationToken, readPolicy: null,
            (decoded, digest) => {
                snapshot = new(fullPath, decoded, digest);
                return snapshot.Run;
            });
        return run is null ? null : snapshot;
    }

    internal async Task<bool> MatchesRunSnapshotAsync(string fullPath,
        FileSandboxWorkspaceRunReadSnapshot snapshot, CancellationToken cancellationToken) {
        const int maxTransientReadRetries = 3;
        for (var attempt = 0; attempt <= maxTransientReadRetries; attempt++) {
            cancellationToken.ThrowIfCancellationRequested();
            try {
                await using var stream = TryOpenSharedReadStream(fullPath);
                if (stream is null) {
                    return false;
                }
                using var activity = AgentFrameworkTelemetry.ActivitySource.StartActivity("workspace.run.snapshot.validate");
                activity?.SetTag("payload.bytes", stream.Length);
                readDiagnostics?.Record(new(fullPath, typeof(ExecutionRunRecord),
                    FileSandboxWorkspaceJsonReadKind.PreparedRunValidation, stream.Length));
                return snapshot.MatchesDigest(await SHA256.HashDataAsync(stream, cancellationToken));
            } catch (Exception exception) when (IsTransientJsonReadException(exception) && attempt < maxTransientReadRetries) {
                await Task.Delay(GetSharedJsonReadRetryDelay(attempt), cancellationToken);
            } catch (Exception exception) when (IsTransientJsonReadException(exception)) {
                throw CreateWorkspaceJsonReadException<ExecutionRunRecord>(fullPath, exception);
            }
        }
        throw CreateWorkspaceJsonReadException<ExecutionRunRecord>(fullPath, null);
    }

    internal async Task<T?[]> ReadJsonBatchAsync<T>(IReadOnlyList<string> paths, CancellationToken cancellationToken) {
        const int maximumConcurrentReads = 4;
        cancellationToken.ThrowIfCancellationRequested();
        if (paths.Count == 0) {
            return [];
        }
        var readPolicy = managedRoot is null ? null : physicalPathPolicyFactory.Create(managedRoot);
        var results = new T?[paths.Count];
        await Parallel.ForEachAsync(Enumerable.Range(0, paths.Count),
            new ParallelOptions { MaxDegreeOfParallelism = maximumConcurrentReads, CancellationToken = cancellationToken },
            async (index, token) => {
                results[index] = await ReadJsonAsync<T>(paths[index], token, readPolicy);
            });
        return results;
    }

    private async Task<T?> ReadJsonAsync<T>(string fullPath, CancellationToken cancellationToken,
        IPhysicalFileSystemPathPolicy? readPolicy,
        Func<T, string, T>? captureSnapshot = null) {
        using var activity = AgentFrameworkTelemetry.ActivitySource.StartActivity("workspace.json.read");
        activity?.SetTag("payload.type", typeof(T).Name);
        if (string.IsNullOrWhiteSpace(fullPath)) {
            throw new ArgumentException("Workspace JSON path must be provided.", nameof(fullPath));
        }

        const int maxTransientReadRetries = 3;
        for (var attempt = 0; attempt <= maxTransientReadRetries; attempt++) {
            cancellationToken.ThrowIfCancellationRequested();
            try {
                var stream = TryOpenSharedReadStream(fullPath, readPolicy);
                if (stream is null) {
                    snapshots.Remove<T>(fullPath);
                    return default;
                }

                await using (stream) {
                    activity?.SetTag("payload.bytes", stream.Length);
                    if (FileSandboxWorkspaceSnapshotCache.Supports<T>(stream.Length)) {
                        return await ReadSnapshotAsync<T>(fullPath, stream, cancellationToken, captureSnapshot);
                    }
                    readDiagnostics?.Record(
                        new FileSandboxWorkspacePhysicalJsonRead(
                            fullPath,
                            typeof(T),
                            FileSandboxWorkspaceJsonReadKind.Deserialization,
                            stream.Length));
                    using var deserialization = AgentFrameworkTelemetry.ActivitySource.StartActivity("workspace.json.deserialize");
                    if (captureSnapshot is not null) {
                        var length = stream.Length;
                        using var observed = new FileSandboxWorkspaceRunHashingStream(stream);
                        var value = await JsonSerializer.DeserializeAsync<T>(observed, SerializerOptions, cancellationToken);
                        var digest = observed.Complete(length);
                        cancellationToken.ThrowIfCancellationRequested();
                        if (value is null) {
                            return default;
                        }
                        return captureSnapshot(value, digest);
                    }
                    return await JsonSerializer.DeserializeAsync<T>(stream, SerializerOptions, cancellationToken);
                }
            } catch (Exception exception) when (IsTransientJsonReadException(exception) && attempt < maxTransientReadRetries) {
                await Task.Delay(GetSharedJsonReadRetryDelay(attempt), cancellationToken);
            } catch (Exception exception) when (IsTransientJsonReadException(exception)) {
                throw CreateWorkspaceJsonReadException<T>(fullPath, exception);
            } catch (JsonException exception) {
                throw CreateWorkspaceJsonReadException<T>(fullPath, exception);
            }
        }

        throw CreateWorkspaceJsonReadException<T>(fullPath, null);
    }

    private async Task<T?> ReadSnapshotAsync<T>(string fullPath, Stream stream, CancellationToken cancellationToken,
        Func<T, string, T>? captureSnapshot = null) {
        var length = stream.Length;
        if (length > FileSandboxWorkspaceSnapshotCache.MaximumPayloadBytes) {
            throw new IOException("The execution snapshot changed size while it was being read.");
        }
        var bytes = new byte[checked((int)length)];
        await stream.ReadExactlyAsync(bytes, cancellationToken);
        if (stream.ReadByte() != -1) {
            throw new IOException("The execution snapshot changed size while it was being read.");
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (snapshots.TryRead<T>(fullPath, bytes, out var cached)) {
            readDiagnostics?.Record(new(fullPath, typeof(T), FileSandboxWorkspaceJsonReadKind.SnapshotValidation, bytes.Length));
            using var hit = AgentFrameworkTelemetry.ActivitySource.StartActivity("workspace.snapshot.hit");
            return CaptureSnapshot(cached);
        }
        readDiagnostics?.Record(new(fullPath, typeof(T), FileSandboxWorkspaceJsonReadKind.Deserialization, bytes.Length));
        using var deserialization = AgentFrameworkTelemetry.ActivitySource.StartActivity("workspace.json.deserialize");
        ReadOnlySpan<byte> json = bytes;
        if (json.StartsWith(Encoding.UTF8.Preamble)) {
            json = json[Encoding.UTF8.Preamble.Length..];
        }
        var value = JsonSerializer.Deserialize<T>(json, SerializerOptions);
        cancellationToken.ThrowIfCancellationRequested();
        return value is null ? default : CaptureSnapshot(snapshots.Remember(fullPath, bytes, value));

        T? CaptureSnapshot(T? value) {
            if (captureSnapshot is null || value is null) {
                return value;
            }
            return captureSnapshot(value, Convert.ToHexString(SHA256.HashData(bytes)));
        }
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
        var paths = Directory.EnumerateFiles(directoryPath, "*.json")
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .ThenBy(path => path, StringComparer.Ordinal)
            .ToArray();
        var records = await ReadJsonBatchAsync<T>(paths, cancellationToken);
        return records.Where(record => record is not null).Select(record => record!).ToArray();
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

            await DeleteDirectoryAsync(directoryPath, cancellationToken);
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

            await DeleteDirectoryAsync(directoryPath, cancellationToken);
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
        var serialized = Serialize(payload);
        if (File.Exists(fullPath))
        {
            var existing = await ReadTextAsync(fullPath, cancellationToken);
            if (string.Equals(existing, serialized, StringComparison.Ordinal))
            {
                return false;
            }
        }

        await WriteObservedJsonAsync(fullPath, payload, serialized, cancellationToken);
        return true;
    }

    public async Task WriteJsonAtomicallyAsync<T>(string fullPath, T payload, CancellationToken cancellationToken) {
        if (payload is ProviderUsageObservation) {
            await WriteObservedJsonAsync(fullPath, payload, Serialize(payload), cancellationToken);
            return;
        }
        byte[] encoded;
        using (var activity = AgentFrameworkTelemetry.ActivitySource.StartActivity("workspace.json.serialize")) {
            activity?.SetTag("payload.type", typeof(T).Name);
            encoded = JsonSerializer.SerializeToUtf8Bytes(payload, SerializerOptions);
        }
        await WriteEncodedJsonAtomicallyAsync(fullPath, encoded, cancellationToken);
    }

    private async Task WriteObservedJsonAsync<T>(string fullPath, T payload, string serialized, CancellationToken cancellationToken) {
        if (UsageIndex is not null && payload is ProviderUsageObservation usage) {
            await UsageIndex.IncludeAsync(fullPath, usage, cancellationToken);
        }
        var prepared = historyJournal is not null && payload is ProviderUsageObservation observation
            ? await historyJournal.PrepareWriteAsync(fullPath, observation, serialized, cancellationToken) : [];
        await WriteJsonAtomicallyAsync(fullPath, serialized, cancellationToken);
        if (historyJournal is not null) {
            await historyJournal.CommitAsync(prepared, cancellationToken);
        }
    }

    private FileStream? TryOpenSharedReadStream(string fullPath, IPhysicalFileSystemPathPolicy? readPolicy = null)
    {
        using var activity = AgentFrameworkTelemetry.ActivitySource.StartActivity("workspace.json.open");
        if (readPolicy is null) {
            EnsureSafeFilePath(fullPath, allowMissingLeaf: true);
        } else {
            readPolicy.EnsureSafePath(fullPath, allowMissingLeaf: true);
        }
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

    private Task WriteJsonAtomicallyAsync(string fullPath, string serialized, CancellationToken cancellationToken)
        => WriteEncodedJsonAtomicallyAsync(fullPath, Encoding.UTF8.GetBytes(serialized), cancellationToken);

    private async Task WriteEncodedJsonAtomicallyAsync(string fullPath, ReadOnlyMemory<byte> encoded, CancellationToken cancellationToken) {
        using var activity = AgentFrameworkTelemetry.ActivitySource.StartActivity("workspace.json.write");
        activity?.SetTag("payload.bytes", encoded.Length);
        string directory = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("Workspace JSON path does not have a parent directory.");
        await durableFileWriter.WriteBytesAsync(
            ResolveManagedRoot(directory),
            fullPath,
            encoded,
            cancellationToken: cancellationToken);
    }

    private string Serialize<T>(T payload) {
        using var activity = AgentFrameworkTelemetry.ActivitySource.StartActivity("workspace.json.serialize");
        activity?.SetTag("payload.type", typeof(T).Name);
        return JsonSerializer.Serialize(payload, SerializerOptions);
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

    public async Task DeleteDirectoryAsync(string directoryPath, CancellationToken cancellationToken) {
        EnsureSafeDirectory(directoryPath);
        if (historyJournal is not null) {
            var enumeration = new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint };
            foreach (var path in Directory.EnumerateFiles(directoryPath, "*.json", enumeration)) {
                if (historyJournal.IsUsagePath(path)) {
                    await DeleteFileAsync(path, cancellationToken);
                }
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        EnsureSafeDirectory(directoryPath);
        Directory.Delete(directoryPath, recursive: true);
    }

    public async Task DeleteFileAsync(string fullPath, CancellationToken cancellationToken)
    {
        string directory = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("Workspace JSON path does not have a parent directory.");
        var prepared = historyJournal is null ? [] : await historyJournal.PrepareDeleteAsync(fullPath, cancellationToken);
        await durableFileWriter.DeleteAsync(
            ResolveManagedRoot(directory),
            fullPath,
            cancellationToken: cancellationToken);
        if (historyJournal is not null) {
            await historyJournal.CommitAsync(prepared, cancellationToken);
        }
    }

    private string ResolveManagedRoot(string fallbackRoot)
        => managedRoot ?? fallbackRoot;
}
