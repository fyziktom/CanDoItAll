using CanDoItAll.Processes.Application;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessCancellationRecoveryDeadlineTests {
    public enum Boundary { Claim, Owner, Checkpoint, CommittedCheckpoint }

    [Theory]
    [InlineData(Boundary.Claim)]
    [InlineData(Boundary.Owner)]
    [InlineData(Boundary.Checkpoint)]
    [InlineData(Boundary.CommittedCheckpoint)]
    public async Task Deadline_never_rewrites_durable_evidence_or_guesses_a_lost_acknowledgement(Boundary boundary) {
        var fixture = new Fixture(boundary);
        var original = fixture.Saved;
        var execution = fixture.RunAsync();
        await fixture.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Clock.Expire();
        var result = await execution.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(ProcessCancellationRecoverySliceStatus.TimedOut, result.Status);
        Assert.Equal(boundary == Boundary.Claim ? null : original.RootRunId, result.RootRunId);
        Assert.Equal(boundary == Boundary.Claim ? 0 : 1, result.Operations);
        Assert.True(fixture.Exited.Task.IsCompletedSuccessfully);
        Assert.Equal(boundary is Boundary.Checkpoint or Boundary.CommittedCheckpoint ? 1 : 0, fixture.CheckpointCalls);
        Assert.Equal(boundary == Boundary.CommittedCheckpoint
            ? original with { Intent = null, ProgressRevision = original.ProgressRevision + 1 }
            : original, fixture.Saved);
    }

    [Fact]
    public async Task A_noncooperative_owner_is_awaited_and_cannot_trigger_another_operation_after_deadline() {
        var fixture = new Fixture(Boundary.Owner, cooperative: false);
        var original = fixture.Saved;
        var execution = fixture.RunAsync();
        await fixture.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Clock.Expire();
        Assert.False(execution.IsCompleted);
        Assert.False(fixture.Exited.Task.IsCompleted);
        fixture.Release.SetResult();
        Assert.Equal(ProcessCancellationRecoverySliceStatus.TimedOut, (await execution.WaitAsync(TimeSpan.FromSeconds(5))).Status);
        Assert.True(fixture.Exited.Task.IsCompletedSuccessfully);
        Assert.Equal(0, fixture.CheckpointCalls);
        Assert.Equal(original, fixture.Saved);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Caller_cancellation_wins_when_both_tokens_are_cancelled_before_owner_unwinds(bool callerFirst) {
        var fixture = new Fixture(Boundary.Owner, cooperative: false);
        var original = fixture.Saved;
        using var caller = new CancellationTokenSource();
        var execution = fixture.RunAsync(caller.Token);
        await fixture.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if (callerFirst) {
            await caller.CancelAsync();
            fixture.Clock.Expire();
        } else {
            fixture.Clock.Expire();
            await caller.CancelAsync();
        }
        Assert.False(execution.IsCompleted);
        fixture.Release.SetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => execution.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.True(fixture.Exited.Task.IsCompletedSuccessfully);
        Assert.Equal(0, fixture.CheckpointCalls);
        Assert.Equal(original, fixture.Saved);
    }

    private sealed class Fixture(Boundary boundary, bool cooperative = true) : IProcessCancellationRecoveryQueue, IProcessCancellationRecoveryStep {
        internal DeadlineClock Clock { get; } = new();
        internal ProcessCancellationRecoveryClaim Saved { get; private set; } = RecoveryContractFixture.Claim() with {
            Intent = RecoveryContractFixture.Intents()[0].Intent
        };
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Exited { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int CheckpointCalls { get; private set; }

        internal Task<ProcessCancellationRecoverySliceResult> RunAsync(CancellationToken cancellationToken = default)
            => new ProcessCancellationRecoveryCoordinator(this, this, Clock, NullLogger<ProcessCancellationRecoveryCoordinator>.Instance)
                .RunSliceAsync(ProcessCancellationRecoveryOrigin.LiveBarrier, ProcessCancellationRecoverySliceLimits.Default, cancellationToken);

        public async Task<ProcessCancellationRecoveryClaim?> ClaimAsync(ProcessCancellationRecoveryOrigin origin, TimeSpan leaseDuration,
            CancellationToken cancellationToken = default) {
            await HoldAsync(Boundary.Claim, cancellationToken);
            return Saved;
        }

        public async Task<ProcessCancellationRecoveryCheckpoint> AdvanceAsync(ProcessCancellationRecoveryClaim claim, CancellationToken cancellationToken) {
            Assert.Equal(Saved, claim);
            await HoldAsync(Boundary.Owner, cancellationToken);
            return new(claim.Position, Resolution: new(claim.Intent!.Context.CommandId, ProcessCancellationIntentOutcome.Committed),
                RetryAfter: TimeSpan.Zero);
        }

        public async Task<ProcessCancellationCheckpointResult> CheckpointAsync(ProcessCancellationRecoveryClaim claim,
            ProcessCancellationRecoveryCheckpoint checkpoint, CancellationToken cancellationToken = default) {
            CheckpointCalls++;
            await HoldAsync(Boundary.Checkpoint, cancellationToken);
            Assert.Equal(Saved, claim);
            checkpoint.Validate(Saved);
            Saved = Saved with { Position = checkpoint.Position, Intent = checkpoint.Intent, ProgressRevision = Saved.ProgressRevision + 1 };
            await HoldAsync(Boundary.CommittedCheckpoint, cancellationToken);
            return new(ProcessCancellationCheckpointStatus.Released);
        }

        public Task<ProcessCancellationSeedResult> SeedLegacyAsync(int take = 16, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("A coordinator slice cannot seed legacy roots.");

        private async Task HoldAsync(Boundary current, CancellationToken cancellationToken) {
            if (current != boundary) {
                return;
            }
            Entered.SetResult();
            try {
                if (cooperative) {
                    await Release.Task.WaitAsync(cancellationToken);
                } else {
                    await Release.Task;
                }
            } finally {
                Exited.SetResult();
            }
        }
    }

    private sealed class DeadlineClock : TimeProvider {
        private DeadlineTimer? timer;
        private long timestamp;
        private TimeSpan due;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => timestamp;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) {
            Assert.Null(timer);
            Assert.Equal(Timeout.InfiniteTimeSpan, period);
            due = dueTime;
            return timer = new(callback, state);
        }

        internal void Expire() {
            Assert.NotNull(timer);
            timestamp += due.Ticks;
            timer.Fire();
        }

        private sealed class DeadlineTimer(TimerCallback callback, object? state) : ITimer {
            private bool disposed;
            internal void Fire() {
                Assert.False(disposed);
                callback(state);
            }
            public bool Change(TimeSpan dueTime, TimeSpan period) => throw new InvalidOperationException("The slice owns one fixed deadline.");
            public void Dispose() => disposed = true;
            public ValueTask DisposeAsync() {
                Dispose();
                return ValueTask.CompletedTask;
            }
        }
    }
}
