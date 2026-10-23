using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;
using static CanDoItAll.Tests.Integration.Processes.ProcessCancellationRecoveryFixture;

namespace CanDoItAll.Tests.Integration.Processes;

[Trait("Category", "HostPlatform")]
public sealed class ProcessRootCancellationAdmissionPersistenceTests {
    internal static readonly ProcessRootCancellationRequester Requester = new(new(ProcessEventActorKind.User, new("cancellation-admission-test")),
        token => {
            token.ThrowIfCancellationRequested();
            return ValueTask.CompletedTask;
        });

    [Fact]
    public async Task Concurrent_native_admissions_converge_on_one_barrier_and_keep_runtime_time_monotonic() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var initial = await new EfProcessRuntimeUnitOfWork(context).CommitAsync(ProcessProjectAdmissionFixture.Initial());
        Assert.True(initial.CommittedNow);
        var original = initial.State;
        var receipts = await context.IdempotencyKeys.CountAsync();
        var loaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var arrivals = 0;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        async Task<ProcessRootCancellationAdmissionResult> AdmitAsync() {
            await using var writing = Context(app.Services);
            var owner = new EfProcessRuntimeUnitOfWork(writing);
            var states = new SelectedReadGate(owner, async () => {
                if (Interlocked.Increment(ref arrivals) == 2) {
                    loaded.TrySetResult();
                }
                await loaded.Task.WaitAsync(deadline.Token);
            });
            var service = new ProcessRootCancellationAdmissionService(states, owner,
                new EfProcessCancellationProgressReader(new Factory(Options(app.Services))), new(true), new EarlierClock(original.UpdatedAtUtc));
            return await service.AdmitAsync(original.RunId, Requester, deadline.Token);
        }
        var results = await Task.WhenAll(AdmitAsync(), AdmitAsync());
        Assert.All(results, result => Assert.Equal(ProcessRootCancellationAdmissionStatus.Accepted, result.Status));
        var barrier = Assert.Single(results.Select(result => result.BarrierCommandId).Distinct());
        Assert.NotNull(barrier);
        var recovery = await context.CancellationRecoveries.AsNoTracking().SingleAsync();
        Assert.Equal(barrier.Value.Value, recovery.BarrierCommandId);
        Assert.Equal(receipts + 1, await context.IdempotencyKeys.CountAsync());
        var state = (await new EfProcessRuntimeUnitOfWork(context).LoadAsync(original.RunId))!;
        Assert.Equal(ProcessRuntimeStatus.CancelRequested, state.Status);
        Assert.True(state.UpdatedAtUtc > original.UpdatedAtUtc);
        Assert.Equal(0, state.UpdatedAtUtc.UtcTicks % 10);
        var recorded = await context.RuntimeEvents.AsNoTracking().SingleAsync(item => item.ActorId == Requester.Actor.Id.Value);
        Assert.Equal(original.UpdatedAtUtc.AddHours(-1), recorded.OccurredAtUtc);
        Assert.Equal(nameof(ProcessEventActorKind.User), recorded.ActorKind);
    }

    [Fact]
    public async Task Lost_native_commit_acknowledgement_is_observed_without_repeating_the_original_command() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var initial = await new EfProcessRuntimeUnitOfWork(context).CommitAsync(ProcessProjectAdmissionFixture.Initial());
        Assert.True(initial.CommittedNow);
        var failure = new IOException("Injected acknowledgement loss after the native cancellation barrier commit.");
        var reader = new EfProcessCancellationProgressReader(new Factory(Options(app.Services)));
        await using (var writing = Context(app.Services, new LostAcknowledgement(failure))) {
            var owner = new EfProcessRuntimeUnitOfWork(writing);
            var service = new ProcessRootCancellationAdmissionService(owner, owner, reader, new(true), TimeProvider.System);
            Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => service.AdmitAsync(initial.State.RunId, Requester)));
        }
        var retained = await ProcessRootCancellationFixture.RetainedAsync(context, initial.State);
        var original = await context.CancellationRecoveries.AsNoTracking().SingleAsync();
        var replayOwner = new EfProcessRuntimeUnitOfWork(context);
        var replay = await new ProcessRootCancellationAdmissionService(replayOwner, replayOwner, reader, new(true), TimeProvider.System)
            .AdmitAsync(initial.State.RunId, Requester);
        Assert.Equal(ProcessRootCancellationAdmissionStatus.Accepted, replay.Status);
        Assert.Equal(original.BarrierCommandId, replay.BarrierCommandId!.Value.Value);
        Assert.Equal(retained, await ProcessRootCancellationFixture.RetainedAsync(context, initial.State));
    }

    private sealed class SelectedReadGate(IProcessRuntimeStateStore owner, Func<Task> beforeReturn) : IProcessRuntimeStateStore {
        public async Task<ProcessRuntimeStateSnapshot?> LoadAsync(ProcessRunId runId, CancellationToken cancellationToken) {
            var state = await owner.LoadAsync(runId, cancellationToken);
            await beforeReturn();
            return state;
        }
    }

    private sealed class EarlierClock(DateTimeOffset original) : TimeProvider {
        public override DateTimeOffset GetUtcNow() => original.AddHours(-1);
    }
}
