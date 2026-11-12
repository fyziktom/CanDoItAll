using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Infrastructure;

namespace CanDoItAll.Tests.Integration;

internal static class ProcessExecutionHistoryFixture {
    internal static async Task SeedBeforeActorsStartAsync(FileSandboxWorkspaceStorageLayout layout,
        IReadOnlyList<ExecutionRunRecord> runs, CancellationToken cancellationToken) {
        Assert.All(runs, run => {
            Assert.Null(run.ChatSessionId);
            Assert.Null(run.ToolAdmission);
            Assert.Empty(run.PendingApprovals);
        });
        var json = new FileSandboxWorkspaceJsonStore(null, new PhysicalFileSystemPathPolicyFactory(),
            durableFileWriter: null, managedRoot: layout.RootPath);
        var slices = new FileSandboxWorkspaceExecutionSliceStore(layout, json);
        var before = await slices.LoadAsync(cancellationToken);
        var originals = new Dictionary<Guid, byte[]>();
        foreach (var run in before.ExecutionRuns) {
            originals.Add(run.Id, await File.ReadAllBytesAsync(layout.RunPath(run.Id), cancellationToken));
        }
        Assert.Equal(before.ExecutionRuns.Count + runs.Count,
            before.ExecutionRuns.Concat(runs).Select(run => run.Id).Distinct().Count());
        var index = await json.ReadJsonAsync<WorkspaceStorageIndex>(layout.WorkspaceIndexPath, cancellationToken)
            ?? throw new InvalidOperationException("History preparation requires an initialized isolated workspace.");
        Assert.True(await slices.SaveAsync(before, before with {
            ExecutionRuns = [.. before.ExecutionRuns, .. runs]
        }, cancellationToken));
        await json.WriteJsonAtomicallyAsync(layout.WorkspaceIndexPath,
            index with { Revision = checked(index.Revision + 1), UpdatedAtUtc = DateTimeOffset.UtcNow }, cancellationToken);
        var saved = await slices.LoadIndexAsync(cancellationToken);
        Assert.Equal(before.ExecutionRuns.Count + runs.Count, saved.RunCount);
        foreach (var (id, bytes) in originals) {
            Assert.Equal(bytes, await File.ReadAllBytesAsync(layout.RunPath(id), cancellationToken));
        }
    }
}
