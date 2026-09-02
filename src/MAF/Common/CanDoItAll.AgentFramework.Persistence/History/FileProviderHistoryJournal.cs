using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.ProviderHistory;

namespace CanDoItAll.AgentFramework.Persistence;

public sealed class FileProviderHistoryJournal {
    private static readonly TimeSpan ReadBudget = TimeSpan.FromMilliseconds(250);
    private readonly TimeProvider clock;
    private readonly FileSandboxWorkspaceStorageLayout layout;
    private readonly FileHistoryJournalStorage storage;
    private readonly Action<FileHistoryCommitStage>? boundary;

    public FileProviderHistoryJournal(string workspaceRoot, WorkspaceScopeDescriptor? scope = null)
        : this(new FileSandboxWorkspaceStorageLayout(workspaceRoot, scope), null) {
    }

    internal FileProviderHistoryJournal(FileSandboxWorkspaceStorageLayout layout, Action<FileHistoryCommitStage>? boundary,
        TimeProvider? clock = null) {
        this.clock = clock ?? TimeProvider.System;
        this.layout = layout;
        storage = new(layout);
        this.boundary = boundary;
    }

    internal bool IsUsagePath(string fullPath) {
        var relative = Path.GetRelativePath(layout.ExecutionStorageRoot, fullPath).Replace('\\', '/').Split('/');
        return relative is ["runs", var run, "usage", var name] && Guid.TryParseExact(run, "N", out _) && IsRecordName(name)
            || relative is ["orphans", "usage", var orphan] && IsRecordName(orphan);
    }

    private static bool IsRecordName(string name)
        => name.EndsWith(".json", StringComparison.Ordinal) && Guid.TryParseExact(name[..^5], "N", out _);

    internal async Task<IReadOnlyList<FileHistoryPrepared>> PrepareWriteAsync(string fullPath,
        ProviderUsageObservation observation, string serialized, CancellationToken cancellationToken) {
        if (!IsUsagePath(fullPath)) {
            throw new InvalidDataException("Provider usage evidence must use its canonical usage file path.");
        }
        var previous = await storage.ReadAsync<ProviderUsageObservation>(fullPath, cancellationToken);
        var oldFact = previous is null ? null : FileHistoryFact.From(previous);
        var next = FileHistoryFact.From(observation);
        var sourcePath = storage.Relative(fullPath);
        var targetHash = FileHistoryJournalStorage.Hash(serialized);
        var writes = new List<FileHistoryPrepared>(2);
        if (oldFact is not null && (next is null || oldFact.Owner != next.Owner || oldFact.Partition != next.Partition)) {
            writes.Add(await PrepareAsync(oldFact, sourcePath, targetHash, true, cancellationToken));
        }
        if (next is not null) {
            writes.Add(await PrepareAsync(next, sourcePath, targetHash, false, cancellationToken));
        }
        return writes;
    }

    internal async Task<IReadOnlyList<FileHistoryPrepared>> PrepareDeleteAsync(string fullPath, CancellationToken cancellationToken) {
        if (!IsUsagePath(fullPath)) {
            return [];
        }
        var observation = await storage.ReadAsync<ProviderUsageObservation>(fullPath, cancellationToken);
        var fact = observation is null ? null : FileHistoryFact.From(observation);
        return fact is null ? [] : [await PrepareAsync(fact, storage.Relative(fullPath), null, true, cancellationToken)];
    }

    private async Task<FileHistoryPrepared> PrepareAsync(FileHistoryFact fact, string sourcePath,
        string? hash, bool deleted, CancellationToken cancellationToken) {
        var key = await storage.ResolveKeyAsync(fact, cancellationToken);
        await storage.MarkReadyAsync(key, cancellationToken);
        var head = await storage.ReadHeadAsync(key, cancellationToken)
            ?? new FileHistoryHead(key, 0, 0, null, null);
        head = await RecoverAsync(head, cancellationToken);
        var version = checked(head.HighVersion + 1);
        var mutation = new FileHistoryMutation(version, fact, sourcePath, hash, deleted);
        head = head with { HighVersion = version, Prepared = mutation };
        await storage.WriteAsync(storage.HeadPath(key, true), head, cancellationToken);
        boundary?.Invoke(FileHistoryCommitStage.Prepared);
        return new(key, version);
    }

