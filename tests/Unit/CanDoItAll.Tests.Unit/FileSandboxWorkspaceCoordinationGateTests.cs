using CanDoItAll.AgentFramework.Persistence;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class FileSandboxWorkspaceCoordinationGateTests {
    [Fact]
    public async Task Normalized_workspace_instances_share_exclusion_and_ordinary_arrival_order() {
        await using var fixture = new Requests();
        var held = await fixture.Take();
        var samePath = new FileSandboxWorkspaceCoordinationGate(Path.Combine(Path.GetDirectoryName(fixture.Key)!, ".", Path.GetFileName(fixture.Key)));
        var first = fixture.Take(gate: samePath);
        var second = fixture.Take();
        Assert.False(first.IsCompleted);
        Assert.False(second.IsCompleted);
        held.Dispose();
        var firstLease = await first;
        Assert.False(second.IsCompleted);
        firstLease.Dispose();
        Assert.NotNull(await second);
    }

    [Fact]
    public async Task Different_workspaces_can_hold_admission_at_the_same_time() {
        await using var first = new Requests();
        await using var second = new Requests();
        Assert.NotNull(await first.Take());
        Assert.NotNull(await second.Take());
    }

    [Fact]
    public async Task Control_arrival_order_is_preserved_and_a_waiting_writer_follows_at_most_eight_controls() {
        await using var fixture = new Requests();
        var held = await fixture.Take();
        var writer = fixture.Take();
        var controls = Enumerable.Range(0, 10).Select(_ => fixture.Take(FileSandboxWorkspaceAccessPriority.Control)).ToArray();
        held.Dispose();
        for (var index = 0; index < 8; index++) {
            var current = await controls[index];
            Assert.False(writer.IsCompleted);
            Assert.All(controls.Skip(index + 1), next => Assert.False(next.IsCompleted));
            current.Dispose();
        }
        var ordinary = await writer;
        Assert.False(controls[8].IsCompleted);
        ordinary.Dispose();
        (await controls[8]).Dispose();
        Assert.NotNull(await controls[9]);
    }

    [Fact]
    public Task Cancelling_an_ordinary_waiter_preserves_the_holder_and_next_request()
        => CheckCancelledWaiterAsync(FileSandboxWorkspaceAccessPriority.Ordinary);

    [Fact]
    public Task Cancelling_a_control_waiter_preserves_the_holder_and_next_request()
        => CheckCancelledWaiterAsync(FileSandboxWorkspaceAccessPriority.Control);

    private static async Task CheckCancelledWaiterAsync(FileSandboxWorkspaceAccessPriority priority) {
        await using var fixture = new Requests();
        var held = await fixture.Take();
        var cancelled = fixture.Take(priority);
        fixture.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
        await using var successor = new Requests(fixture.Key);
        var next = successor.Take();
        Assert.False(next.IsCompleted);
        held.Dispose();
        Assert.NotNull(await next);
    }

    [Fact]
    public async Task Already_cancelled_admission_creates_no_holder() {
        await using var fixture = new Requests();
        fixture.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await fixture.Take());
        await using var successor = new Requests(fixture.Key);
        Assert.NotNull(await successor.Take());
    }

    [Fact]
    public async Task Grant_cancellation_races_keep_every_returned_lease_accounted_for() {
        for (var iteration = 0; iteration < 64; iteration++) {
            await using var fixture = new Requests();
            var held = await fixture.Take();
            var waiting = fixture.Take(FileSandboxWorkspaceAccessPriority.Control);
            await Task.WhenAll(Task.Run(held.Dispose), Task.Run(fixture.Cancel));
            try {
                (await waiting).Dispose();
            } catch (OperationCanceledException) {
                Assert.True(fixture.IsCancelled);
            }
            await using var successor = new Requests(fixture.Key);
            Assert.NotNull(await successor.Take());
        }
    }

    [Fact]
    public async Task Disposing_an_old_lease_twice_cannot_release_its_successor() {
        await using var fixture = new Requests();
        var first = await fixture.Take();
        var second = fixture.Take();
        first.Dispose();
        var held = await second;
        var third = fixture.Take();
        first.Dispose();
        Assert.False(third.IsCompleted);
        held.Dispose();
        Assert.NotNull(await third);
    }

    [Fact]
    public async Task An_unknown_priority_is_rejected_without_occupying_the_workspace() {
        await using var fixture = new Requests();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await fixture.Take((FileSandboxWorkspaceAccessPriority)(-1)));
        Assert.NotNull(await fixture.Take());
    }

    private sealed class Requests(string? key = null) : IAsyncDisposable {
        private readonly CancellationTokenSource deadline = new(TimeSpan.FromSeconds(5));
        private readonly List<Task<IDisposable>> pending = [];
        internal string Key { get; } = key ?? Path.GetFullPath($"coordination-{Guid.NewGuid():N}.lock");
        internal bool IsCancelled => deadline.IsCancellationRequested;

        internal Task<IDisposable> Take(FileSandboxWorkspaceAccessPriority priority = FileSandboxWorkspaceAccessPriority.Ordinary,
            FileSandboxWorkspaceCoordinationGate? gate = null) {
            var task = (gate ?? new(Key)).AcquireAsync(priority, deadline.Token).AsTask();
            pending.Add(task);
            return task;
        }

        internal void Cancel() => deadline.Cancel();

        public async ValueTask DisposeAsync() {
            deadline.Cancel();
            try {
                foreach (var task in pending) {
                    try {
                        (await task).Dispose();
                    } catch (OperationCanceledException) when (deadline.IsCancellationRequested) {
                    }
                }
            } finally {
                deadline.Dispose();
            }
        }
    }
}
