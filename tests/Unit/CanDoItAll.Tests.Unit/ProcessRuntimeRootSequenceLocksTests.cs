using CanDoItAll.Processes.Persistence;

namespace CanDoItAll.Tests.Unit.Processes;

[CollectionDefinition(nameof(ProcessRootSequenceCollection), DisableParallelization = true)]
public sealed class ProcessRootSequenceCollection { }

[Collection(nameof(ProcessRootSequenceCollection))]
public sealed class ProcessRuntimeRootSequenceLocksTests {
    [Fact]
    public async Task Empty_roots_do_not_retain_an_entry_even_with_a_canceled_token() {
        var baseline = ProcessRuntimeRootSequenceLocks.RetainedRootCount;
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        using var lease = await ProcessRuntimeRootSequenceLocks.AcquireAsync([], canceled.Token);
        Assert.Equal(baseline, ProcessRuntimeRootSequenceLocks.RetainedRootCount);
    }

    [Fact]
    public async Task Duplicate_roots_are_acquired_once_and_retired_after_the_lease() {
        var baseline = ProcessRuntimeRootSequenceLocks.RetainedRootCount;
        var root = Guid.NewGuid();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using (await ProcessRuntimeRootSequenceLocks.AcquireAsync([root, root], deadline.Token)) {
            Assert.Equal(baseline + 1, ProcessRuntimeRootSequenceLocks.RetainedRootCount);
        }
        Assert.Equal(baseline, ProcessRuntimeRootSequenceLocks.RetainedRootCount);
    }

