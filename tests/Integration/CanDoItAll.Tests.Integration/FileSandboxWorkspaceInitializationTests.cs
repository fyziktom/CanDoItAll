using System.Diagnostics;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Integration;

[Trait("Category", "HostPlatform")]
[Trait("Category", "FileSystemPortability")]
public sealed class FileSandboxWorkspaceInitializationTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Concurrent_first_reads_observe_one_committed_initialization(bool independentStore) {
        await using var environment = CanDoItAllTestEnvironment.Create("workspace-initialization");
        var layout = new FileSandboxWorkspaceStorageLayout(environment.CreateInMemoryProfile("primary").WorkspaceRootPath);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var observation = new InitializationObservation(2);
        var store = new FileSandboxWorkspaceStore(layout.RootPath);
        var other = independentStore ? new FileSandboxWorkspaceStore(layout.RootPath) : store;
        IAsyncDisposable? blocker = await BlockInitializationAsync(layout, deadline.Token);
        Task? pending = null;
        try {
            var catalog = store.LoadCatalogAsync(deadline.Token);
            var summary = other.LoadExecutionSummaryAsync(deadline.Token);
            pending = Task.WhenAll(catalog, summary);
            await observation.Prepared.Task.WaitAsync(deadline.Token);
            Assert.False(pending.IsCompleted);
            Assert.False(File.Exists(layout.CatalogPath));
            await blocker.DisposeAsync();
            blocker = null;
            await pending;

            var snapshot = await store.LoadSnapshotAsync(deadline.Token);
            Assert.Equal(1, snapshot.Revision);
            Assert.Equal(CatalogDataRevision.Initial, snapshot.Document.CatalogDataRevision);
            Assert.NotEmpty((await catalog).Agents);
            Assert.Equal(snapshot.Document.Agents.Select(agent => agent.Id), (await catalog).Agents.Select(agent => agent.Id));
            Assert.Equal(await summary, await other.LoadExecutionSummaryAsync(deadline.Token));
            Assert.Equal(1, observation.ExistingInitializations);
        } finally {
            if (blocker is not null) {
                await blocker.DisposeAsync();
            }
            await deadline.CancelAsync();
            if (pending is not null) {
                await Record.ExceptionAsync(() => pending);
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Incomplete_or_pending_publication_is_refused_without_reseeding(bool pendingPublication) {
        await using var environment = CanDoItAllTestEnvironment.Create("workspace-initialization");
        var layout = new FileSandboxWorkspaceStorageLayout(environment.CreateInMemoryProfile("primary").WorkspaceRootPath);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var observation = new InitializationObservation(1);
        var store = new FileSandboxWorkspaceStore(layout.RootPath);
        IAsyncDisposable? blocker = await BlockInitializationAsync(layout, deadline.Token);
        Task<Exception?>? pending = null;
        var retained = pendingPublication
            ? Path.Combine(layout.DataRoot, $".{Path.GetFileName(layout.CatalogPath)}.{Guid.NewGuid():N}.pending")
            : layout.CatalogPath;
        const string evidence = "Unconfirmed original publication";
        try {
            pending = Record.ExceptionAsync(() => store.LoadCatalogAsync(deadline.Token));
            await observation.Prepared.Task.WaitAsync(deadline.Token);
            Assert.False(pending.IsCompleted);
            await File.WriteAllTextAsync(retained, evidence, deadline.Token);
            await blocker.DisposeAsync();
            blocker = null;

            var conflict = Assert.IsType<SandboxWorkspacePublicationConflictException>(await pending);
            Assert.Equal(pendingPublication ? SandboxWorkspacePublicationConflictKind.PendingRecoveryChanged
                : SandboxWorkspacePublicationConflictKind.OriginalBytesChanged, conflict.Kind);
            Assert.Equal(evidence, await File.ReadAllTextAsync(retained, deadline.Token));
            Assert.False(File.Exists(layout.WorkspaceIndexPath));
            Assert.False(File.Exists(layout.ExecutionIndexPath));
            Assert.Equal(0, observation.ExistingInitializations);
        } finally {
            if (blocker is not null) {
                await blocker.DisposeAsync();
            }
            await deadline.CancelAsync();
            if (pending is not null) {
                await pending;
            }
        }
    }

    private static ValueTask<IAsyncDisposable> BlockInitializationAsync(FileSandboxWorkspaceStorageLayout layout, CancellationToken token) {
        var seeded = SandboxWorkspaceSeedFactory.Normalize(SandboxWorkspaceSeedFactory.Create());
        var key = FileSandboxWorkspaceWriteSet.Workspace(SandboxWorkspaceDocument.Empty, seeded).LockPaths(layout)[0];
        var writer = new DurableFileWriter(new PhysicalFileSystemPathPolicyFactory());
        return writer.AcquireCoordinationAsync(layout.RootPath, key, TimeSpan.FromSeconds(5), false, token);
    }

    private sealed class InitializationObservation : IDisposable {
        private readonly Activity parent = new Activity("workspace-initialization-test").Start();
        private readonly ActivityListener listener;
        private int prepared;
        private int existing;
        internal TaskCompletionSource Prepared { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int ExistingInitializations => Volatile.Read(ref existing);

        internal InitializationObservation(int expectedPreparations) {
            listener = new ActivityListener {
                ShouldListenTo = source => source.Name == AgentFrameworkTelemetry.SourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
                ActivityStarted = activity => {
                    if (activity.TraceId != parent.TraceId) {
                        return;
                    }
                    if (activity.OperationName == "workspace.publication.keys.wait" && Interlocked.Increment(ref prepared) == expectedPreparations) {
                        Prepared.TrySetResult();
                    }
                    if (activity.OperationName == "workspace.initialization.existing") {
                        Interlocked.Increment(ref existing);
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
