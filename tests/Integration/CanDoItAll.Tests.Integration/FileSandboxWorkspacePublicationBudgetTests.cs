using System.Runtime.ExceptionServices;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Integration.AgentFramework;

[Trait("Category", "HostPlatform")]
[Trait("Category", "FileSystemPortability")]
public sealed class FileSandboxWorkspacePublicationBudgetTests {
    [Fact]
    public async Task Expired_shared_budget_releases_acquired_keys_and_stops_before_the_next_key() {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var environment = CanDoItAllTestEnvironment.Create("publication-key-budget");
        var failures = new List<Exception>();
        IAsyncDisposable? blocker = null;
        IAsyncDisposable? publication = null;
        FileSandboxWorkspacePublicationScope? scope = null;
        Task<IAsyncDisposable>? pending = null;
        var outcomeObserved = false;
        try {
            var layout = new FileSandboxWorkspaceStorageLayout(environment.RootPath);
            var gate = new FileSandboxWorkspaceCoordinationGate(layout.WorkspaceLockPath);
            var policies = new PhysicalFileSystemPathPolicyFactory();
            var writer = new DurableFileWriter(policies);
            var now = DateTimeOffset.UtcNow;
            var run = new ExecutionRunRecord(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Budget fixture",
                "budget-fixture", "", "", "", "", "", "{}", "", "", "", "", ExecutionState.Completed,
                null, now, now, null, now, "", null, []);
            var writeSet = FileSandboxWorkspaceWriteSet.Run(run, run, readsAgent: true);
            var paths = writeSet.LockPaths(layout);
            Assert.Equal(3, paths.Length);
            var payload = layout.RunPath(run.Id);
            var original = "unchanged publication input"u8.ToArray();
            await writer.WriteBytesAsync(layout.RootPath, payload, original, cancellationToken: deadline.Token);
            blocker = await writer.AcquireCoordinationAsync(layout.RootPath, paths[1], TimeSpan.FromSeconds(5),
                false, deadline.Token);
            var clock = new BudgetClock();
            scope = await FileSandboxWorkspacePublicationScope.EnterAsync(gate,
                new(layout.RootPath, layout.WorkspaceLockPath, writer), layout, writer, deadline.Token, clock);
            var witness = await FileSandboxWorkspaceMutationWitness.CaptureAsync(layout, [payload], [], [],
                policies, deadline.Token);
            pending = scope.AcquireAsync(writeSet, witness, deadline.Token);
            await clock.SecondKeyBudgetObserved.Task.WaitAsync(deadline.Token);
            Assert.False(pending.IsCompleted);
            Assert.True(File.Exists(paths[0]));
            Assert.Throws<IOException>(() => {
                using var conflicting = new FileStream(paths[0], FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            });
            Assert.False(File.Exists(paths[2]));
            using (var availableLocal = await gate.AcquireAsync(FileSandboxWorkspaceAccessPriority.Ordinary, deadline.Token)) {
                Assert.NotNull(availableLocal);
            }
            await using (var availableWorkspace = await writer.AcquireCoordinationAsync(layout.RootPath,
                    layout.WorkspaceLockPath, TimeSpan.FromSeconds(5), false, deadline.Token)) {
                Assert.NotNull(availableWorkspace);
            }

            clock.Advance(TimeSpan.FromMinutes(1) + TimeSpan.FromTicks(1));
            var released = blocker;
            blocker = null;
            await released.DisposeAsync();
            var failure = await Record.ExceptionAsync(async () => publication = await pending.WaitAsync(deadline.Token));
            outcomeObserved = pending.IsCompleted;
            Assert.IsType<TimeoutException>(failure);
            Assert.Null(publication);
            Assert.True(clock.ExpiredBudgetObserved);
            Assert.False(File.Exists(paths[2]));
            Assert.Equal(original, await File.ReadAllBytesAsync(payload, deadline.Token));
            using (var availableLocal = await gate.AcquireAsync(FileSandboxWorkspaceAccessPriority.Ordinary, deadline.Token)) {
                Assert.NotNull(availableLocal);
            }
            foreach (var path in paths.Take(2).Append(layout.WorkspaceLockPath)) {
                await using var available = await writer.AcquireCoordinationAsync(layout.RootPath, path,
                    TimeSpan.FromSeconds(5), false, deadline.Token);
                Assert.NotNull(available);
            }
        } catch (Exception failure) {
            failures.Add(failure);
        } finally {
            try {
                await deadline.CancelAsync();
            } catch (Exception failure) {
                failures.Add(failure);
            }
            try {
                if (blocker is not null) {
                    var released = blocker;
                    blocker = null;
                    await released.DisposeAsync();
                }
            } catch (Exception failure) {
                failures.Add(failure);
            }
            if (pending is not null) {
                try {
                    publication ??= await pending;
                } catch (OperationCanceledException) when (deadline.IsCancellationRequested) {
                } catch (Exception failure) {
                    if (!outcomeObserved && !failures.Contains(failure)) {
                        failures.Add(failure);
                    }
                }
            }
            foreach (var owned in new IAsyncDisposable?[] { publication, scope, environment }) {
                try {
                    if (owned is not null) {
                        await owned.DisposeAsync();
                    }
                } catch (Exception failure) {
                    failures.Add(failure);
                }
            }
        }
        if (failures.Count == 1) {
            ExceptionDispatchInfo.Capture(failures[0]).Throw();
        }
        if (failures.Count > 1) {
            throw new AggregateException("Publication budget proof and owned cleanup failed.", failures);
        }
    }

    private sealed class BudgetClock : TimeProvider {
        private long timestamp;
        private int observations;
        private int expiredBudgetObserved;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public TaskCompletionSource SecondKeyBudgetObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool ExpiredBudgetObserved => Volatile.Read(ref expiredBudgetObserved) != 0;

        public override long GetTimestamp() {
            var observed = Volatile.Read(ref timestamp);
            if (Interlocked.Increment(ref observations) == 4) {
                SecondKeyBudgetObserved.TrySetResult();
            }
            if (observed > TimeSpan.TicksPerMinute) {
                Volatile.Write(ref expiredBudgetObserved, 1);
            }
            return observed;
        }

        public void Advance(TimeSpan elapsed) => Interlocked.Add(ref timestamp, elapsed.Ticks);
    }
}