    [Fact]
    public async Task Pre_canceled_acquisition_drops_its_pending_reference() {
        var baseline = ProcessRuntimeRootSequenceLocks.RetainedRootCount;
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ProcessRuntimeRootSequenceLocks.AcquireAsync([Guid.NewGuid()], canceled.Token));
        Assert.Equal(baseline, ProcessRuntimeRootSequenceLocks.RetainedRootCount);
    }

    [Fact]
    public async Task Another_root_progresses_while_a_same_root_waiter_is_blocked() {
        var baseline = ProcessRuntimeRootSequenceLocks.RetainedRootCount;
        var root = Guid.NewGuid();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using var holder = await ProcessRuntimeRootSequenceLocks.AcquireAsync([root], deadline.Token);
        var pending = ProcessRuntimeRootSequenceLocks.AcquireAsync([root], deadline.Token);
        Assert.False(pending.IsCompleted);
        using (await ProcessRuntimeRootSequenceLocks.AcquireAsync([Guid.NewGuid()], deadline.Token)) {
            Assert.False(pending.IsCompleted);
            Assert.Equal(baseline + 2, ProcessRuntimeRootSequenceLocks.RetainedRootCount);
        }
        holder.Dispose();
        using (await pending.WaitAsync(deadline.Token)) {
            Assert.Equal(baseline + 1, ProcessRuntimeRootSequenceLocks.RetainedRootCount);
        }
        Assert.Equal(baseline, ProcessRuntimeRootSequenceLocks.RetainedRootCount);
    }

    [Fact]
    public async Task Canceling_a_later_root_releases_the_first_root_and_pending_reference() {
        var baseline = ProcessRuntimeRootSequenceLocks.RetainedRootCount;
        var roots = new[] { Guid.NewGuid(), Guid.NewGuid() }.Order().ToArray();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
        using var later = await ProcessRuntimeRootSequenceLocks.AcquireAsync([roots[1]], deadline.Token);
        var pending = ProcessRuntimeRootSequenceLocks.AcquireAsync(roots.Reverse(), cancellation.Token);
        Assert.False(pending.IsCompleted);
        Assert.Equal(baseline + 2, ProcessRuntimeRootSequenceLocks.RetainedRootCount);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending).WaitAsync(deadline.Token);
        using (await ProcessRuntimeRootSequenceLocks.AcquireAsync([roots[0]], deadline.Token)) {
            Assert.Equal(baseline + 2, ProcessRuntimeRootSequenceLocks.RetainedRootCount);
        }
        later.Dispose();
        Assert.Equal(baseline, ProcessRuntimeRootSequenceLocks.RetainedRootCount);
    }

    [Fact]
    public async Task Canceling_one_waiter_preserves_the_holder_and_other_waiter() {
        var baseline = ProcessRuntimeRootSequenceLocks.RetainedRootCount;
        var root = Guid.NewGuid();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
        using var holder = await ProcessRuntimeRootSequenceLocks.AcquireAsync([root], deadline.Token);
        var canceled = ProcessRuntimeRootSequenceLocks.AcquireAsync([root], cancellation.Token);
        var surviving = ProcessRuntimeRootSequenceLocks.AcquireAsync([root], deadline.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => canceled).WaitAsync(deadline.Token);
        Assert.False(surviving.IsCompleted);
        Assert.Equal(baseline + 1, ProcessRuntimeRootSequenceLocks.RetainedRootCount);
        holder.Dispose();
        using (await surviving.WaitAsync(deadline.Token)) {
            Assert.Equal(baseline + 1, ProcessRuntimeRootSequenceLocks.RetainedRootCount);
        }
        Assert.Equal(baseline, ProcessRuntimeRootSequenceLocks.RetainedRootCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Contention_and_reversed_multi_root_orders_never_overlap(bool multipleRoots) {
        var baseline = ProcessRuntimeRootSequenceLocks.RetainedRootCount;
        var roots = multipleRoots ? new[] { Guid.NewGuid(), Guid.NewGuid() } : [Guid.NewGuid()];
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var active = 0;
        var entered = 0;
        var tasks = Enumerable.Range(0, 8).Select(async worker => {
            for (var iteration = 0; iteration < 100; iteration++) {
                using var held = await ProcessRuntimeRootSequenceLocks.AcquireAsync(worker % 2 == 0 ? roots : roots.Reverse(), deadline.Token);
                var simultaneous = Interlocked.Increment(ref active);
                try {
                    Assert.Equal(1, simultaneous);
                    await Task.Yield();
                    Interlocked.Increment(ref entered);
                } finally {
                    Interlocked.Decrement(ref active);
                }
            }
        }).ToArray();
        await Task.WhenAll(tasks).WaitAsync(deadline.Token);
        Assert.Equal(800, entered);
        Assert.Equal(baseline, ProcessRuntimeRootSequenceLocks.RetainedRootCount);
    }

    [Fact]
    public async Task Concurrent_disposal_releases_a_lease_exactly_once() {
        var baseline = ProcessRuntimeRootSequenceLocks.RetainedRootCount;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        for (var iteration = 0; iteration < 100; iteration++) {
            var root = Guid.NewGuid();
            using var holder = await ProcessRuntimeRootSequenceLocks.AcquireAsync([root], deadline.Token);
            var first = ProcessRuntimeRootSequenceLocks.AcquireAsync([root], deadline.Token);
            var second = ProcessRuntimeRootSequenceLocks.AcquireAsync([root], deadline.Token);
            await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(holder.Dispose, deadline.Token))).WaitAsync(deadline.Token);
            var winner = await Task.WhenAny(first, second).WaitAsync(deadline.Token);
            var other = ReferenceEquals(winner, first) ? second : first;
            using (await winner.WaitAsync(deadline.Token)) {
                Assert.False(other.IsCompleted);
            }
            using (await other.WaitAsync(deadline.Token)) {
                Assert.Equal(baseline + 1, ProcessRuntimeRootSequenceLocks.RetainedRootCount);
            }
        }
        Assert.Equal(baseline, ProcessRuntimeRootSequenceLocks.RetainedRootCount);
    }

    [Fact]
    public async Task Completed_high_cardinality_roots_do_not_accumulate() {
        var baseline = ProcessRuntimeRootSequenceLocks.RetainedRootCount;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        for (var iteration = 0; iteration < 10000; iteration++) {
            using (await ProcessRuntimeRootSequenceLocks.AcquireAsync([Guid.NewGuid()], deadline.Token)) {
                Assert.Equal(baseline + 1, ProcessRuntimeRootSequenceLocks.RetainedRootCount);
            }
            Assert.Equal(baseline, ProcessRuntimeRootSequenceLocks.RetainedRootCount);
        }
    }

    [Fact]
    public async Task Repeated_cancellation_and_root_reuse_does_not_replace_a_live_semaphore() {
        var baseline = ProcessRuntimeRootSequenceLocks.RetainedRootCount;
        var root = Guid.NewGuid();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        for (var iteration = 0; iteration < 300; iteration++) {
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
            using var holder = await ProcessRuntimeRootSequenceLocks.AcquireAsync([root], deadline.Token);
            var canceled = ProcessRuntimeRootSequenceLocks.AcquireAsync([root], cancellation.Token);
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => canceled).WaitAsync(deadline.Token);
            var next = ProcessRuntimeRootSequenceLocks.AcquireAsync([root], deadline.Token);
            Assert.False(next.IsCompleted);
            holder.Dispose();
            using (await next.WaitAsync(deadline.Token)) {
                Assert.Equal(baseline + 1, ProcessRuntimeRootSequenceLocks.RetainedRootCount);
            }
            Assert.Equal(baseline, ProcessRuntimeRootSequenceLocks.RetainedRootCount);
        }
    }
}
