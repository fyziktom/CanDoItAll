using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;
using static CanDoItAll.Tests.Integration.Processes.ProcessRootCancellationFixture;
using static CanDoItAll.Tests.Integration.Processes.ProcessCancellationRecoveryFixture;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed class ProcessCancellationRecoveryAtomicTests {
    [Fact]
    public async Task First_barrier_and_direct_cancellation_enqueue_once_with_database_time_and_original_receipt() {
        await using var app = await TestApplication.CreateAsync();
        await using var scope = app.Services.CreateAsyncScope();
        Assert.IsType<EfProcessCancellationRecoveryQueue>(scope.ServiceProvider.GetRequiredService<IProcessCancellationRecoveryQueue>());
        await using var context = Context(app.Services);
        var owner = new EfProcessRuntimeUnitOfWork(context);
        foreach (var direct in new[] { false, true }) {
            var initial = ProcessProjectAdmissionFixture.Initial();
            var state = (await owner.CommitAsync(initial)).State;
            var command = Command(state);
            var engine = new ProcessRuntimeEngine(owner);
            var before = await context.Database.SqlQueryRaw<DateTimeOffset>("""SELECT clock_timestamp() AS "Value" """).SingleAsync();
            var result = direct ? await engine.RequestCancellationAsync(state, command) : await engine.BeginRootCancellationAsync(state, command);
            Assert.True(result.CommittedNow);
            var row = await context.CancellationRecoveries.AsNoTracking().SingleAsync(item => item.RootRunId == state.RunId.Value);
            Assert.Equal(command.CommandId.Value, row.BarrierCommandId);
            Assert.Equal(ProcessCancellationRecoveryOrigin.LiveBarrier, row.Origin);
            Assert.True(row.CreatedAtUtc >= before);
            Assert.NotNull(await owner.FindCompletedCommandAsync(command.CommandId));
            var retained = await RowsAsync(context);
            var replay = direct ? await engine.RequestCancellationAsync(state, command) : await engine.BeginRootCancellationAsync(state, command);
            Assert.False(replay.CommittedNow);
            Assert.Equal(retained, await RowsAsync(context));
            await owner.CommitAsync(initial);
            Assert.Equal(retained, await RowsAsync(context));
            if (!direct) {
                Assert.True((await engine.FinalizeRootCancellationAsync(result.State, Command(result.State))).CommittedNow);
                Assert.Equal(retained, await RowsAsync(context));
            }
        }
    }

    [Fact]
    public async Task Failures_before_and_after_flush_roll_back_barrier_queue_events_and_receipt_together() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var owner = new EfProcessRuntimeUnitOfWork(context);
        foreach (var after in new[] { false, true }) {
            var state = (await owner.CommitAsync(ProcessProjectAdmissionFixture.Initial())).State;
            var command = Command(state);
            var before = await RetainedAsync(context, state);
            var rows = await RowsAsync(context);
            var fault = new FlushFault(after);
            await using (var broken = Context(app.Services, fault)) {
                Assert.Same(fault.Failure, await Assert.ThrowsAsync<IOException>(() =>
                    new ProcessRuntimeEngine(new EfProcessRuntimeUnitOfWork(broken)).BeginRootCancellationAsync(state, command)));
            }
            Assert.Equal(after, fault.Flushed);
            Assert.Equal(before, await RetainedAsync(context, state));
            Assert.Equal(rows, await RowsAsync(context));
            Assert.Null(await owner.FindCompletedCommandAsync(command.CommandId));
            Assert.True((await new ProcessRuntimeEngine(owner).BeginRootCancellationAsync(state, command)).CommittedNow);
        }
    }

    [Fact]
    public async Task Lost_barrier_acknowledgement_and_actual_host_restart_preserve_queue_and_progress() {
        await using var environment = CanDoItAllTestEnvironment.Create("process-decision-generation");
        var harness = new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = environment.CreatePostgreSqlProfile("cancellation-recovery-restart") };
        ProcessRuntimeStateSnapshot state;
        RuntimeCommandContext command;
        ProcessCancellationRecoveryClaim claim;
        string[] retained;
        await using (var app = await TestApplication.CreateAsync(harness)) {
            await using var context = Context(app.Services);
            state = (await new EfProcessRuntimeUnitOfWork(context).CommitAsync(ProcessProjectAdmissionFixture.Initial())).State;
            command = Command(state);
            var failure = new IOException("Injected cancellation acknowledgement loss.");
            await using (var broken = Context(app.Services, new LostAcknowledgement(failure))) {
                Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() =>
                    new ProcessRuntimeEngine(new EfProcessRuntimeUnitOfWork(broken)).BeginRootCancellationAsync(state, command)));
            }
            var queue = Queue(app.Services);
            claim = (await queue.ClaimAsync(ProcessCancellationRecoveryOrigin.LiveBarrier, Lease))!;
            claim = (await queue.CheckpointAsync(claim, new(claim.Position, Intent(claim)))).Claim!;
            retained = await RowsAsync(context);
        }
        await using var restarted = await TestApplication.CreateAsync(harness);
        await using var reopened = Context(restarted.Services);
        var replay = await new ProcessRuntimeEngine(new EfProcessRuntimeUnitOfWork(reopened)).BeginRootCancellationAsync(state, command);
        Assert.False(replay.CommittedNow);
        Assert.Equal(ProcessRuntimeStatus.CancelRequested, replay.State.Status);
        Assert.Equal(retained, await RowsAsync(reopened));
        Assert.Equal(ProcessCancellationCheckpointStatus.Released, (await Queue(restarted.Services).CheckpointAsync(claim,
            new(claim.Position, claim.Intent, ProcessCancellationRecoveryPending.UnresolvedEffects, RetryAfter: TimeSpan.Zero))).Status);
        Assert.Equal(claim.Intent, (await Queue(restarted.Services).ClaimAsync(ProcessCancellationRecoveryOrigin.LiveBarrier, Lease))!.Intent);
    }

    [Fact]
    public async Task Refused_cancellation_and_a_conflicting_marker_cannot_partially_stop_the_root() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var state = (await StartAsync(context)).State;
        var owner = new EfProcessRuntimeUnitOfWork(context);
        var refused = await new ProcessRuntimeEngine(owner).RequestCancellationAsync(state, Command(state));
        Assert.False(refused.Succeeded);
        Assert.Empty(await context.CancellationRecoveries.ToArrayAsync());
        context.CancellationRecoveries.Add(new() {
            RootRunId = state.RootRunId.Value, Origin = ProcessCancellationRecoveryOrigin.LiveBarrier, BarrierCommandId = Guid.NewGuid(),
            PositionJson = ProcessCancellationRecoveryCodec.WritePosition(ProcessCancellationRecoveryPosition.Initial)
        });
        await context.SaveChangesAsync();
        var before = await RetainedAsync(context, state);
        var rows = await RowsAsync(context);
        var command = Command(state);
        await Assert.ThrowsAsync<DbUpdateException>(() => new ProcessRuntimeEngine(owner).BeginRootCancellationAsync(state, command));
        Assert.Equal(before, await RetainedAsync(context, state));
        Assert.Equal(rows, await RowsAsync(context));
        Assert.Null(await owner.FindCompletedCommandAsync(command.CommandId));
    }
}
