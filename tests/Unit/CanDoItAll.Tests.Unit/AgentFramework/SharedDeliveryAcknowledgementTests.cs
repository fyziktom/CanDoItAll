using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class SharedDeliveryAcknowledgementTests {
    [Fact]
    public async Task Callback_return_without_receiver_ack_keeps_delivery_pending() {
        var (recovery, attempt) = Begin();
        var result = await recovery.DeliverTargetAsync(attempt, attempt.ProviderId, () => true, _ => Task.CompletedTask);
        Assert.Equal(SharedProviderDeliveryDisposition.Pending, result);
        Assert.False(recovery.PendingDelivery(attempt.AttemptId)!.IsAcknowledged);
        Assert.Same(attempt, recovery.FindTarget(attempt.ProviderId));
        Assert.False(recovery.CompleteTarget(attempt));
    }

    [Fact]
    public async Task Callback_exception_before_ack_keeps_delivery_pending() {
        var (recovery, attempt) = Begin();
        var result = await recovery.DeliverTargetAsync(attempt, attempt.ProviderId, () => true,
            _ => Task.FromException(new IOException("Interrupted receiver.")));
        Assert.Equal(SharedProviderDeliveryDisposition.Pending, result);
        Assert.False(recovery.PendingDelivery(attempt.AttemptId)!.IsAcknowledged);
        Assert.Same(attempt, recovery.FindTarget(attempt.ProviderId));
    }

    [Fact]
    public async Task Reconcile_ack_then_callback_exception_does_not_repeat_reconciliation() {
        var (recovery, attempt) = Begin();
        var effects = 0;
        var result = await recovery.DeliverTargetAsync(attempt, attempt.ProviderId, () => true, async delivery => {
            await delivery.ReconcileAsync(() => {
                effects++;
                return Task.CompletedTask;
            });
            throw new IOException("Sender lost the callback result.");
        });
        Assert.Equal(SharedProviderDeliveryDisposition.Pending, result);
        Assert.True(recovery.PendingDelivery(attempt.AttemptId)!.IsAcknowledged);
        Assert.Equal(SharedProviderDeliveryDisposition.Acknowledged,
            await recovery.DeliverTargetAsync(attempt, attempt.ProviderId, () => true,
                _ => throw new InvalidOperationException("Completed reconciliation must not repeat.")));
        Assert.Equal(1, effects);
    }

    [Fact]
    public async Task Reconcile_ack_survives_sender_teardown() {
        var (recovery, attempt) = Begin();
        var current = true;
        var result = await recovery.DeliverTargetAsync(attempt, attempt.ProviderId, () => current,
            delivery => delivery.ReconcileAsync(() => {
                current = false;
                return Task.CompletedTask;
            }));
        Assert.Equal(SharedProviderDeliveryDisposition.Pending, result);
        Assert.True(recovery.PendingDelivery(attempt.AttemptId)!.IsAcknowledged);
        Assert.Same(attempt, recovery.FindTarget(attempt.ProviderId));
    }

    [Fact]
    public async Task Correct_owner_can_finalize_already_acknowledged_delivery() {
        var (recovery, attempt) = Begin();
        var envelope = recovery.PendingDelivery(attempt.AttemptId)!;
        await envelope.ReconcileAsync(() => Task.CompletedTask);
        Assert.Equal(SharedProviderDeliveryDisposition.Acknowledged,
            await recovery.DeliverTargetAsync(attempt, attempt.ProviderId, () => true,
                _ => throw new InvalidOperationException("Receiver has already completed.")));
        Assert.Null(recovery.FindTarget(attempt.ProviderId));
        Assert.Null(recovery.PendingDelivery(attempt.AttemptId));
    }

    [Fact]
    public async Task Wrong_owner_cannot_finalize_acknowledged_delivery() {
        var (recovery, attempt) = Begin();
        var envelope = recovery.PendingDelivery(attempt.AttemptId)!;
        await envelope.ReconcileAsync(() => Task.CompletedTask);
        Assert.Equal(SharedProviderDeliveryDisposition.NotCurrent,
            await recovery.DeliverTargetAsync(attempt, Guid.NewGuid(), () => true, _ => Task.CompletedTask));
        Assert.Equal(SharedProviderDeliveryDisposition.NotCurrent,
            await recovery.DeliverTargetAsync(attempt, attempt.ProviderId, () => false, _ => Task.CompletedTask));
        Assert.Same(envelope, recovery.PendingDelivery(attempt.AttemptId));
        Assert.Same(attempt, recovery.FindTarget(attempt.ProviderId));
    }

    [Fact]
    public async Task Successful_ack_releases_attempt_and_delivery_once() {
        var (recovery, attempt) = Begin();
        var callbacks = 0;
        Task Receive(SharedProviderChangeDelivery delivery) => delivery.ReconcileAsync(() => {
            callbacks++;
            return Task.CompletedTask;
        });
        Assert.Equal(SharedProviderDeliveryDisposition.Acknowledged,
            await recovery.DeliverTargetAsync(attempt, attempt.ProviderId, () => true, Receive));
        Assert.Equal(SharedProviderDeliveryDisposition.NotCurrent,
            await recovery.DeliverTargetAsync(attempt, attempt.ProviderId, () => true, Receive));
        Assert.Equal(1, callbacks);
        Assert.Null(recovery.FindTarget(attempt.ProviderId));
        Assert.Null(recovery.PendingDelivery(attempt.AttemptId));
    }

    [Fact]
    public async Task Delivery_retry_never_replays_backend_mutation() {
        var recovery = new SharedProviderRecovery();
        var attempt = new SharedProviderSourceMutationAttempt(Guid.NewGuid(), SharedProviderSourceMutationKind.Synchronize);
        recovery.BeginSource(attempt);
        recovery.RecordCommit(attempt.AttemptId, new(SharedProviderChangeKind.Reconciliation, []));
        Assert.Equal(SharedProviderDeliveryDisposition.Pending,
            await recovery.DeliverSourceAsync(attempt, attempt.SourceId, () => true, _ => Task.CompletedTask));
        Assert.Throws<InvalidOperationException>(() => recovery.BeginSource(attempt));
        Assert.Equal(SharedProviderDeliveryDisposition.Acknowledged,
            await recovery.DeliverSourceAsync(attempt, attempt.SourceId, () => true,
                delivery => delivery.ReconcileAsync(() => Task.CompletedTask)));
        Assert.Null(recovery.Source);
    }

    [Fact]
    public async Task Concurrent_reconciliation_joins_one_in_flight_delegate() {
        var delivery = Envelope();
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        Task Reconcile() {
            calls++;
            return pending.Task;
        }
        var first = delivery.ReconcileAsync(Reconcile);
        var second = delivery.ReconcileAsync(Reconcile);
        try {
            Assert.Same(first, second);
            Assert.Equal(1, calls);
            Assert.False(delivery.IsAcknowledged);
        } finally {
            pending.SetResult();
            await Task.WhenAll(first, second);
        }
        Assert.True(delivery.IsAcknowledged);
    }

    [Fact]
    public async Task Failed_concurrent_reconciliation_is_shared_and_explicit_retry_can_succeed() {
        var delivery = Envelope();
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        Task Reconcile() {
            calls++;
            return pending.Task;
        }
        var first = delivery.ReconcileAsync(Reconcile);
        var second = delivery.ReconcileAsync(Reconcile);
        pending.SetException(new IOException("Read unavailable."));
        await Assert.ThrowsAsync<IOException>(() => first);
        await Assert.ThrowsAsync<IOException>(() => second);
        Assert.Same(first, second);
        Assert.Equal(1, calls);
        Assert.False(delivery.IsAcknowledged);
        await delivery.ReconcileAsync(() => {
            calls++;
            return Task.CompletedTask;
        });
        Assert.True(delivery.IsAcknowledged);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Acknowledged_reconciliation_never_repeats_delegate() {
        var delivery = Envelope();
        await delivery.ReconcileAsync(() => Task.CompletedTask);
        await delivery.ReconcileAsync(() => throw new InvalidOperationException("Acknowledged effect cannot repeat."));
        Assert.True(delivery.IsAcknowledged);
    }

    [Fact]
    public async Task Canceled_reconciliation_remains_retryable() {
        var delivery = Envelope();
        using var owner = new CancellationTokenSource();
        owner.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => delivery.ReconcileAsync(() => Task.FromCanceled(owner.Token)));
        Assert.False(delivery.IsAcknowledged);
        await delivery.ReconcileAsync(() => Task.CompletedTask);
        Assert.True(delivery.IsAcknowledged);
    }

    private static SharedProviderChangeDelivery Envelope() => new(Guid.NewGuid(), new(SharedProviderChangeKind.Publication, []));

    private static (SharedProviderRecovery Recovery, SharedProviderTargetAttempt Attempt) Begin() {
        var recovery = new SharedProviderRecovery();
        var state = SharedTargetVerificationTests.Local(false);
        var attempt = recovery.BeginTarget(state.ProviderProfileId, SharedProviderTargetMutationKind.Publish, state);
        recovery.RecordCommit(attempt.AttemptId, new(SharedProviderChangeKind.Publication, [state.ProviderProfileId]));
        return (recovery, attempt);
    }
}
