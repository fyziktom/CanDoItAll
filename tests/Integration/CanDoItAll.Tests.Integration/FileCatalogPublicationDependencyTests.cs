using System.Diagnostics;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Tests.Integration.Runtime;

namespace CanDoItAll.Tests.Integration;

[Trait("Category", "HostPlatform")]
[Trait("Category", "FileSystemPortability")]
public sealed class FileCatalogPublicationDependencyTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Run_checkpoint_during_catalog_wait_preserves_the_declared_dependency(bool expectedWorkspaceRevision) {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var files = await AgentToolAdmissionJournalFixture.CreateAsync();
        var token = deadline.Token;
        var before = await files.Store.LoadSnapshotAsync(token);
        var callbacks = 0;
        var error = await DuringCatalogWaitAsync(files, expectedWorkspaceRevision ? before.Revision : null,
            () => callbacks++, async () => {
                await CheckpointAsync(files, token);
                Assert.Equal(before.Revision + 1, (await files.NewStore().LoadSnapshotAsync(token)).Revision);
            }, token);
        Assert.Equal(1, callbacks);
        var saved = await files.NewStore().LoadSnapshotAsync(token);
        var run = Assert.Single(saved.Document.ExecutionRuns, item => item.Id == files.Detail.Run.Id);
        Assert.Equal("Concurrent checkpoint", run.ResultSummary);
        Assert.Equal(files.Detail.Run.Revision + 1, run.Revision);
        var agent = Assert.Single(saved.Document.Agents, item => item.Id == files.Agent.Id);
        if (expectedWorkspaceRevision) {
            var conflict = Assert.IsType<SandboxWorkspaceConcurrencyException>(error);
            Assert.Equal(before.Revision, conflict.ExpectedRevision);
            Assert.Equal(before.Revision + 1, conflict.ActualRevision);
            Assert.Equal(before.Revision + 1, saved.Revision);
            Assert.Equal(files.Agent.Summary, agent.Summary);
            Assert.Equal(before.Document.CatalogDataRevision, saved.Document.CatalogDataRevision);
        } else {
            Assert.Null(error);
            Assert.Equal(before.Revision + 2, saved.Revision);
            Assert.Equal("Prepared catalog change", agent.Summary);
            Assert.Equal(before.Document.CatalogDataRevision.Next(), saved.Document.CatalogDataRevision);
        }
    }

    [Fact]
    public async Task Intervening_catalog_edit_refuses_without_rebasing_or_replaying_the_callback() {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var files = await AgentToolAdmissionJournalFixture.CreateAsync();
        var token = deadline.Token;
        var before = await files.Store.LoadCatalogAsync(token);
        var other = before.Agents.First(agent => agent.Id != files.Agent.Id);
        var callbacks = 0;
        var error = await DuringCatalogWaitAsync(files, null, () => callbacks++, async () => {
            await files.NewStore().UpdateCatalogAsync(current => current with {
                Agents = current.Agents.Select(agent => agent.Id == other.Id
                    ? agent with { Summary = "Independent catalog change" } : agent).ToArray()
            }, token);
        }, token);
        Assert.Equal(1, callbacks);
        Assert.Equal(SandboxWorkspacePublicationConflictKind.OriginalBytesChanged,
            Assert.IsType<SandboxWorkspacePublicationConflictException>(error).Kind);
        var saved = await files.NewStore().LoadCatalogAsync(token);
        Assert.Equal(before.CatalogDataRevision.Next(), saved.CatalogDataRevision);
        Assert.Equal(files.Agent.Summary, saved.Agents.Single(agent => agent.Id == files.Agent.Id).Summary);
        Assert.Equal("Independent catalog change", saved.Agents.Single(agent => agent.Id == other.Id).Summary);
    }

    [Fact]
    public async Task Shared_pending_publication_still_refuses_the_catalog_only_write() {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var files = await AgentToolAdmissionJournalFixture.CreateAsync();
        var token = deadline.Token;
        var layout = new FileSandboxWorkspaceStorageLayout(files.WorkspaceRoot, files.StorageScope);
        var before = await File.ReadAllBytesAsync(layout.CatalogPath, token);
        var pending = Path.Combine(layout.DataRoot, $".{Path.GetFileName(layout.WorkspaceIndexPath)}.{Guid.NewGuid():N}.pending");
        var evidence = "Incomplete unrelated index publication"u8.ToArray();
        var created = false;
        try {
            var error = await DuringCatalogWaitAsync(files, null, () => { }, async () => {
                await using var stream = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                created = true;
                await stream.WriteAsync(evidence, token);
            }, token);
            Assert.Equal(SandboxWorkspacePublicationConflictKind.PendingRecoveryChanged,
                Assert.IsType<SandboxWorkspacePublicationConflictException>(error).Kind);
            Assert.Equal(before, await File.ReadAllBytesAsync(layout.CatalogPath, token));
            Assert.Equal(evidence, await File.ReadAllBytesAsync(pending, token));
        } finally {
            if (created) {
                File.Delete(pending);
            }
        }
    }

    private static async Task<Exception?> DuringCatalogWaitAsync(AgentToolAdmissionJournalFixture files,
        long? expectedRevision, Action callback, Func<Task> duringWait, CancellationToken token) {
        var layout = new FileSandboxWorkspaceStorageLayout(files.WorkspaceRoot, files.StorageScope);
        var before = await files.Store.LoadCatalogAsync(token);
        var target = before with {
            Agents = before.Agents.Select(agent => agent.Id == files.Agent.Id
                ? agent with { Summary = "Prepared catalog change" } : agent).ToArray()
        };
        var path = Assert.Single(FileSandboxWorkspaceWriteSet.Catalog(before, target).LockPaths(layout));
        var writer = new DurableFileWriter(new PhysicalFileSystemPathPolicyFactory());
        var blocker = await writer.AcquireCoordinationAsync(layout.RootPath, path, TimeSpan.FromSeconds(5), false, token);
        Task<Exception?>? pending = null;
        try {
            using var wait = new PublicationWait();
            var store = files.NewStore();
            pending = Record.ExceptionAsync(async () => {
                if (expectedRevision is { } revision) {
                    await store.UpdateCatalogAsync(Update, revision, token);
                } else {
                    await store.UpdateCatalogAsync(Update, token);
                }
            });
            await wait.Entered.Task.WaitAsync(token);
            Assert.False(pending.IsCompleted);
            await duringWait();
        } finally {
            await blocker.DisposeAsync();
            if (pending is not null) {
                await pending;
            }
        }
        return await pending!;

        SandboxWorkspaceCatalog Update(SandboxWorkspaceCatalog current) {
            callback();
            Assert.Equal(before.CatalogDataRevision, current.CatalogDataRevision);
            return target;
        }
    }

    private static Task<ExecutionRunDetail> CheckpointAsync(AgentToolAdmissionJournalFixture files, CancellationToken token)
        => ((ISandboxWorkspaceExecutionRunMutationStore)files.NewStore()).UpdateExecutionRunDetailAsync(files.Detail.Run.Id,
            current => current with { Run = current.Run with {
                Revision = checked(current.Run.Revision + 1), ResultSummary = "Concurrent checkpoint"
            } }, token);

    private sealed class PublicationWait : IDisposable {
        private readonly Activity parent = new Activity("catalog-dependency-test").Start();
        private readonly ActivityListener listener;
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal PublicationWait() {
            listener = new ActivityListener {
                ShouldListenTo = source => source.Name == AgentFrameworkTelemetry.SourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
                ActivityStarted = activity => {
                    if (activity.TraceId == parent.TraceId && activity.OperationName == "workspace.publication.keys.wait") {
                        Entered.TrySetResult();
                    }
                }
            };
            ActivitySource.AddActivityListener(listener);
        }

        public void Dispose() {
            listener.Dispose();
            parent.Dispose();
        }
    }
}
