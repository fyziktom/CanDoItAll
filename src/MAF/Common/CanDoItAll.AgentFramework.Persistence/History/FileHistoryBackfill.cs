using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.ProviderHistory;

namespace CanDoItAll.AgentFramework.Persistence;

public sealed record FileHistoryBackfillProgress(bool DiscoveryComplete, bool AllSourceIntentsStaged,
    int DiscoverySteps, int VisitedFiles, int StagedRecords);

public sealed class FileHistoryBackfill : IDisposable {
    private static readonly TimeSpan WorkBudget = TimeSpan.FromMilliseconds(250);
    private readonly TimeProvider clock;
    private readonly FileSandboxWorkspaceStorageLayout layout;
    private readonly FileHistoryJournalStorage storage;
    private readonly FileProviderHistoryJournal journal;
    private readonly HistoryPartition partition;
    private readonly string backfillRoot;
    private IEnumerator<string?>? discovery;
    private FileHistoryManifestChunk? pendingChunk;
    private bool discoveryEnded;

    public FileHistoryBackfill(string workspaceRoot, WorkspaceScopeDescriptor? scope, HistoryPartition partition,
        TimeProvider? clock = null) {
        this.clock = clock ?? TimeProvider.System;
        layout = new(workspaceRoot, scope);
        storage = new(layout);
        journal = new(workspaceRoot, scope);
        this.partition = partition;
        backfillRoot = Path.Combine(storage.BackfillRoot, partition.StorageLineageId.ToString("N"));
    }

    public async Task<FileHistoryBackfillProgress> ProcessAsync(int maximumFiles,
        CancellationToken cancellationToken = default) {
        if (maximumFiles is < 1 or > 1000) {
            throw new ArgumentOutOfRangeException(nameof(maximumFiles));
        }
        await using var lease = await storage.BackfillLock.AcquireAsync(cancellationToken);
        var started = clock.GetTimestamp();
        var completionPath = Path.Combine(backfillRoot, "discovery-complete.json");
        var complete = await storage.ReadAsync<bool?>(completionPath, cancellationToken) == true;
        var steps = 0;
        if (!complete) {
            try {
                steps = await DiscoverBatchAsync(maximumFiles, cancellationToken);
            } catch {
                discovery?.Dispose();
                discovery = null;
                discoveryEnded = false;
                throw;
            }
            complete = discoveryEnded && pendingChunk is null;
            if (complete) {
                await storage.WriteAsync(completionPath, true, cancellationToken);
            }
        }
        var chunkPath = PendingChunkPath();
        if (chunkPath is null) {
            return new(complete, complete, steps, 0, 0);
        }
        var chunk = await storage.ReadAsync<FileHistoryManifestChunk>(chunkPath, cancellationToken)
            ?? throw new InvalidDataException("History manifest chunk disappeared during its locked read.");
        if (chunk.Paths.Length > 1000 || chunk.Offset < 0 || chunk.Offset > chunk.Paths.Length) {
            throw new InvalidDataException("History manifest chunk violates its bounded cursor contract.");
        }
        var visited = 0;
        var staged = 0;
        var offset = chunk.Offset;
        while (offset < chunk.Paths.Length && visited < maximumFiles) {
            cancellationToken.ThrowIfCancellationRequested();
            if (await journal.StageExistingAsync(chunk.Paths[offset], partition, cancellationToken)) {
                staged++;
            }
            offset++;
            visited++;
            if (clock.GetElapsedTime(started) >= WorkBudget) {
                break;
            }
        }
        if (offset == chunk.Paths.Length) {
            await storage.DeleteAsync(chunkPath, cancellationToken);
        } else {
            await storage.WriteAsync(chunkPath, chunk with { Offset = offset }, cancellationToken);
        }
        return new(complete, complete && PendingChunkPath() is null, steps, visited, staged);
    }

    private async Task<int> DiscoverBatchAsync(int maximumSteps, CancellationToken cancellationToken) {
        var steps = 0;
        if (pendingChunk is null && !discoveryEnded) {
            discovery ??= Discover().GetEnumerator();
            var paths = new List<string>(maximumSteps);
            while (steps < maximumSteps) {
                cancellationToken.ThrowIfCancellationRequested();
                if (!discovery.MoveNext()) {
                    discoveryEnded = true;
                    discovery.Dispose();
                    discovery = null;
                    break;
                }
                if (discovery.Current is { } path) {
                    paths.Add(path);
                }
                steps++;
            }
            if (paths.Count > 0) {
                pendingChunk = new(Guid.NewGuid(), paths.ToArray(), 0);
            }
        }
        if (pendingChunk is { } chunk) {
            await storage.WriteAsync(Path.Combine(backfillRoot, "chunks", chunk.Id.ToString("N") + ".json"),
                chunk, cancellationToken);
            pendingChunk = null;
        }
        return steps;
    }

    private string? PendingChunkPath() {
        var directory = Path.Combine(backfillRoot, "chunks");
        storage.Relative(directory);
        return Directory.Exists(directory) ? Directory.EnumerateFiles(directory, "*.json").FirstOrDefault() : null;
    }

    private IEnumerable<string?> Discover() {
        storage.Relative(layout.ExecutionRunsRoot);
        if (Directory.Exists(layout.ExecutionRunsRoot)) {
            foreach (var directory in Directory.EnumerateDirectories(layout.ExecutionRunsRoot)) {
                yield return null;
                storage.Relative(directory);
                if (!Guid.TryParseExact(Path.GetFileName(directory), "N", out var runId)) {
                    continue;
                }
                foreach (var path in UsageFiles(layout.RunUsageRoot(runId))) {
                    yield return path;
                }
            }
        }
        foreach (var path in UsageFiles(layout.OrphanUsageRoot)) {
            yield return path;
        }
    }

    private IEnumerable<string> UsageFiles(string directory) {
        storage.Relative(directory);
        if (Directory.Exists(directory)) {
            foreach (var path in Directory.EnumerateFiles(directory, "*.json")) {
                if (journal.IsUsagePath(path)) {
                    yield return storage.Relative(path);
                }
            }
        }
    }

    public void Dispose() => discovery?.Dispose();
}

internal sealed record FileHistoryManifestChunk(Guid Id, string[] Paths, int Offset);
