using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class FileSandboxWorkspaceJsonBatchTests {
    [Fact]
    public async Task Batch_bounds_parallel_reads_preserves_order_and_revalidates_every_path() {
        using var fixture = new Files();
        var paths = await fixture.CreateFilesAsync(12);
        fixture.Factory.BlockReads = true;
        var reading = fixture.Store.ReadJsonBatchAsync<Payload>(paths, default);
        try {
            await fixture.Factory.FourReadersEntered.Task.WaitAsync(TimeSpan.FromSeconds(30));
            Assert.Equal(4, fixture.Factory.ActiveReaders);
            Assert.False(reading.IsCompleted);
        } finally {
            fixture.Factory.Release.Set();
        }
        var values = await reading;
        Assert.Equal(Enumerable.Range(0, 12), values.Select(value => value!.Index));
        Assert.Equal(12, fixture.Factory.PathChecks);
        Assert.Equal(2, fixture.Factory.PoliciesCreated);
        Assert.Equal(0, fixture.Factory.ActiveReaders);
        Assert.Null(Assert.Single(await fixture.Store.ReadJsonBatchAsync<Payload>([Path.Combine(fixture.Root, "missing.json")], default)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Batch_drains_started_readers_before_cancellation_or_failure_completes(bool corrupt) {
        using var fixture = new Files();
        var paths = await fixture.CreateFilesAsync(12);
        if (corrupt) {
            await File.WriteAllTextAsync(paths[0], "{invalid");
        }
        fixture.Factory.BlockReads = true;
        using var cancellation = new CancellationTokenSource();
        var reading = fixture.Store.ReadJsonBatchAsync<Payload>(paths, cancellation.Token);
        try {
            await fixture.Factory.FourReadersEntered.Task.WaitAsync(TimeSpan.FromSeconds(30));
            if (!corrupt) {
                cancellation.Cancel();
            }
            Assert.False(reading.IsCompleted);
        } finally {
            fixture.Factory.Release.Set();
        }
        if (corrupt) {
            await Assert.ThrowsAsync<InvalidDataException>(() => reading);
        } else {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reading);
        }
        Assert.Equal(0, fixture.Factory.ActiveReaders);
        Assert.Equal(1, Assert.Single(await fixture.Store.ReadJsonBatchAsync<Payload>([paths[1]], default))!.Index);
    }

    [Fact]
    public async Task Batch_rejects_a_link_introduced_after_its_policy_was_created() {
        using var fixture = new Files();
        using var outside = new Files();
        var paths = await fixture.CreateFilesAsync(1);
        var outsidePaths = await outside.CreateFilesAsync(1);
        fixture.Factory.AfterCreate = () => {
            File.Delete(paths[0]);
            File.CreateSymbolicLink(paths[0], outsidePaths[0]);
        };
        try {
            var exception = await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Store.ReadJsonBatchAsync<Payload>(paths, default));
            Assert.Equal(PhysicalPathValidationErrorCode.LinkTraversal,
                Assert.IsType<PhysicalPathValidationException>(exception.InnerException).ErrorCode);
        } finally {
            File.Delete(paths[0]);
        }
    }

    private sealed record Payload(int Index);

    private sealed class Files : IDisposable {
        public string Root { get; } = TestFileSystem.CreateTemporaryRoot("usage-json-batch");
        public InspectingPolicyFactory Factory { get; } = new();
        public FileSandboxWorkspaceJsonStore Store { get; }
        public Files() => Store = new(null, Factory, null, Root);
        public async Task<string[]> CreateFilesAsync(int count) {
            var paths = Enumerable.Range(0, count).Select(index => Path.Combine(Root, $"{index}.json")).ToArray();
            for (var index = 0; index < count; index++) {
                await File.WriteAllTextAsync(paths[index], $"{{\"index\":{index}}}");
            }
            return paths;
        }
        public void Dispose() {
            Factory.Release.Dispose();
            TestFileSystem.DeleteDirectoryWithRetry(Root);
        }
    }

    private sealed class InspectingPolicyFactory : IPhysicalFileSystemPathPolicyFactory {
        public int PoliciesCreated;
        public int ActiveReaders;
        public int PathChecks;
        public bool BlockReads;
        public Action? AfterCreate;
        public TaskCompletionSource FourReadersEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ManualResetEventSlim Release { get; } = new();
        public IPhysicalFileSystemPathPolicy Create(string root) {
            Interlocked.Increment(ref PoliciesCreated);
            var policy = new InspectingPolicy(new PhysicalFileSystemPathPolicyFactory().Create(root), this);
            AfterCreate?.Invoke();
            return policy;
        }
        private sealed class InspectingPolicy(IPhysicalFileSystemPathPolicy policy, InspectingPolicyFactory owner) : IPhysicalFileSystemPathPolicy {
            public string RootPath => policy.RootPath;
            public PhysicalFileSystemCaseSensitivity CaseSensitivity => policy.CaseSensitivity;
            public StringComparer PathComparer => policy.PathComparer;
            public StringComparison PathComparison => policy.PathComparison;
            public bool IsWithinRoot(string path) => policy.IsWithinRoot(path);
            public string ResolveContainedPath(string path) => policy.ResolveContainedPath(path);
            public void RevalidateMutationTarget(string path) => policy.RevalidateMutationTarget(path);
            public void EnsureSafePath(string path, bool allowMissingLeaf = false) {
                if (Interlocked.Increment(ref owner.ActiveReaders) == 4) {
                    owner.FourReadersEntered.TrySetResult();
                }
                try {
                    Interlocked.Increment(ref owner.PathChecks);
                    if (owner.BlockReads && !owner.Release.Wait(TimeSpan.FromSeconds(30))) {
                        throw new TimeoutException("The test did not release the pending file readers.");
                    }
                    policy.EnsureSafePath(path, allowMissingLeaf);
                } finally {
                    Interlocked.Decrement(ref owner.ActiveReaders);
                }
            }
        }
    }
}
