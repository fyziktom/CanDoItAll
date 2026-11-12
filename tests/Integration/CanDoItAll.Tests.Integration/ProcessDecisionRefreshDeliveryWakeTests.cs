using System.Data.Common;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;
using static CanDoItAll.Tests.Integration.ProcessDecisionRefreshJournalFixture;

namespace CanDoItAll.Tests.Integration;

[Trait("Category", "FileSystemPortability")]
public sealed class ProcessDecisionRefreshDeliveryWakeTests {
    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot, false, false)]
    [InlineData(ReadBasisMode.RunSnapshot, true, false)]
    [InlineData(ReadBasisMode.StepSnapshot, false, false)]
    [InlineData(ReadBasisMode.StepSnapshot, true, false)]
    [InlineData(ReadBasisMode.RunSnapshot, false, true)]
    [InlineData(ReadBasisMode.RunSnapshot, true, true)]
    [InlineData(ReadBasisMode.StepSnapshot, false, true)]
    [InlineData(ReadBasisMode.StepSnapshot, true, true)]
    public async Task Original_return_cannot_be_lost_before_or_after_parking_even_after_cancellation(ReadBasisMode mode, bool parkFirst, bool cancel) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(mode, freezeClock: false);
        var (admission, state) = await ProcessDecisionRefreshJournalFixture.StartAsync(fixture);
        var queue = fixture.Services.GetRequiredService<IProcessDecisionRefreshDeliveryQueue>();
        var claim = (await queue.ClaimAsync(TimeSpan.FromMinutes(1)))!;
        var store = fixture.Services.GetRequiredService<IProcessDecisionRefreshCaptureStore>();
        ReturnGate gate = new(store);
        CaptureProbe capturer = new(fixture.Services);
        var capture = Coordinator(fixture.Services, capturer, gate).PrepareAsync(state, fixture.CommandContext, admission);
        try {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var invoking = await SavedAsync(fixture.Services, state, admission, mode);
            Assert.Equal(ProcessDecisionRefreshCapturePhase.Invoking, invoking.Phase);
            var before = await SnapshotAsync(fixture.Services, state);
            if (parkFirst) {
                await ParkAsync();
                Assert.Equal(before, await SnapshotAsync(fixture.Services, state));
                Assert.Null(await queue.ClaimAsync(TimeSpan.FromMinutes(1)));
            }
            if (cancel) {
                await CancelAsync(fixture.Services, state, fixture.CommandContext);
            }
            gate.Resume.TrySetResult();
            await Assert.ThrowsAsync<IOException>(() => capture.WaitAsync(TimeSpan.FromSeconds(10)));
            await using var context = Context(fixture.Services);
            var woken = await context.RefreshDeliveries.AsNoTracking().SingleAsync();
            Assert.Equal(1, woken.WakeRevision);
            Assert.Equal(ProcessDecisionRefreshDeliveryDisposition.Ready, woken.Disposition);
            if (!parkFirst) {
                Assert.Equal(claim.LeaseToken, woken.LeaseToken);
                Assert.Equal(claim.LeaseExpiresAtUtc, woken.DueAtUtc);
                Assert.Null(await queue.ClaimAsync(TimeSpan.FromMinutes(1)));
                await ParkAsync();
            }
            var ready = await context.RefreshDeliveries.AsNoTracking().SingleAsync();
            Assert.Equal(ProcessDecisionRefreshDeliveryDisposition.Ready, ready.Disposition);
            Assert.Equal(ProcessDecisionRefreshDeliveryReason.Admitted, ready.Reason);
            Assert.Null(ready.LeaseToken);
            var returned = await SavedAsync(fixture.Services, state, admission, mode);
            var outcome = (await store.ReadReturnedAsync(state.RunId, returned.Reservation.ReservationId))!;
            var savedDelivery = await ProcessDecisionRefreshDeliveryQueueTests.RowsAsync(context);
            await store.ReturnAsync(returned.Reservation, returned.Invocation!.Value, outcome);
            Assert.Equal(savedDelivery, await ProcessDecisionRefreshDeliveryQueueTests.RowsAsync(context));
            await using var restarted = fixture.Services.CreateAsyncScope();
            var nextQueue = restarted.ServiceProvider.GetRequiredService<IProcessDecisionRefreshDeliveryQueue>();
            var next = (await nextQueue.ClaimAsync(TimeSpan.FromMinutes(1)))!;
            Assert.Equal(claim.AdmissionId, next.AdmissionId);
            Assert.Equal(claim.LeaseGeneration + 1, next.LeaseGeneration);
            var result = await restarted.ServiceProvider.GetRequiredService<ProcessDecisionRefreshDeliveryCoordinator>().AdvanceAsync(next.RunId, next.AdmissionId);
            if (cancel) {
                Assert.IsType<ProcessDecisionRefreshDeliveryResult.Stopped>(result);
            } else {
                Assert.IsType<ProcessDecisionRefreshDeliveryResult.Advanced>(result);
            }
            Assert.Equal(ProcessDecisionRefreshDeliveryCheckpointStatus.Released, await nextQueue.CheckpointAsync(next,
                new(cancel ? ProcessDecisionRefreshDeliveryReason.Stopped : ProcessDecisionRefreshDeliveryReason.Advanced)));
            Assert.Null(await nextQueue.ClaimAsync(TimeSpan.FromMinutes(1)));
            Assert.Equal(1, capturer.Calls);
            Assert.All(await context.DecisionBudgets.AsNoTracking().ToArrayAsync(), row => Assert.Equal(0, row.ReservedWorkTicks));
            Assert.Equal(returned.Outcome, (await SavedAsync(fixture.Services, state, admission, mode)).Outcome);
            Assert.Equal(0, await context.RefreshCapturePayloads.CountAsync());
            await fixture.Entry.Files.Sql.VerifyAsync(0, 0);
        } finally {
            gate.Resume.TrySetResult();
            await Record.ExceptionAsync(() => capture.WaitAsync(TimeSpan.FromSeconds(10)));
        }

        async Task ParkAsync() {
            Assert.Equal(ProcessDecisionRefreshDeliveryCheckpointStatus.Released, await queue.CheckpointAsync(claim,
                new(ProcessDecisionRefreshDeliveryReason.UnknownInvocation)));
        }
    }

    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot, false)]
    [InlineData(ReadBasisMode.StepSnapshot, false)]
    [InlineData(ReadBasisMode.RunSnapshot, true)]
    [InlineData(ReadBasisMode.StepSnapshot, true)]
    public async Task Never_invoked_closure_wakes_pending_delivery_but_preserves_explicit_review(ReadBasisMode mode, bool review) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(mode, freezeClock: false);
        var (admission, state) = await ProcessDecisionRefreshJournalFixture.StartAsync(fixture);
        var store = fixture.Services.GetRequiredService<IProcessDecisionRefreshCaptureStore>();
        await Assert.ThrowsAsync<IOException>(() => Coordinator(fixture.Services, new(fixture.Services), new InterruptedStore(store, Interruption.BeforeBegin))
            .PrepareAsync(state, fixture.CommandContext, admission));
        var queue = fixture.Services.GetRequiredService<IProcessDecisionRefreshDeliveryQueue>();
        var claim = (await queue.ClaimAsync(TimeSpan.FromMinutes(1)))!;
        await queue.CheckpointAsync(claim, review ? new(ProcessDecisionRefreshDeliveryReason.SourceProjectionRequired) :
            new(ProcessDecisionRefreshDeliveryReason.PendingCapture, TimeSpan.FromHours(1)));
        var saved = await SavedAsync(fixture.Services, state, admission, mode);
        await using var context = Context(fixture.Services);
        var before = await ProcessDecisionRefreshDeliveryQueueTests.RowsAsync(context);
        await CancelAsync(fixture.Services, state, fixture.CommandContext);
        await store.CloseNeverInvokedAsync(saved.Reservation);
        if (review) {
            Assert.Equal(before, await ProcessDecisionRefreshDeliveryQueueTests.RowsAsync(context));
            Assert.Null(await queue.ClaimAsync(TimeSpan.FromMinutes(1)));
        } else {
            var next = (await queue.ClaimAsync(TimeSpan.FromMinutes(1)))!;
            Assert.Equal(claim.WakeRevision + 1, next.WakeRevision);
            Assert.Equal(claim.AdmissionId, next.AdmissionId);
        }
        Assert.Equal(ProcessDecisionRefreshCaptureOutcomeKind.NeverInvoked, (await SavedAsync(fixture.Services, state, admission, mode)).Outcome!.Kind);
    }

    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public async Task Return_waiting_on_a_checkpoint_lock_wakes_the_committed_parked_row(ReadBasisMode mode) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(mode, freezeClock: false);
        var (admission, state) = await ProcessDecisionRefreshJournalFixture.StartAsync(fixture);
        TaskCompletionSource checkpointEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseCheckpoint = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource wakeEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var queue = ProcessDecisionRefreshDeliveryQueueTests.Queue(fixture.Services, new WriteProbe(async () => {
            checkpointEntered.TrySetResult();
            await releaseCheckpoint.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }));
        var claim = (await queue.ClaimAsync(TimeSpan.FromMinutes(1)))!;
        var store = new EfProcessDecisionRefreshCaptureStore(new Processes.ProcessCancellationRecoveryFixture.Factory(Options(fixture.Services,
            new WriteProbe(() => {
                wakeEntered.TrySetResult();
                return Task.CompletedTask;
            }))), fixture.Services.GetRequiredService<ICanonicalRuntimeDatabase>());
        ReturnGate gate = new(store);
        var capture = Coordinator(fixture.Services, new(fixture.Services), gate).PrepareAsync(state, fixture.CommandContext, admission);
        Task<ProcessDecisionRefreshDeliveryCheckpointStatus>? checkpoint = null;
        try {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            checkpoint = queue.CheckpointAsync(claim, new(ProcessDecisionRefreshDeliveryReason.UnknownInvocation));
            await checkpointEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            gate.Resume.TrySetResult();
            await wakeEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            releaseCheckpoint.TrySetResult();
            Assert.Equal(ProcessDecisionRefreshDeliveryCheckpointStatus.Released, await checkpoint.WaitAsync(TimeSpan.FromSeconds(5)));
            await Assert.ThrowsAsync<IOException>(() => capture.WaitAsync(TimeSpan.FromSeconds(5)));
            await using var context = Context(fixture.Services);
            var row = await context.RefreshDeliveries.AsNoTracking().SingleAsync();
            Assert.Equal(ProcessDecisionRefreshDeliveryDisposition.Ready, row.Disposition);
            Assert.Equal(1, row.ProgressRevision);
            Assert.Equal(1, row.WakeRevision);
            Assert.Null(row.LeaseToken);
            Assert.NotNull(await queue.ClaimAsync(TimeSpan.FromMinutes(1)));
            Assert.Equal(ProcessDecisionRefreshCapturePhase.Returned, (await SavedAsync(fixture.Services, state, admission, mode)).Phase);
        } finally {
            releaseCheckpoint.TrySetResult();
            gate.Resume.TrySetResult();
            if (checkpoint is not null) {
                await Record.ExceptionAsync(() => checkpoint.WaitAsync(TimeSpan.FromSeconds(10)));
            }
            await Record.ExceptionAsync(() => capture.WaitAsync(TimeSpan.FromSeconds(10)));
        }
    }

    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public async Task Failure_after_wake_rolls_back_return_payload_and_wake_together(ReadBasisMode mode) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(mode, freezeClock: false);
        var (admission, state) = await ProcessDecisionRefreshJournalFixture.StartAsync(fixture);
        WakeWriteFault fault = new();
        var failedStore = new EfProcessDecisionRefreshCaptureStore(new Processes.ProcessCancellationRecoveryFixture.Factory(Options(fixture.Services, fault)),
            fixture.Services.GetRequiredService<ICanonicalRuntimeDatabase>());
        ReturnGate gate = new(failedStore);
        CaptureProbe capturer = new(fixture.Services);
        var capture = Coordinator(fixture.Services, capturer, gate).PrepareAsync(state, fixture.CommandContext, admission);
        try {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var queue = fixture.Services.GetRequiredService<IProcessDecisionRefreshDeliveryQueue>();
            var claim = (await queue.ClaimAsync(TimeSpan.FromMinutes(1)))!;
            await queue.CheckpointAsync(claim, new(ProcessDecisionRefreshDeliveryReason.UnknownInvocation));
            await using var context = Context(fixture.Services);
            var before = await ProcessDecisionRefreshDeliveryQueueTests.RowsAsync(context);
            var journalBefore = await SnapshotAsync(fixture.Services, state);
            gate.Resume.TrySetResult();
            Assert.Same(fault.Failure, await Assert.ThrowsAsync<IOException>(() => capture.WaitAsync(TimeSpan.FromSeconds(10))));
            Assert.Equal(1, fault.Affected);
            Assert.Equal(before, await ProcessDecisionRefreshDeliveryQueueTests.RowsAsync(context));
            Assert.Equal(journalBefore, await SnapshotAsync(fixture.Services, state));
            Assert.Null(await queue.ClaimAsync(TimeSpan.FromMinutes(1)));
            var saved = await SavedAsync(fixture.Services, state, admission, mode);
            await fixture.Services.GetRequiredService<IProcessDecisionRefreshCaptureStore>().ReturnAsync(saved.Reservation, saved.Invocation!.Value, gate.Returned!);
            Assert.NotNull(await queue.ClaimAsync(TimeSpan.FromMinutes(1)));
            Assert.Equal(1, capturer.Calls);
            Assert.Equal(gate.Returned!.Outcome, (await SavedAsync(fixture.Services, state, admission, mode)).Outcome);
        } finally {
            gate.Resume.TrySetResult();
            await Record.ExceptionAsync(() => capture.WaitAsync(TimeSpan.FromSeconds(10)));
        }
    }

    private sealed class WakeWriteFault : DbCommandInterceptor {
        internal IOException Failure { get; } = new("Injected failure after delivery wake in the original Return transaction.");
        internal int Affected { get; private set; }
        public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result,
            CancellationToken cancellationToken = default) {
            if (command.CommandText.Contains("UPDATE process_decision_refresh_delivery SET", StringComparison.Ordinal)) {
                Affected = result;
                throw Failure;
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class WriteProbe(Func<Task> before) : DbCommandInterceptor {
        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            if (command.CommandText.Contains("UPDATE process_decision_refresh_delivery SET", StringComparison.Ordinal)) {
                await before();
            }
            return result;
        }
    }

    private sealed class ReturnGate(IProcessDecisionRefreshCaptureStore inner) : IProcessDecisionRefreshCaptureStore {
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Resume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal ProcessDecisionRefreshCaptureReturned? Returned { get; private set; }
        public Task<ProcessDecisionRefreshCaptureState?> ReadAsync(ProcessRunId run, RuntimeCommandId reservation, CancellationToken cancellationToken = default)
            => inner.ReadAsync(run, reservation, cancellationToken);
        public Task<ProcessDecisionRefreshCaptureReturned?> ReadReturnedAsync(ProcessRunId run, RuntimeCommandId reservation, CancellationToken cancellationToken = default)
            => inner.ReadReturnedAsync(run, reservation, cancellationToken);
        public Task<ProcessDecisionRefreshCaptureInvocationResult> BeginAsync(ProcessDecisionRefreshCaptureReservation reservation,
            ProcessDecisionCaptureInvocationId invocation, CancellationToken cancellationToken = default)
            => inner.BeginAsync(reservation, invocation, cancellationToken);
        public async Task<ProcessDecisionRefreshCaptureState> ReturnAsync(ProcessDecisionRefreshCaptureReservation reservation,
            ProcessDecisionCaptureInvocationId invocation, ProcessDecisionRefreshCaptureReturned returned, CancellationToken cancellationToken = default) {
            Returned = returned;
            Entered.TrySetResult();
            await Resume.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            await inner.ReturnAsync(reservation, invocation, returned, cancellationToken);
            throw new IOException("Injected acknowledgement loss after original capture return.");
        }
        public Task<ProcessDecisionRefreshCaptureState> CloseNeverInvokedAsync(ProcessDecisionRefreshCaptureReservation reservation, CancellationToken cancellationToken = default)
            => inner.CloseNeverInvokedAsync(reservation, cancellationToken);
    }
}
