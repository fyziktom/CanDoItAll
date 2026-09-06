using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class SharedDeliveryLifecycleTests {
    [Fact]
    public async Task Successful_acknowledgement_prevents_second_delivery_and_releases_attempt() {
        var recovery = new SharedProviderRecovery();
        var attempt = Begin(recovery);
        var calls = 0;
        var result = await recovery.DeliverTargetAsync(attempt, attempt.ProviderId, () => true, _ => {
            calls++;
            return Task.CompletedTask;
        });
        Assert.Equal(SharedProviderDeliveryDisposition.Acknowledged, result);
        Assert.Null(recovery.FindTarget(attempt.ProviderId));
        Assert.Null(recovery.PendingDelivery(attempt.AttemptId));
        recovery.RecordCommit(attempt.AttemptId, new(SharedProviderChangeKind.Publication, [attempt.ProviderId]));
        Assert.Equal(SharedProviderDeliveryDisposition.NotCurrent,
            await recovery.DeliverTargetAsync(attempt, attempt.ProviderId, () => true, _ => throw new InvalidOperationException()));
        Assert.Null(recovery.PendingDelivery(attempt.AttemptId));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Concurrent_delivery_retry_is_serialized() {
        var recovery = new SharedProviderRecovery();
        var attempt = Begin(recovery);
        var pending = new TaskCompletionSource();
        var calls = 0;
        var first = recovery.DeliverTargetAsync(attempt, attempt.ProviderId, () => true, _ => {
            calls++;
            return pending.Task;
        });
        var second = await recovery.DeliverTargetAsync(attempt, attempt.ProviderId, () => true, _ => {
            calls++;
            return Task.CompletedTask;
        });
        Assert.Equal(SharedProviderDeliveryDisposition.InProgress, second);
        Assert.Equal(1, calls);
        pending.SetResult();
        Assert.Equal(SharedProviderDeliveryDisposition.Acknowledged, await first);
    }

    [Fact]
    public async Task Target_A_delivery_cannot_be_acknowledged_by_target_B() {
        var recovery = new SharedProviderRecovery();
        var attempt = Begin(recovery);
        Assert.Equal(SharedProviderDeliveryDisposition.NotCurrent,
            await recovery.DeliverTargetAsync(attempt, Guid.NewGuid(), () => true, _ => throw new InvalidOperationException()));
        Assert.NotNull(recovery.PendingDelivery(attempt.AttemptId));
        Assert.Same(attempt, recovery.FindTarget(attempt.ProviderId));
    }

    [Fact]
    public async Task Source_A_delivery_cannot_be_acknowledged_by_source_B() {
        var recovery = new SharedProviderRecovery();
        var attempt = new SharedProviderSourceMutationAttempt(Guid.NewGuid(), SharedProviderSourceMutationKind.Delete);
        recovery.BeginSource(attempt);
        recovery.RecordCommit(attempt.AttemptId, new(SharedProviderChangeKind.SourceDeleted, []));
        Assert.Equal(SharedProviderDeliveryDisposition.NotCurrent,
            await recovery.DeliverSourceAsync(attempt, Guid.NewGuid(), () => true, _ => throw new InvalidOperationException()));
        Assert.Same(attempt, recovery.Source);
        Assert.False(recovery.CompleteSource(attempt));
    }

    [Fact]
    public async Task Callback_failure_retains_pending_delivery_without_repeating_mutation() {
        var recovery = new SharedProviderRecovery();
        var attempt = Begin(recovery);
        Assert.Equal(SharedProviderDeliveryDisposition.Pending,
            await recovery.DeliverTargetAsync(attempt, attempt.ProviderId, () => true, _ => Task.FromException(new IOException("Interrupted receiver."))));
        Assert.Same(attempt, recovery.FindTarget(attempt.ProviderId));
        Assert.False(recovery.CompleteTarget(attempt));
        Assert.Equal(SharedProviderDeliveryDisposition.Acknowledged,
            await recovery.DeliverTargetAsync(attempt, attempt.ProviderId, () => true, _ => Task.CompletedTask));
    }

    [Fact]
    public async Task Receiver_acknowledgement_survives_sender_teardown_without_duplicate_callback() {
        var recovery = new SharedProviderRecovery();
        var attempt = Begin(recovery);
        var ownerCurrent = true;
        var effects = 0;
        var result = await recovery.DeliverTargetAsync(attempt, attempt.ProviderId, () => ownerCurrent,
            delivery => delivery.ReconcileAsync(() => {
                effects++;
                ownerCurrent = false;
                return Task.CompletedTask;
            }));
        Assert.Equal(SharedProviderDeliveryDisposition.Pending, result);
        Assert.NotNull(recovery.PendingDelivery(attempt.AttemptId));
        Assert.Equal(SharedProviderDeliveryDisposition.Acknowledged,
            await recovery.DeliverTargetAsync(attempt, attempt.ProviderId, () => true, _ => throw new InvalidOperationException()));
        Assert.Equal(1, effects);
        Assert.Null(recovery.FindTarget(attempt.ProviderId));
    }

    [Fact]
    public async Task Disposed_or_stale_component_emits_no_new_callback() {
        var recovery = new SharedProviderRecovery();
        var attempt = Begin(recovery);
        Assert.Equal(SharedProviderDeliveryDisposition.NotCurrent,
            await recovery.DeliverTargetAsync(attempt, attempt.ProviderId, () => false, _ => throw new InvalidOperationException()));
        Assert.NotNull(recovery.PendingDelivery(attempt.AttemptId));
    }

    [Fact]
    public async Task Stale_shared_completion_cannot_clear_newer_attempt() {
        var recovery = new SharedProviderRecovery();
        var old = Begin(recovery);
        await recovery.DeliverTargetAsync(old, old.ProviderId, () => true, _ => Task.CompletedTask);
        var current = recovery.BeginTarget(old.ProviderId, SharedProviderTargetMutationKind.Publish, old.Before);
        Assert.False(recovery.CompleteTarget(old));
        recovery.RecordCommit(old.AttemptId, new(SharedProviderChangeKind.Publication, [old.ProviderId]));
        Assert.Same(current, recovery.FindTarget(old.ProviderId));
        Assert.Null(recovery.PendingDelivery(old.AttemptId));
    }

    [Fact]
    public void Pending_delivery_survives_cleanup_of_unrelated_attempts() {
        var recovery = new SharedProviderRecovery();
        var pending = Begin(recovery);
        var otherState = SharedTargetVerificationTests.Local(false);
        var other = recovery.BeginTarget(otherState.ProviderProfileId, SharedProviderTargetMutationKind.Publish, otherState);
        Assert.True(recovery.CompleteTarget(other));
        Assert.Same(pending, recovery.FindTarget(pending.ProviderId));
        Assert.NotNull(recovery.PendingDelivery(pending.AttemptId));
    }

    [Fact]
    public async Task Terminal_source_attempt_releases_bookkeeping_without_clearing_newer_attempt() {
        var recovery = new SharedProviderRecovery();
        var old = new SharedProviderSourceMutationAttempt(Guid.NewGuid(), SharedProviderSourceMutationKind.Delete);
        recovery.BeginSource(old);
        recovery.RecordCommit(old.AttemptId, new(SharedProviderChangeKind.SourceDeleted, []));
        Assert.Equal(SharedProviderDeliveryDisposition.Acknowledged,
            await recovery.DeliverSourceAsync(old, old.SourceId, () => true, _ => Task.CompletedTask));
        Assert.Null(recovery.Source);
        Assert.Null(recovery.PendingDelivery(old.AttemptId));
        var current = new SharedProviderSourceMutationAttempt(old.SourceId, SharedProviderSourceMutationKind.Create);
        recovery.BeginSource(current);
        Assert.False(recovery.CompleteSource(old));
        recovery.RecordCommit(old.AttemptId, new(SharedProviderChangeKind.SourceDeleted, []));
        Assert.Same(current, recovery.Source);
        Assert.Null(recovery.PendingDelivery(old.AttemptId));
    }

    [Fact]
    public void Known_source_commit_revokes_controlled_retry_until_delivery() {
        var recovery = new SharedProviderRecovery();
        var attempt = new SharedProviderSourceMutationAttempt(Guid.NewGuid(), SharedProviderSourceMutationKind.Create);
        recovery.BeginSource(attempt);
        recovery.AllowSourceRetry(attempt);
        Assert.True(recovery.SourceRetryAllowed);
        recovery.RecordCommit(attempt.AttemptId, new(SharedProviderChangeKind.SourceConfiguration, []));
        Assert.False(recovery.SourceRetryAllowed);
        Assert.Throws<InvalidOperationException>(() => recovery.BeginSource(attempt));
        Assert.Same(attempt, recovery.Source);
    }

    private static SharedProviderTargetAttempt Begin(SharedProviderRecovery recovery) {
        var state = SharedTargetVerificationTests.Local(false);
        var attempt = recovery.BeginTarget(state.ProviderProfileId, SharedProviderTargetMutationKind.Publish, state);
        recovery.RecordCommit(attempt.AttemptId, new(SharedProviderChangeKind.Publication, [state.ProviderProfileId]));
        return attempt;
    }
}
