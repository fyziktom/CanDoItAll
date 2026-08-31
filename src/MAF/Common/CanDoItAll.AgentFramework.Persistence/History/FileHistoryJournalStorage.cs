using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;

namespace CanDoItAll.AgentFramework.Persistence;

internal sealed class FileHistoryJournalStorage {
    internal const int MaximumRecordBytes = 8 * 1024 * 1024;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly IPhysicalFileSystemPathPolicy paths;
    private readonly DurableFileWriter writer;
    private readonly string journalRoot;
    private readonly FileSandboxWorkspaceStorageLayout layout;

    internal FileHistoryJournalStorage(FileSandboxWorkspaceStorageLayout layout) {
        this.layout = layout;
        var factory = new PhysicalFileSystemPathPolicyFactory();
        paths = factory.Create(layout.RootPath);
        writer = new DurableFileWriter(factory);
        journalRoot = Path.Combine(layout.RootPath, ".provider-history",
            layout.Scope.IsDefaultSandbox ? "default" : layout.Scope.PartitionRelativePath);
        WorkspaceLock = new(layout.RootPath, layout.WorkspaceLockPath, writer);
        BackfillLock = new(layout.RootPath, Path.Combine(journalRoot, "backfill.lock"), writer);
    }

    internal async Task MarkReadyAsync(FileHistoryKey key, CancellationToken cancellationToken) {
        if (key.PartitionId is { } partition) {
            using var queue = new FileHistoryReadyQueue(layout.RootPath);
            await queue.MarkAsync(layout.Scope, partition, cancellationToken);
        }
    }

    internal async Task ClearReadyIfDrainedAsync(HistoryPartition partition, CancellationToken cancellationToken) {
        if (!PendingPaths(partition).Any()) {
            using var queue = new FileHistoryReadyQueue(layout.RootPath);
            await queue.RemoveAsync(layout.Scope, partition.StorageLineageId, cancellationToken);
        }
    }

    internal FileSandboxWorkspaceCrossProcessLock WorkspaceLock { get; }
    internal FileSandboxWorkspaceCrossProcessLock BackfillLock { get; }
    internal string BackfillRoot => Path.Combine(journalRoot, "backfill");
    internal string Relative(string fullPath) {
        paths.EnsureSafePath(fullPath, allowMissingLeaf: true);
        return Path.GetRelativePath(paths.RootPath, fullPath);
    }

    internal string Absolute(string relativePath) {
        var full = paths.ResolveContainedPath(relativePath);
        paths.EnsureSafePath(full, allowMissingLeaf: true);
        return full;
    }

    internal static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    internal async Task<string?> ReadSourceHashAsync(string relativePath, CancellationToken cancellationToken) {
        var fullPath = Absolute(relativePath);
        if (!File.Exists(fullPath)) {
            return null;
        }
        await using var stream = OpenRead(fullPath);
        if (stream.Length > MaximumRecordBytes) {
            throw new InvalidDataException("History source metadata exceeds its bounded file size.");
        }
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
    }

    internal async Task<T?> ReadAsync<T>(string fullPath, CancellationToken cancellationToken) {
        paths.EnsureSafePath(fullPath, allowMissingLeaf: true);
        if (!File.Exists(fullPath)) {
            return default;
        }
        await using var stream = OpenRead(fullPath);
        if (stream.Length > MaximumRecordBytes) {
            throw new InvalidDataException("History metadata exceeds its bounded file size.");
        }
        return await JsonSerializer.DeserializeAsync<T>(stream, Json, cancellationToken)
            ?? throw new InvalidDataException("History metadata cannot contain a null record.");
    }

    internal Task WriteAsync<T>(string fullPath, T value, CancellationToken cancellationToken) {
        var text = JsonSerializer.Serialize(value, Json);
        if (Encoding.UTF8.GetByteCount(text) > MaximumRecordBytes) {
            throw new InvalidDataException("History metadata exceeds its bounded file size.");
        }
        return writer.WriteTextAsync(paths.RootPath, fullPath, text, cancellationToken: cancellationToken);
    }

    internal Task DeleteAsync(string path, CancellationToken cancellationToken)
        => writer.DeleteAsync(paths.RootPath, path, cancellationToken: cancellationToken);

    internal string HeadPath(FileHistoryKey key, bool pending)
        => Path.Combine(journalRoot, key.PartitionId?.ToString("N") ?? "legacy",
            $"{key.EvidenceId:N}.{(pending ? "pending" : "state")}.json");

    internal string BindingPath(Guid evidenceId) => Path.Combine(journalRoot, "bindings", $"{evidenceId:N}.json");

    internal async Task<FileHistoryKey> ResolveKeyAsync(FileHistoryFact fact, CancellationToken cancellationToken) {
        var id = Guid.ParseExact(fact.Owner.EvidenceId.Value, "N");
        var binding = fact.Partition ?? await ReadAsync<HistoryPartition?>(BindingPath(id), cancellationToken);
        return new(id, binding?.StorageLineageId);
    }

    internal async Task<FileHistoryHead?> ReadHeadAsync(FileHistoryKey key, CancellationToken cancellationToken) {
        var pending = await ReadAsync<FileHistoryHead>(HeadPath(key, true), cancellationToken);
        var state = await ReadAsync<FileHistoryHead>(HeadPath(key, false), cancellationToken);
        if (pending is null && state is null && key.PartitionId is { } partitionId) {
            var binding = await ReadAsync<HistoryPartition?>(BindingPath(key.EvidenceId), cancellationToken);
            if (binding?.StorageLineageId == partitionId) {
                var legacy = await ReadHeadAsync(new(key.EvidenceId, null), cancellationToken);
                return legacy is null ? null : legacy with { Key = key };
            }
        }
        if (pending is null) {
            return state;
        }
        if (state is null) {
            return pending;
        }
        return state.HighVersion > pending.HighVersion ? state
            : pending with { AcknowledgedVersion = Math.Max(pending.AcknowledgedVersion, state.AcknowledgedVersion) };
    }

    internal IEnumerable<string> PendingPaths(HistoryPartition partition) {
        foreach (var key in new[] { partition.StorageLineageId.ToString("N"), "legacy" }) {
            var directory = Path.Combine(journalRoot, key);
            paths.EnsureSafePath(directory, allowMissingLeaf: true);
            if (Directory.Exists(directory)) {
                foreach (var path in Directory.EnumerateFiles(directory, "*.pending.json")) {
                    yield return path;
                }
            }
        }
    }

    private static FileStream OpenRead(string path) => new(path, FileMode.Open, FileAccess.Read,
        FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
}
