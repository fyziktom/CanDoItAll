using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;

namespace CanDoItAll.AgentFramework.Persistence;

public sealed record FileUsageIndexProgress(int Processed, int Total, bool Complete);

public sealed class FileProviderUsageIndexMaintenance {
    private readonly FileSandboxWorkspaceStorageLayout layout;
    private readonly FileSandboxWorkspaceJsonStore storage;
    private readonly FileProviderUsageIndex index;
    private readonly FileSandboxWorkspaceCrossProcessLock workspaceLock;

    public FileProviderUsageIndexMaintenance(string workspaceRoot, WorkspaceScopeDescriptor? scope = null) {
        layout = new(workspaceRoot, scope);
        var paths = new PhysicalFileSystemPathPolicyFactory();
        var writer = new DurableFileWriter(paths);
        storage = new(null, paths, writer, layout.RootPath);
        index = new(layout, storage);
        workspaceLock = new(layout.RootPath, layout.WorkspaceLockPath, writer);
    }

    public async Task<FileUsageIndexProgress> ProcessAsync(int maximumRecords = 100, bool rebuild = false,
        CancellationToken cancellationToken = default) {
        if (maximumRecords is < 1 or > 1000) {
            throw new ArgumentOutOfRangeException(nameof(maximumRecords));
        }
        await using var lease = await workspaceLock.AcquireAsync(cancellationToken);
        FileUsageIndexHeader? header = null;
        if (!rebuild) {
            header = await index.ReadHeaderAsync(cancellationToken);
        }
        var cursor = rebuild ? null : await storage.ReadJsonAsync<FileUsageIndexCursor>(index.CursorPath, cancellationToken);
        if (!rebuild && header is { Complete: true, Version: FileProviderUsageIndex.Version } && cursor is null) {
            return new(0, 0, true);
        }
        if (cursor is null || header is null || cursor.Generation != header.Generation || header.Version != FileProviderUsageIndex.Version) {
            header = FileUsageIndexHeader.New();
            await index.WriteHeaderAsync(header, cancellationToken);
            var locators = Discover(cancellationToken).ToArray();
            cursor = new(header.Generation, locators, 0);
            await storage.WriteJsonAtomicallyAsync(index.CursorPath, cursor, cancellationToken);
        }
        if (cursor.Offset < 0 || cursor.Offset > cursor.Locators.Length) {
            throw new InvalidDataException("Invalid usage index maintenance cursor. Restart with rebuild enabled.");
        }
        var end = Math.Min(cursor.Locators.Length, cursor.Offset + maximumRecords);
        for (var offset = cursor.Offset; offset < end; offset++) {
            cancellationToken.ThrowIfCancellationRequested();
            var path = index.Absolute(cursor.Locators[offset]);
            var observation = await storage.ReadJsonAsync<ProviderUsageObservation>(path, cancellationToken);
            if (observation is not null) {
                await index.IncludeAsync(path, observation, cancellationToken);
            }
            cursor = cursor with { Offset = offset + 1 };
            await storage.WriteJsonAtomicallyAsync(index.CursorPath, cursor, cancellationToken);
        }
        var complete = cursor.Offset == cursor.Locators.Length;
        if (complete) {
            header = await index.ReadHeaderAsync(cancellationToken)
                ?? throw new InvalidDataException("Usage index disappeared during maintenance.");
            await index.WriteHeaderAsync(header with { Complete = true, CoverageVerifiedAtUtc = DateTimeOffset.UtcNow }, cancellationToken);
            await storage.DeleteFileAsync(index.CursorPath, cancellationToken);
        }
        return new(cursor.Offset, cursor.Locators.Length, complete);
    }

    private IEnumerable<string> Discover(CancellationToken token) {
        if (!layout.ExecutionStorageExists() && (File.Exists(layout.CatalogPath) || File.Exists(layout.LegacyExecutionPath))) {
            throw new InvalidOperationException("Migrate legacy execution storage with FileSandboxWorkspaceStore.LoadExecutionAsync before indexing.");
        }
        var options = new EnumerationOptions { AttributesToSkip = FileAttributes.ReparsePoint };
        if (Directory.Exists(layout.ExecutionRunsRoot)) {
            foreach (var directory in Directory.EnumerateDirectories(layout.ExecutionRunsRoot, "*", options)) {
                token.ThrowIfCancellationRequested();
                if (Guid.TryParseExact(Path.GetFileName(directory), "N", out var run)) {
                    foreach (var locator in UsageFiles(layout.RunUsageRoot(run), options, token)) {
                        yield return locator;
                    }
                }
            }
        }
        foreach (var locator in UsageFiles(layout.OrphanUsageRoot, options, token)) {
            yield return locator;
        }
    }

    private IEnumerable<string> UsageFiles(string directory, EnumerationOptions options, CancellationToken token) {
        if (!Directory.Exists(directory)) {
            yield break;
        }
        foreach (var path in Directory.EnumerateFiles(directory, "*.json", options)) {
            token.ThrowIfCancellationRequested();
            yield return index.Locator(path);
        }
    }
}

internal sealed record FileUsageIndexCursor(Guid Generation, string[] Locators, int Offset);
