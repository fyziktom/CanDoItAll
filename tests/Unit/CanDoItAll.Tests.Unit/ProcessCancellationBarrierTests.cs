using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit.Processes;

public sealed class ProcessCancellationBarrierTests {
    [Fact]
    public async Task Failed_step_rework_cannot_reactivate_a_cancelling_root() {
        var owner = new RecordingOwner();
        var engine = new ProcessRuntimeEngine(owner);
        var active = Active();
        var cancelled = await engine.BeginRootCancellationAsync(active, Context(active));
        Assert.Equal(ProcessRuntimeStatus.CancelRequested, cancelled.State.Status);
        Assert.Equal(ProcessRuntimeStepStatus.Failed, cancelled.State.Steps[0].Status);
        var result = await engine.RequestStepReworkAsync(cancelled.State, Context(cancelled.State),
            new(cancelled.State.Steps[0].StepInstanceId, "Retry the earlier failed step."));
        Assert.False(result.Succeeded);
        Assert.Equal(ProcessRuntimeStatus.CancelRequested, result.State.Status);
        Assert.Single(owner.Requests);
    }

    [Fact]
    public async Task Failed_run_without_cancellation_can_still_retry() {
        var owner = new RecordingOwner();
        var state = Active() with { Status = ProcessRuntimeStatus.Failed };
        var result = await new ProcessRuntimeEngine(owner).RequestStepReworkAsync(state, Context(state),
            new(state.Steps[0].StepInstanceId, "Retry an ordinary failed run."));
        Assert.True(result.Succeeded);
        Assert.Equal(ProcessRuntimeStatus.Active, result.State.Status);
        Assert.Equal(ProcessRuntimeStepStatus.Ready, result.State.Steps[0].Status);
        Assert.Single(owner.Requests);
    }

    [Fact]
    public async Task Repeated_begin_and_finalization_preserve_the_barrier_and_original_failed_step() {
        var owner = new RecordingOwner();
        var engine = new ProcessRuntimeEngine(owner);
        var active = Active();
        var begun = await engine.BeginRootCancellationAsync(active, Context(active));
        var repeated = await engine.BeginRootCancellationAsync(begun.State, Context(begun.State));
        Assert.Equal(ProcessRuntimeTransitionOutcome.Duplicate, repeated.Outcome);
        Assert.Equal(begun.State, repeated.State);
        Assert.Single(owner.Requests);
        var finalized = await engine.FinalizeRootCancellationAsync(repeated.State, Context(repeated.State));
        Assert.True(finalized.Succeeded);
        Assert.Equal(ProcessRuntimeStatus.Cancelled, finalized.State.Status);
        Assert.Equal(ProcessRuntimeStepStatus.Failed, finalized.State.Steps[0].Status);
        Assert.Equal(ProcessRuntimeStepStatus.Cancelled, finalized.State.Steps[1].Status);
        Assert.Equal(2, owner.Requests.Count);
    }

    private static ProcessRuntimeStateSnapshot Active() {
        var state = ProcessProjectAdmissionFixture.Initial().Mutation.State;
        var failed = state.Steps[0] with { Status = ProcessRuntimeStepStatus.Failed };
        var pending = failed with { StepInstanceId = ProcessStepInstanceId.New(), StepDefinitionId = ProcessStepDefinitionId.New(), Status = ProcessRuntimeStepStatus.Ready };
        return state with { Status = ProcessRuntimeStatus.Active, Steps = [failed, pending] };
    }

    private static RuntimeCommandContext Context(ProcessRuntimeStateSnapshot state)
        => new(RuntimeCommandId.New(), new(ProcessEventActorKind.System, new("cancellation-barrier-fixture")),
            new("cancellation-barrier-fixture"), state.UpdatedAtUtc.AddSeconds(1));

    private sealed class RecordingOwner : IProcessRuntimeUnitOfWork {
        internal List<ProcessRuntimeCommitRequest> Requests { get; } = [];

        public Task<ProcessRuntimeCommitResult> CommitAsync(ProcessRuntimeCommitRequest request, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            return Task.FromResult(ProcessRuntimeCommitResult.FromMutation(request.Mutation) with { CommittedNow = true });
        }
    }
}
