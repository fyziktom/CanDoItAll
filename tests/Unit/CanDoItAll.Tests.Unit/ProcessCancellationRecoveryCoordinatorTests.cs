using CanDoItAll.Processes.Application;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessCancellationRecoveryCoordinatorTests {
    [Fact]
    public async Task Prepared_intent_is_saved_before_owner_execution_and_its_exact_resolution() {
        var fixture = new Fixture();
        var intent = RecoveryContractFixture.Intents()[0].Intent;
        fixture.Step = (claim, _) => {
            Assert.Equal(fixture.Queue.Saved, claim);
            if (claim.Intent is null) {
                return Task.FromResult(new ProcessCancellationRecoveryCheckpoint(claim.Position, intent));
            }
            Assert.Same(intent, fixture.Queue.Saved!.Intent);
            Assert.Single(fixture.Queue.Checkpoints);
            return Task.FromResult(new ProcessCancellationRecoveryCheckpoint(claim.Position,
                Resolution: new(intent.Context.CommandId, ProcessCancellationIntentOutcome.Committed)));
        };

        var result = await fixture.RunAsync(2);

        Assert.Equal(ProcessCancellationRecoverySliceStatus.Yielded, result.Status);
        Assert.Equal(2, result.Operations);
        Assert.Equal(3, fixture.Queue.Checkpoints.Count);
        Assert.Null(fixture.Queue.Saved!.Intent);
        Assert.Equal(ProcessCancellationRecoveryPosition.Initial, fixture.Queue.Saved.Position);
    }

    [Fact]
    public async Task Unknown_checkpoint_acknowledgement_leaves_saved_intent_for_exact_replay() {
        var fixture = new Fixture();
        var intent = RecoveryContractFixture.Intents()[0].Intent;
        var ownerCalls = 0;
        fixture.Queue.FailAfterSave = true;
        fixture.Step = (claim, _) => {
            if (claim.Intent is null) {
                return Task.FromResult(new ProcessCancellationRecoveryCheckpoint(claim.Position, intent));
            }
            ownerCalls++;
            Assert.Same(intent, claim.Intent);
            return Task.FromResult(new ProcessCancellationRecoveryCheckpoint(claim.Position,
                Resolution: new(intent.Context.CommandId, ProcessCancellationIntentOutcome.Committed)));
        };

        await Assert.ThrowsAsync<IOException>(() => fixture.RunAsync());
        Assert.Equal(0, ownerCalls);
        Assert.Same(intent, fixture.Queue.Saved!.Intent);

        await fixture.RunAsync(1);
        Assert.Equal(1, ownerCalls);
        Assert.Null(fixture.Queue.Saved!.Intent);
    }

    [Fact]
    public async Task Unknown_owner_outcome_retains_original_intent_and_backs_off_before_replay() {
        var fixture = new Fixture();
        var intent = RecoveryContractFixture.Intents()[0].Intent;
        fixture.Queue.Saved = fixture.Queue.Saved! with { Intent = intent };
        fixture.Step = (_, _) => throw new IOException("Owner acknowledgement lost.");

        var result = await fixture.RunAsync();

        Assert.Equal(ProcessCancellationRecoverySliceStatus.Pending, result.Status);
        Assert.Equal(ProcessCancellationRecoveryPending.UnresolvedEffects, result.Pending);
        Assert.Same(intent, fixture.Queue.Saved!.Intent);
        Assert.Equal(ProcessCancellationRecoverySliceLimits.Default.FailureDelay, Assert.Single(fixture.Queue.Checkpoints).RetryAfter);
        fixture.Step = (claim, _) => {
            Assert.Same(intent, claim.Intent);
            return Task.FromResult(new ProcessCancellationRecoveryCheckpoint(claim.Position,
                Resolution: new(intent.Context.CommandId, ProcessCancellationIntentOutcome.Committed)));
        };
        await fixture.RunAsync(1);
        Assert.Null(fixture.Queue.Saved.Intent);
    }

    [Fact]
    public async Task Lost_ownership_ends_the_slice_without_another_owner_call_or_release() {
        var fixture = new Fixture();
        fixture.Queue.ResultOverride = new(ProcessCancellationCheckpointStatus.NotOwned);

        var result = await fixture.RunAsync();

        Assert.Equal(ProcessCancellationRecoverySliceStatus.NotOwned, result.Status);
        Assert.Equal(1, fixture.Calls);
        Assert.Single(fixture.Queue.Checkpoints);
    }

    [Fact]
    public async Task Caller_cancellation_during_owner_work_preserves_the_intent_and_expiring_lease() {
        var fixture = new Fixture();
        using var cancellation = new CancellationTokenSource();
        var original = fixture.Queue.Saved! with { Intent = RecoveryContractFixture.Intents()[0].Intent };
        fixture.Queue.Saved = original;
        fixture.Step = (_, token) => {
            cancellation.Cancel();
            token.ThrowIfCancellationRequested();
            throw new InvalidOperationException("Cancellation must propagate.");
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.RunAsync(cancellationToken: cancellation.Token));

        Assert.Equal(original, fixture.Queue.Saved);
        Assert.Empty(fixture.Queue.Checkpoints);
    }

    [Fact]
    public async Task Owner_cancellation_without_caller_cancellation_is_not_a_definite_refusal() {
        var fixture = new Fixture();
        fixture.Queue.Saved = fixture.Queue.Saved! with { Intent = RecoveryContractFixture.Intents()[0].Intent };
        fixture.Step = (_, _) => throw new OperationCanceledException();

        await Assert.ThrowsAsync<OperationCanceledException>(() => fixture.RunAsync());

        Assert.NotNull(fixture.Queue.Saved.Intent);
        Assert.Empty(fixture.Queue.Checkpoints);
    }

    [Fact]
    public async Task Time_exhausted_while_preparing_saves_the_intent_then_yields_without_execution() {
        var fixture = new Fixture();
        var intent = RecoveryContractFixture.Intents()[0].Intent;
        fixture.Step = (claim, _) => {
            fixture.Clock.Advance(TimeSpan.FromSeconds(3));
            return Task.FromResult(new ProcessCancellationRecoveryCheckpoint(claim.Position, intent));
        };

        var result = await fixture.RunAsync();

        Assert.Equal(ProcessCancellationRecoverySliceStatus.Yielded, result.Status);
        Assert.Equal(1, fixture.Calls);
        Assert.Same(intent, fixture.Queue.Saved!.Intent);
        Assert.Equal(2, fixture.Queue.Checkpoints.Count);
    }

    [Fact]
    public async Task Time_spent_claiming_counts_against_the_slice_budget() {
        var fixture = new Fixture();
        fixture.Queue.OnClaim = _ => fixture.Clock.Advance(TimeSpan.FromSeconds(3));

        var result = await fixture.RunAsync();

        Assert.Equal(ProcessCancellationRecoverySliceStatus.Yielded, result.Status);
        Assert.Equal(0, result.Operations);
        Assert.Equal(0, fixture.Calls);
        Assert.Single(fixture.Queue.Checkpoints);
    }

    [Fact]
    public async Task An_owner_that_makes_no_progress_still_exhausts_the_finite_operation_budget() {
        var fixture = new Fixture();

        var result = await fixture.RunAsync(3);

        Assert.Equal(ProcessCancellationRecoverySliceStatus.Yielded, result.Status);
        Assert.Equal(3, fixture.Calls);
        Assert.Equal(4, fixture.Queue.Checkpoints.Count);
        Assert.Equal(ProcessCancellationRecoverySliceLimits.Default.YieldDelay, fixture.Queue.Checkpoints[^1].RetryAfter);
    }

    [Fact]
    public async Task Corrupt_claim_consumes_its_turn_without_selecting_another_class() {
        var fixture = new Fixture();
        fixture.Queue.OnClaim = _ => throw new ProcessCancellationRecoveryCorruptRecordException(RecoveryContractFixture.Root, new InvalidDataException());

        var result = await fixture.RunAsync();

        Assert.Equal(ProcessCancellationRecoverySliceStatus.InvalidEvidence, result.Status);
        Assert.Equal(1, result.Operations);
        Assert.Equal(RecoveryContractFixture.Root, result.RootRunId);
        Assert.Single(fixture.Queue.Claims);
        Assert.Empty(fixture.Queue.Checkpoints);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Empty_scheduled_class_checks_the_other_class_at_most_once(bool otherEmpty) {
        var fixture = new Fixture();
        fixture.Queue.Saved = otherEmpty ? null : fixture.Queue.Saved! with {
            Origin = ProcessCancellationRecoveryOrigin.LegacySeed, BarrierCommandId = null
        };

        var result = await fixture.RunAsync(1);

        Assert.Equal(new[] { ProcessCancellationRecoveryOrigin.LiveBarrier, ProcessCancellationRecoveryOrigin.LegacySeed }, fixture.Queue.Claims);
        Assert.Equal(otherEmpty ? ProcessCancellationRecoverySliceStatus.Idle : ProcessCancellationRecoverySliceStatus.Yielded, result.Status);
        Assert.Equal(otherEmpty ? 0 : 1, fixture.Calls);
    }

    [Fact]
    public async Task Scheduled_work_does_not_claim_the_other_class() {
        var fixture = new Fixture();

        await fixture.RunAsync(1);

        Assert.Equal(ProcessCancellationRecoveryOrigin.LiveBarrier, Assert.Single(fixture.Queue.Claims));
    }

    [Fact]
    public async Task Invalid_limits_or_origin_are_rejected_before_claiming_work() {
        var fixture = new Fixture();
        var valid = ProcessCancellationRecoverySliceLimits.Default;
        ProcessCancellationRecoverySliceLimits[] invalid = [valid with { MaximumOperations = 0 }, valid with { MaximumOperations = 65 },
            valid with { MaximumDuration = TimeSpan.Zero }, valid with { MaximumDuration = valid.LeaseDuration },
            valid with { LeaseDuration = TimeSpan.FromMilliseconds(999) }, valid with { LeaseDuration = TimeSpan.FromMinutes(16) },
            valid with { YieldDelay = TimeSpan.FromTicks(-1) }, valid with { FailureDelay = TimeSpan.Zero }];
        foreach (var limits in invalid) {
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => fixture.Coordinator.RunSliceAsync(ProcessCancellationRecoveryOrigin.LiveBarrier, limits));
        }
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => fixture.Coordinator.RunSliceAsync((ProcessCancellationRecoveryOrigin)99, valid));
        Assert.Empty(fixture.Queue.Claims);
    }

    [Fact]
    public async Task Malformed_owner_checkpoint_cannot_clear_unresolved_intent() {
        var fixture = new Fixture();
        var original = fixture.Queue.Saved! with { Intent = RecoveryContractFixture.Intents()[0].Intent };
        fixture.Queue.Saved = original;
        fixture.Step = (claim, _) => Task.FromResult(new ProcessCancellationRecoveryCheckpoint(claim.Position));

        var result = await fixture.RunAsync();

        Assert.Equal(ProcessCancellationRecoveryPending.InvalidEvidence, result.Pending);
        Assert.Same(original.Intent, fixture.Queue.Saved!.Intent);
        Assert.Equal(original.Position, fixture.Queue.Saved.Position);
        Assert.Single(fixture.Queue.Checkpoints);
    }

    [Fact]
    public async Task Verified_final_root_completion_ends_without_selecting_or_releasing_again() {
        var fixture = new Fixture();
        var intent = RecoveryContractFixture.Intents()[4].Intent;
        fixture.Queue.Saved = fixture.Queue.Saved! with { Position = new(ProcessCancellationRecoveryStage.Quiescence), Intent = intent };
        fixture.Step = (_, _) => Task.FromResult(new ProcessCancellationRecoveryCheckpoint(new(ProcessCancellationRecoveryStage.Completed),
            Resolution: new(intent.Context.CommandId, ProcessCancellationIntentOutcome.Committed)));

        var result = await fixture.RunAsync();

        Assert.Equal(ProcessCancellationRecoverySliceStatus.Completed, result.Status);
        Assert.Single(fixture.Queue.Checkpoints);
        Assert.Single(fixture.Queue.Claims);
    }

    [Fact]
    public async Task Explicit_pending_release_does_not_add_another_yield() {
        var fixture = new Fixture();
        fixture.Step = (claim, _) => Task.FromResult(new ProcessCancellationRecoveryCheckpoint(claim.Position,
            Pending: ProcessCancellationRecoveryPending.ActiveWork, RetryAfter: TimeSpan.FromSeconds(1)));

        var result = await fixture.RunAsync();

        Assert.Equal(ProcessCancellationRecoverySliceStatus.Pending, result.Status);
        Assert.Equal(ProcessCancellationRecoveryPending.ActiveWork, result.Pending);
        Assert.Single(fixture.Queue.Checkpoints);
    }

    [Theory]
    [InlineData(ProcessCancellationCheckpointStatus.Applied)]
    [InlineData((ProcessCancellationCheckpointStatus)99)]
    public async Task Unsupported_checkpoint_acknowledgement_fails_without_guessing_ownership(ProcessCancellationCheckpointStatus status) {
        var fixture = new Fixture();
        fixture.Queue.ResultOverride = new(status);

        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.RunAsync());

        Assert.Equal(1, fixture.Calls);
        Assert.Single(fixture.Queue.Checkpoints);
    }

    private sealed class Fixture : IProcessCancellationRecoveryStep {
        public Queue Queue { get; } = new();
        public Clock Clock { get; } = new();
        public int Calls { get; private set; }
        public Func<ProcessCancellationRecoveryClaim, CancellationToken, Task<ProcessCancellationRecoveryCheckpoint>> Step { get; set; }
            = (claim, _) => Task.FromResult(new ProcessCancellationRecoveryCheckpoint(claim.Position, claim.Intent, claim.Pending));
        public ProcessCancellationRecoveryCoordinator Coordinator => new(Queue, this, Clock, NullLogger<ProcessCancellationRecoveryCoordinator>.Instance);
        public Task<ProcessCancellationRecoverySliceResult> RunAsync(int maximumOperations = 8, CancellationToken cancellationToken = default)
            => Coordinator.RunSliceAsync(ProcessCancellationRecoveryOrigin.LiveBarrier,
                ProcessCancellationRecoverySliceLimits.Default with { MaximumOperations = maximumOperations }, cancellationToken);
        public Task<ProcessCancellationRecoveryCheckpoint> AdvanceAsync(ProcessCancellationRecoveryClaim claim, CancellationToken cancellationToken) {
            Calls++;
            return Step(claim, cancellationToken);
        }
    }

    private sealed class Queue : IProcessCancellationRecoveryQueue {
        public ProcessCancellationRecoveryClaim? Saved { get; set; } = RecoveryContractFixture.Claim();
        public List<ProcessCancellationRecoveryOrigin> Claims { get; } = [];
        public List<ProcessCancellationRecoveryCheckpoint> Checkpoints { get; } = [];
        public Action<ProcessCancellationRecoveryOrigin>? OnClaim { get; set; }
        public bool FailAfterSave { get; set; }
        public ProcessCancellationCheckpointResult? ResultOverride { get; set; }

        public Task<ProcessCancellationRecoveryClaim?> ClaimAsync(ProcessCancellationRecoveryOrigin origin, TimeSpan leaseDuration, CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            Claims.Add(origin);
            OnClaim?.Invoke(origin);
            return Task.FromResult(Saved?.Origin == origin ? Saved : null);
        }

        public Task<ProcessCancellationCheckpointResult> CheckpointAsync(ProcessCancellationRecoveryClaim claim,
            ProcessCancellationRecoveryCheckpoint checkpoint, CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            Checkpoints.Add(checkpoint);
            if (ResultOverride is not null) {
                return Task.FromResult(ResultOverride);
            }
            Assert.Equal(Saved, claim);
            checkpoint.Validate(Saved!);
            Saved = claim with { Position = checkpoint.Position, Intent = checkpoint.Intent, Pending = checkpoint.Pending, ProgressRevision = claim.ProgressRevision + 1 };
            if (FailAfterSave) {
                FailAfterSave = false;
                throw new IOException("Checkpoint committed but acknowledgement was lost.");
            }
            return Task.FromResult(checkpoint.RetryAfter is not null || checkpoint.Position.Stage == ProcessCancellationRecoveryStage.Completed
                ? new ProcessCancellationCheckpointResult(ProcessCancellationCheckpointStatus.Released)
                : new ProcessCancellationCheckpointResult(ProcessCancellationCheckpointStatus.Applied, Saved));
        }

        public Task<ProcessCancellationSeedResult> SeedLegacyAsync(int take = 16, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Seeding belongs to the host scheduler.");
    }

    private sealed class Clock : TimeProvider {
        private long timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => timestamp;
        public void Advance(TimeSpan duration) => timestamp += duration.Ticks;
    }
}