    internal async Task CommitAsync(IReadOnlyList<FileHistoryPrepared> writes, CancellationToken cancellationToken) {
        foreach (var write in writes) {
            boundary?.Invoke(FileHistoryCommitStage.SourceCommitted);
            var head = await storage.ReadHeadAsync(write.Key, cancellationToken)
                ?? throw new InvalidDataException("Prepared provider history journal disappeared.");
            if (head.Prepared?.Version != write.Version) {
                throw new InvalidDataException("Prepared provider history version changed.");
            }
            head = await RecoverAsync(head, cancellationToken);
            if (head.Committed?.Version != write.Version) {
                throw new InvalidDataException("Provider history source did not commit its prepared content.");
            }
            boundary?.Invoke(FileHistoryCommitStage.Published);
        }
    }

    public async Task<IReadOnlyList<FileHistoryPublication>> ReadBatchAsync(
        HistoryPartition partition, int maximumFiles, CancellationToken cancellationToken = default) {
        if (maximumFiles is < 1 or > 1000) {
            throw new ArgumentOutOfRangeException(nameof(maximumFiles));
        }
        await using var fileLock = await storage.WorkspaceLock.AcquireAsync(cancellationToken);
        var result = new List<FileHistoryPublication>();
        var seen = new HashSet<(Guid, long)>();
        var started = clock.GetTimestamp();
        var visited = 0;
        foreach (var path in storage.PendingPaths(partition).Take(maximumFiles)) {
            cancellationToken.ThrowIfCancellationRequested();
            if (visited++ > 0 && clock.GetElapsedTime(started) >= ReadBudget) {
                break;
            }
            var head = await storage.ReadAsync<FileHistoryHead>(path, cancellationToken)
                ?? throw new InvalidDataException("Provider history journal disappeared during its locked read.");
            head = await RecoverAsync(await storage.ReadHeadAsync(head.Key, cancellationToken) ?? head, cancellationToken);
            if (head.Committed is not { } committed || committed.Version <= head.AcknowledgedVersion) {
                await ArchiveAsync(head, cancellationToken);
                continue;
            }
            var binding = committed.Fact.Partition
                ?? await storage.ReadAsync<HistoryPartition?>(storage.BindingPath(head.Key.EvidenceId), cancellationToken);
            if (binding is { } existing && existing != partition) {
                continue;
            }
            if (head.Key.PartitionId is null) {
                head = await BindAsync(head, partition, cancellationToken);
                committed = head.Committed!;
            }
            if (committed.Version > head.AcknowledgedVersion && seen.Add((head.Key.EvidenceId, committed.Version))) {
                result.Add(new(head.Key.EvidenceId, committed.Version, committed.Fact.Project(partition, committed.Version, committed.Deleted)));
            }
        }
        return result;
    }

    public async Task ClearReadyIfDrainedAsync(HistoryPartition partition, CancellationToken cancellationToken = default) {
        await using var fileLock = await storage.WorkspaceLock.AcquireAsync(cancellationToken);
        await storage.ClearReadyIfDrainedAsync(partition, cancellationToken);
    }

    public async Task AcknowledgeAsync(FileHistoryPublication publication, CancellationToken cancellationToken = default) {
        await using var fileLock = await storage.WorkspaceLock.AcquireAsync(cancellationToken);
        var key = new FileHistoryKey(publication.EvidenceId, publication.Mutation.Source.Partition.StorageLineageId);
        var head = await storage.ReadHeadAsync(key, cancellationToken)
            ?? throw new InvalidDataException("Acknowledged provider history journal is missing.");
        if (head.Committed is not { } committed || publication.Version < 1 || publication.Version > committed.Version ||
            publication.Mutation.Version.Value != publication.Version ||
            committed.Fact.Owner.OwnerId != publication.Mutation.Source.Owner) {
            throw new InvalidDataException("Cannot acknowledge unpublished provider history.");
        }
        head = head with { AcknowledgedVersion = Math.Max(head.AcknowledgedVersion, publication.Version) };
        await ArchiveAsync(head, cancellationToken);
        boundary?.Invoke(FileHistoryCommitStage.Acknowledged);
    }

    internal async Task<FileHistoryMutation?> ReadCurrentAsync(CanonicalEvidenceReference source, CancellationToken cancellationToken) {
        if (source.Kind != HistorySourceKind.AgentConversation || !Guid.TryParseExact(source.Evidence.Value, "N", out var id)) {
            return null;
        }
        await using var fileLock = await storage.WorkspaceLock.AcquireAsync(cancellationToken);
        var head = await storage.ReadHeadAsync(new(id, source.Partition.StorageLineageId), cancellationToken);
        if (head is null) {
            return null;
        }
        head = await RecoverAsync(head, cancellationToken);
        if (head.Committed is not { } committed || committed.Fact.Owner.OwnerId != source.Owner) {
            return null;
        }
        var partition = committed.Fact.Partition
            ?? await storage.ReadAsync<HistoryPartition?>(storage.BindingPath(id), cancellationToken);
        return partition == source.Partition ? committed : null;
    }

