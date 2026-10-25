using System.Text.Json;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed class ProcessCancellationBarrierPersistenceTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Direct_commits_cannot_escape_pending_or_finalized_cancellation(bool finalize) {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var owner = new EfProcessRuntimeUnitOfWork(context);
        var engine = new ProcessRuntimeEngine(owner);
        var active = await ActiveAsync(owner);
        var stopped = await engine.BeginRootCancellationAsync(active, Command(active));
        if (finalize) {
            stopped = await engine.FinalizeRootCancellationAsync(stopped.State, Command(stopped.State));
        }
        var expected = JsonSerializer.Serialize(stopped.State);
        var before = await CountsAsync(context);
        foreach (var status in Enum.GetValues<ProcessRuntimeStatus>().Where(status => status != stopped.State.Status &&
                (finalize || status != ProcessRuntimeStatus.Cancelled))) {
            var mutation = new ProcessRuntimeMutation(ProcessRuntimeTransitionOutcome.Applied,
                stopped.State with { Status = status, UpdatedAtUtc = stopped.State.UpdatedAtUtc.AddSeconds(1) }, [], [], [], []);
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => owner.CommitAsync(new(RuntimeCommandId.New(), stopped.State, mutation)));
            Assert.Contains("persisted cancellation barrier", error.Message, StringComparison.Ordinal);
            Assert.Equal(expected, JsonSerializer.Serialize(await owner.LoadAsync(stopped.State.RunId)));
            Assert.Equal(before, await CountsAsync(context));
        }
        var unchanged = stopped.State with { UpdatedAtUtc = stopped.State.UpdatedAtUtc.AddSeconds(1) };
        Assert.True((await owner.CommitAsync(new(RuntimeCommandId.New(), stopped.State,
            new(ProcessRuntimeTransitionOutcome.Applied, unchanged, [], [], [], [])))).CommittedNow);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancellation_and_rework_ordering_rejects_the_stale_command_and_preserves_a_fresh_barrier(bool cancellationFirst) {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var owner = new EfProcessRuntimeUnitOfWork(context);
        var active = await ActiveAsync(owner);
        var capture = new RecordingOwner();
        var engine = new ProcessRuntimeEngine(capture);
        await engine.BeginRootCancellationAsync(active, Command(active));
        await engine.RequestStepReworkAsync(active, Command(active), new(active.Steps.Single().StepInstanceId, "Retry failed work."));
        Assert.Equal(2, capture.Requests.Count);
        var first = capture.Requests[cancellationFirst ? 0 : 1];
        var second = capture.Requests[cancellationFirst ? 1 : 0];
        var committed = await owner.CommitAsync(first);
        var before = await CountsAsync(context);
        await Assert.ThrowsAsync<ProcessRuntimeOptimisticConcurrencyException>(() => owner.CommitAsync(second));
        Assert.Equal(before, await CountsAsync(context));
        var current = (await owner.LoadAsync(active.RunId))!;
        Assert.Equal(JsonSerializer.Serialize(committed.State), JsonSerializer.Serialize(current));
        engine = new ProcessRuntimeEngine(owner);
        if (!cancellationFirst) {
            current = (await engine.BeginRootCancellationAsync(current, Command(current))).State;
        }
        Assert.Equal(ProcessRuntimeStatus.CancelRequested, current.Status);
        var refused = await engine.RequestStepReworkAsync(current, Command(current), new(current.Steps.Single().StepInstanceId, "Retry after cancellation."));
        Assert.False(refused.Succeeded);
        Assert.Contains(refused.Diagnostics, diagnostic => diagnostic.Code == "Runtime.CancellationInProgress");
        Assert.Equal(ProcessRuntimeStatus.CancelRequested, (await owner.LoadAsync(active.RunId))!.Status);
    }

    [Fact]
    public async Task A_completed_command_replays_with_current_cancellation_state_without_reopening() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var owner = new EfProcessRuntimeUnitOfWork(context);
        var initial = ProcessProjectAdmissionFixture.Initial();
        var created = await owner.CommitAsync(initial);
        var stopped = await new ProcessRuntimeEngine(owner).BeginRootCancellationAsync(created.State, Command(created.State));
        var before = await CountsAsync(context);
        var replay = await owner.CommitAsync(initial);
        Assert.False(replay.CommittedNow);
        Assert.Equal(ProcessRuntimeStatus.CancelRequested, replay.State.Status);
        Assert.Equal(JsonSerializer.Serialize(stopped.State), JsonSerializer.Serialize(replay.State));
        Assert.Equal(before, await CountsAsync(context));
    }

    private static async Task<ProcessRuntimeStateSnapshot> ActiveAsync(EfProcessRuntimeUnitOfWork owner) {
        var created = await owner.CommitAsync(ProcessProjectAdmissionFixture.Initial());
        var state = created.State with {
            Status = ProcessRuntimeStatus.Active, UpdatedAtUtc = created.State.UpdatedAtUtc.AddSeconds(1),
            Steps = created.State.Steps.Select(step => step with { Status = ProcessRuntimeStepStatus.Failed }).ToArray()
        };
        return (await owner.CommitAsync(new(RuntimeCommandId.New(), created.State,
            new(ProcessRuntimeTransitionOutcome.Applied, state, [], [], [], [])))).State;
    }

    private static RuntimeCommandContext Command(ProcessRuntimeStateSnapshot state)
        => new(RuntimeCommandId.New(), new(ProcessEventActorKind.System, new("cancellation-barrier-fixture")),
            new("cancellation-barrier-fixture"), state.UpdatedAtUtc.AddSeconds(1));

    private static async Task<(int Events, int Outbox, int Receipts)> CountsAsync(ProcessPersistenceDbContext context)
        => (await context.RuntimeEvents.CountAsync(), await context.OutboxMessages.CountAsync(), await context.IdempotencyKeys.CountAsync());

    private sealed class RecordingOwner : IProcessRuntimeUnitOfWork {
        internal List<ProcessRuntimeCommitRequest> Requests { get; } = [];

        public Task<ProcessRuntimeCommitResult> CommitAsync(ProcessRuntimeCommitRequest request, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            return Task.FromResult(ProcessRuntimeCommitResult.FromMutation(request.Mutation) with { CommittedNow = true });
        }
    }
}
