using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.ProviderHistory;
using System.Security.Cryptography;
using System.Text;

namespace CanDoItAll.AgentFramework.Persistence;

public sealed class FileHistoryReadyQueue : IDisposable {
    private readonly FileHistoryJournalStorage storage;
    private readonly string root;
    private IEnumerator<string>? pending;
    private Guid? currentPartition;

    public FileHistoryReadyQueue(string workspaceRoot) {
        storage = new(new(workspaceRoot));
        root = Path.Combine(workspaceRoot, ".provider-history", "ready");
    }

    internal Task MarkAsync(WorkspaceScopeDescriptor scope, Guid partition, CancellationToken cancellationToken)
        => storage.WriteAsync(MarkerPath(scope, partition), new FileHistoryReadyScope(scope.Kind, scope.Key, partition), cancellationToken);

    internal Task RemoveAsync(WorkspaceScopeDescriptor scope, Guid partition, CancellationToken cancellationToken)
        => storage.DeleteAsync(MarkerPath(scope, partition), cancellationToken);

    public async Task<WorkspaceScopeDescriptor?> NextAsync(HistoryPartition partition, CancellationToken cancellationToken) {
        if (currentPartition != partition.StorageLineageId) {
            Dispose();
            currentPartition = partition.StorageLineageId;
        }
        var directory = Path.Combine(root, partition.StorageLineageId.ToString("N"));
        storage.Relative(directory);
        if (!Directory.Exists(directory)) {
            return null;
        }
        pending ??= Directory.EnumerateFiles(directory, "*.json").GetEnumerator();
        for (var checkedFiles = 0; checkedFiles < 32; checkedFiles++) {
            cancellationToken.ThrowIfCancellationRequested();
            if (!pending.MoveNext()) {
                Dispose();
                return null;
            }
            var marker = await storage.ReadAsync<FileHistoryReadyScope>(pending.Current, cancellationToken);
            if (marker is null) {
                continue;
            }
            if (marker.PartitionId != partition.StorageLineageId || !Enum.IsDefined(marker.Kind)) {
                throw new InvalidDataException("The file history queue contains an invalid scope.");
            }
            var scope = new WorkspaceScopeDescriptor(marker.Kind, marker.Key);
            if (scope.Key != marker.Key || !string.Equals(MarkerPath(scope, marker.PartitionId), pending.Current, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidDataException("The file history queue scope does not match its marker.");
            }
            return scope;
        }
        return null;
    }

    public bool HasPending(HistoryPartition partition) {
        var directory = Path.Combine(root, partition.StorageLineageId.ToString("N"));
        storage.Relative(directory);
        return Directory.Exists(directory) && Directory.EnumerateFiles(directory, "*.json").Any();
    }

    private string MarkerPath(WorkspaceScopeDescriptor scope, Guid partition) {
        var identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(scope.DisplayName)));
        return Path.Combine(root, partition.ToString("N"), identity + ".json");
    }

    public void Dispose() {
        pending?.Dispose();
        pending = null;
    }

    private sealed record FileHistoryReadyScope(WorkspaceScopeKind Kind, string Key, Guid PartitionId);
}