    public async Task<HistorySourceMutation?> ReadAsync(CanonicalEvidenceReference source, CancellationToken cancellationToken = default) {
        var current = await ReadCurrentAsync(source, cancellationToken);
        return current?.Fact.Project(source.Partition, current.Version, current.Deleted);
    }

    public async Task<bool> StageExistingAsync(string relativePath, HistoryPartition partition, CancellationToken cancellationToken = default) {
        await using var fileLock = await storage.WorkspaceLock.AcquireAsync(cancellationToken);
        var fullPath = storage.Absolute(relativePath);
        if (!IsUsagePath(fullPath)) {
            throw new InvalidDataException("History backfill requires a canonical usage source path.");
        }
        var observation = await storage.ReadAsync<ProviderUsageObservation>(fullPath, cancellationToken);
        var fact = observation is null ? null : FileHistoryFact.From(observation);
        if (fact is null || fact.Partition is { } original && original != partition) {
            return false;
        }
        var id = Guid.ParseExact(fact.Owner.EvidenceId.Value, "N");
        if (fact.Partition is null) {
            var bindingPath = storage.BindingPath(id);
            var binding = await storage.ReadAsync<HistoryPartition?>(bindingPath, cancellationToken);
            if (binding is { } existing && existing != partition) {
                return false;
            }
            if (binding is null) {
                await storage.WriteAsync(bindingPath, partition, cancellationToken);
            }
        }
        var key = await storage.ResolveKeyAsync(fact, cancellationToken);
        var head = await storage.ReadHeadAsync(key, cancellationToken);
        if (head is not null && (await RecoverAsync(head, cancellationToken)).Committed is not null) {
            return false;
        }
        var hash = await storage.ReadSourceHashAsync(relativePath, cancellationToken)
            ?? throw new InvalidDataException("History backfill source disappeared during its locked read.");
        var prepared = await PrepareAsync(fact, relativePath, hash, false, cancellationToken);
        await CommitAsync([prepared], cancellationToken);
        return true;
    }

    private async Task<FileHistoryHead> BindAsync(FileHistoryHead head, HistoryPartition partition, CancellationToken cancellationToken) {
        await storage.WriteAsync(storage.BindingPath(head.Key.EvidenceId), partition, cancellationToken);
        boundary?.Invoke(FileHistoryCommitStage.LegacyBindingPersisted);
        var oldKey = head.Key;
        var newKey = new FileHistoryKey(head.Key.EvidenceId, partition.StorageLineageId);
        await storage.MarkReadyAsync(newKey, cancellationToken);
        var newer = await storage.ReadHeadAsync(newKey, cancellationToken);
        head = newer is not null && newer.HighVersion >= head.HighVersion ? newer : head with { Key = newKey };
        await storage.WriteAsync(storage.HeadPath(head.Key, true), head, cancellationToken);
        boundary?.Invoke(FileHistoryCommitStage.LegacyHeadBound);
        await storage.DeleteAsync(storage.HeadPath(oldKey, true), cancellationToken);
        await storage.DeleteAsync(storage.HeadPath(oldKey, false), cancellationToken);
        return head;
    }

    private async Task<FileHistoryHead> RecoverAsync(FileHistoryHead head, CancellationToken cancellationToken) {
        if (head.Prepared is not { } prepared) {
            return head;
        }
        var actualHash = await storage.ReadSourceHashAsync(prepared.SourcePath, cancellationToken);
        head = head with {
            Committed = actualHash == prepared.TargetHash ? prepared : head.Committed,
            Prepared = null
        };
        await storage.WriteAsync(storage.HeadPath(head.Key, true), head, cancellationToken);
        return head;
    }

    private async Task ArchiveAsync(FileHistoryHead head, CancellationToken cancellationToken) {
        if (head.Prepared is not null || head.Committed is { } committed && committed.Version > head.AcknowledgedVersion) {
            await storage.WriteAsync(storage.HeadPath(head.Key, true), head, cancellationToken);
            return;
        }
        if (head.Committed is { Deleted: true } deleted) {
            head = head with { Committed = deleted with { Fact = deleted.Fact with { Aggregate = null, Attempts = [] } } };
        }
        await storage.WriteAsync(storage.HeadPath(head.Key, false), head, cancellationToken);
        boundary?.Invoke(FileHistoryCommitStage.AcknowledgmentPersisted);
        await storage.DeleteAsync(storage.HeadPath(head.Key, true), cancellationToken);
    }
}
