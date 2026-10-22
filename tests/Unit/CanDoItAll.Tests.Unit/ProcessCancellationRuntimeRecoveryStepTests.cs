using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessCancellationRuntimeRecoveryStepTests {
    [Fact]
    public async Task Selection_retains_one_run_without_loading_runtime_or_executions() {
        var fixture = new Fixture(selected: false);
        var result = await fixture.AdvanceAsync();
        Assert.Equal(fixture.State.RunId, result.Position.CurrentRun);
        Assert.Equal(1, fixture.Selections);
        Assert.Equal(0, fixture.Loads);
        Assert.Equal(0, fixture.ExecutionReads);
        Assert.Equal(0, fixture.Commits);
    }

    [Fact]
    public async Task Cancellation_intent_is_retained_before_any_owner_call() {
        var fixture = new Fixture();
        var result = await fixture.AdvanceAsync();
        var intent = Assert.IsType<ProcessCancellationCancelRun>(result.Intent);
        Assert.Equal(fixture.State.RunId, intent.RunId);
        Assert.Equal(ProcessCancellationRecoveryIntent.Actor, intent.Context.Actor);
        Assert.Equal(fixture.Claim.RootRunId.Value.ToString("D"), intent.Context.CorrelationId.Value);
        Assert.Equal(0, fixture.Commits);
        Assert.Equal(0, fixture.Loads);
        Assert.Equal(0, fixture.ExecutionReads);
    }

    [Fact]
    public async Task Lost_acknowledgement_recovers_exact_receipt_before_reloading_or_cancelling_again() {
        var fixture = new Fixture();
        await fixture.AdvanceAsync();
        var retained = fixture.Claim;
        fixture.LoseAcknowledgement = true;
        await Assert.ThrowsAsync<IOException>(() => fixture.AdvanceAsync());
        Assert.Equal(retained, fixture.Claim);
        Assert.Equal(1, fixture.Commits);
        Assert.Equal(1, fixture.Loads);
        var recovered = await fixture.AdvanceAsync();
        Assert.Equal(new(retained.Intent!.Context.CommandId, ProcessCancellationIntentOutcome.Committed), recovered.Resolution);
        Assert.Equal(retained.Position, recovered.Position);
        Assert.Null(recovered.Intent);
        Assert.Equal(1, fixture.Commits);
        Assert.Equal(1, fixture.Loads);
        Assert.Equal(retained.Intent.Context.CommandId, fixture.Lookup);
    }

    [Theory]
    [InlineData(ProcessRuntimeStatus.Completed)]
    [InlineData(ProcessRuntimeStatus.Failed)]
    [InlineData(ProcessRuntimeStatus.Cancelled)]
    public async Task Terminal_runs_still_inspect_original_Agent_work(ProcessRuntimeStatus status) {
        var fixture = new Fixture();
        fixture.State = fixture.State with { Status = status };
        var result = await fixture.AdvanceAsync();
        Assert.Equal(1, fixture.ExecutionReads);
        Assert.Equal(fixture.State.RunId, result.Position.RunAfter);
        Assert.Null(result.Position.CurrentRun);
        Assert.Equal(0, fixture.Commits);
        Assert.Equal(0, fixture.Loads);
    }

    [Fact]
    public async Task Root_barrier_does_not_create_another_cancel_intent_but_scans_its_Agents() {
        var fixture = new Fixture();
        fixture.State = fixture.State with { RunId = fixture.Claim.RootRunId, Status = ProcessRuntimeStatus.CancelRequested };
        fixture.Claim = fixture.Claim with { Position = fixture.Claim.Position with { CurrentRun = fixture.State.RunId } };
        await fixture.AdvanceAsync();
        Assert.Equal(1, fixture.ExecutionReads);
        Assert.Null(fixture.Claim.Intent);
        Assert.Equal(0, fixture.Commits);
    }

    [Fact]
    public async Task Pending_first_execution_advances_its_cursor_and_later_success_retains_pending_cycle() {
        var fixture = new Fixture();
        fixture.State = fixture.State with { Status = ProcessRuntimeStatus.Cancelled };
        var execution = ProcessExecutionRunId.New();
        fixture.Progress = new(execution, false, ProcessCancellationRecoveryPending.UnresolvedEffects);
        await fixture.AdvanceAsync();
        Assert.Equal(execution, fixture.Claim.Position.ExecutionAfter);
        Assert.True(fixture.Claim.Position.CycleHadPending);
        fixture.Progress = new(execution, true);
        await fixture.AdvanceAsync();
        Assert.Equal(fixture.State.RunId, fixture.Claim.Position.RunAfter);
        Assert.True(fixture.Claim.Position.CycleHadPending);
        Assert.Equal(ProcessCancellationRecoveryPending.UnresolvedEffects, fixture.Claim.Pending);
        fixture.Next = null;
        await fixture.AdvanceAsync();
        Assert.Equal(ProcessCancellationRecoveryStage.Decisions, fixture.Claim.Position.Stage);
        Assert.True(fixture.Claim.Position.CycleHadPending);
        Assert.Equal(2, fixture.ExecutionReads);
    }

    [Fact]
    public async Task Unavailable_execution_selection_advances_the_run_with_pending_evidence() {
        var fixture = new Fixture();
        fixture.State = fixture.State with { Status = ProcessRuntimeStatus.Cancelled };
        fixture.Progress = new(null, false, ProcessCancellationRecoveryPending.MissingJournal);
        await fixture.AdvanceAsync();
        Assert.Equal(fixture.State.RunId, fixture.Claim.Position.RunAfter);
        Assert.True(fixture.Claim.Position.CycleHadPending);
        Assert.Equal(ProcessCancellationRecoveryPending.MissingJournal, fixture.Claim.Pending);
    }

    [Fact]
    public async Task Resolved_transport_failure_does_not_invent_unresolved_work() {
        var fixture = new Fixture(selected: false);
        fixture.Claim = fixture.Claim with { Pending = ProcessCancellationRecoveryPending.UnresolvedEffects };
        await fixture.AdvanceAsync();
        Assert.False(fixture.Claim.Position.CycleHadPending);
        Assert.Equal(ProcessCancellationRecoveryPending.None, fixture.Claim.Pending);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Known_refusal_or_rolled_back_conflict_can_advance_but_cannot_report_quiescence(bool conflict) {
        var fixture = new Fixture();
        await fixture.AdvanceAsync();
        fixture.RefuseCommit = !conflict;
        fixture.CommitFailure = conflict ? new ProcessRuntimeOptimisticConcurrencyException(fixture.State.RunId, fixture.State.UpdatedAtUtc) : null;
        var result = await fixture.AdvanceAsync();
        Assert.Equal(ProcessCancellationIntentOutcome.RefusedWithoutCommit, result.Resolution!.Outcome);
        Assert.True(result.Position.CycleHadPending);
        Assert.Equal(fixture.State.RunId, result.Position.RunAfter);
        Assert.Null(result.Intent);
        Assert.Empty(fixture.Saved);
    }

    [Fact]
    public async Task Concurrent_terminal_transition_is_a_known_no_commit_and_still_needs_notification() {
        var fixture = new Fixture();
        await fixture.AdvanceAsync();
        fixture.State = fixture.State with { Status = ProcessRuntimeStatus.Completed };
        var resolved = await fixture.AdvanceAsync();
        Assert.Equal(ProcessCancellationIntentOutcome.RefusedWithoutCommit, resolved.Resolution!.Outcome);
        Assert.Equal(fixture.State.RunId, resolved.Position.CurrentRun);
        Assert.Equal(0, fixture.Commits);
        await fixture.AdvanceAsync();
        Assert.Equal(1, fixture.ExecutionReads);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task Rebound_receipt_cannot_clear_original_intent(int change) {
        var fixture = new Fixture { KeepReplayEvents = true };
        await fixture.AdvanceAsync();
        var retained = fixture.Claim;
        await fixture.AdvanceAsync();
        var id = retained.Intent!.Context.CommandId;
        var receipt = fixture.Saved[id];
        fixture.Saved[id] = change switch {
            0 => receipt with { State = receipt.State with { RootRunId = ProcessRunId.New() } },
            1 => receipt with { State = receipt.State with { RunId = ProcessRunId.New() } },
            2 => receipt with { State = receipt.State with { Status = ProcessRuntimeStatus.Completed } },
            3 => receipt with { Events = [receipt.Events[0] with { Actor = new(ProcessEventActorKind.System, new("another-actor")) }] },
            _ => receipt with { Events = [] }
        };
        fixture.Claim = retained;
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.AdvanceAsync());
        Assert.Equal(retained, fixture.Claim);
        Assert.Equal(1, fixture.Commits);
    }

    [Fact]
    public async Task Lost_root_barrier_refuses_before_selection_receipt_or_side_effect() {
        var fixture = new Fixture();
        fixture.Stopped = false;
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.AdvanceAsync());
        Assert.Equal(0, fixture.Selections);
        Assert.Equal(0, fixture.ExecutionReads);
        Assert.Equal(default, fixture.Lookup);
        Assert.Equal(0, fixture.Commits);
    }

    [Fact]
    public async Task Missing_selected_run_cannot_be_treated_as_finished() {
        var fixture = new Fixture();
        fixture.Missing = true;
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.AdvanceAsync());
        Assert.NotNull(fixture.Claim.Position.CurrentRun);
        Assert.Equal(0, fixture.ExecutionReads);
    }

    [Fact]
    public async Task Selected_descendant_intent_cannot_be_rebound_to_the_root() {
        var fixture = new Fixture();
        fixture.Claim = fixture.Claim with { Intent = new ProcessCancellationCancelRun(fixture.Claim.RootRunId,
            ProcessCancellationRecoveryIntent.CreateContext(fixture.Claim.RootRunId, RecoveryContractFixture.Now)) };
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.AdvanceAsync());
        Assert.Equal(default, fixture.Lookup);
        Assert.Equal(0, fixture.Commits);
    }

    [Fact]
    public async Task A_nonprogressing_execution_page_cannot_hot_loop() {
        var fixture = new Fixture();
        fixture.State = fixture.State with { Status = ProcessRuntimeStatus.Cancelled };
        fixture.Progress = new(null, false);
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.AdvanceAsync());
        Assert.False(fixture.Claim.Position.CycleHadPending);
    }

    private sealed class Fixture : IProcessCancellationRuntimeReader, IProcessRootCancellationBarrierReader,
        IProcessExecutionRecovery, IProcessRuntimeStateStore, IProcessIdempotencyStore, IProcessRuntimeUnitOfWork {
        internal Fixture(bool selected = true) {
            State = new(Claim.RootRunId, ProcessRunId.New(), new(Guid.NewGuid()), new string('a', 64), ProcessRuntimeStatus.Active,
                [], [], [], new HashSet<ArtifactSlotId>(), RecoveryContractFixture.Now);
            Next = State.RunId;
            if (selected) {
                Claim = Claim with { Position = Claim.Position with { CurrentRun = State.RunId } };
            }
        }
        internal ProcessCancellationRecoveryClaim Claim { get; set; } = RecoveryContractFixture.Claim();
        internal ProcessRuntimeStateSnapshot State { get; set; }
        internal ProcessRunId? Next { get; set; }
        internal bool Stopped { get; set; } = true;
        internal bool Missing { get; set; }
        internal bool LoseAcknowledgement { get; set; }
        internal bool RefuseCommit { get; set; }
        internal bool KeepReplayEvents { get; set; }
        internal Exception? CommitFailure { get; set; }
        internal int Loads { get; private set; }
        internal int Commits { get; private set; }
        internal int Selections { get; private set; }
        internal int ExecutionReads { get; private set; }
        internal RuntimeCommandId Lookup { get; private set; }
        internal Dictionary<RuntimeCommandId, ProcessRuntimeCommitResult> Saved { get; } = [];
        internal ProcessCancellationExecutionProgress Progress { get; set; } = new(null, true);

        internal async Task<ProcessCancellationRecoveryCheckpoint> AdvanceAsync() {
            var step = new ProcessCancellationRuntimeRecoveryStep(this, this, this, this, this, this, TimeProvider.System);
            var result = await step.AdvanceAsync(Claim, default);
            result.Validate(Claim);
            Claim = Claim with { Position = result.Position, Intent = result.Intent, Pending = result.Pending };
            return result;
        }
        public Task<ProcessRunId?> NextAsync(ProcessRunId rootRunId, ProcessRunId? after, CancellationToken cancellationToken) {
            Assert.Equal(Claim.RootRunId, rootRunId);
            Assert.Equal(Claim.Position.RunAfter, after);
            Selections++;
            return Task.FromResult(Next);
        }
        public Task<ProcessCancellationRuntimeRun?> ReadAsync(ProcessRunId rootRunId, ProcessRunId runId, CancellationToken cancellationToken)
            => Task.FromResult(Missing ? null : new ProcessCancellationRuntimeRun(State.RunId, State.Status));
        public Task<bool> IsStoppedAsync(ProcessRunId rootRunId, CancellationToken cancellationToken) => Task.FromResult(Stopped);
        public Task<ProcessCancellationExecutionProgress> InspectNextAsync(ProcessRunId rootRunId, ProcessRunId runId,
            ProcessExecutionRunId? after, CancellationToken cancellationToken) {
            Assert.Equal(Claim.RootRunId, rootRunId);
            Assert.Equal(State.RunId, runId);
            Assert.Equal(Claim.Position.ExecutionAfter, after);
            ExecutionReads++;
            return Task.FromResult(Progress);
        }
        public Task<ProcessRuntimeStateSnapshot?> LoadAsync(ProcessRunId runId, CancellationToken cancellationToken) {
            Loads++;
            return Task.FromResult<ProcessRuntimeStateSnapshot?>(Missing ? null : State);
        }
        public Task<ProcessRuntimeCommitResult?> FindCompletedCommandAsync(RuntimeCommandId commandId, CancellationToken cancellationToken) {
            Lookup = commandId;
            var saved = Saved.GetValueOrDefault(commandId);
            return Task.FromResult(saved is null || KeepReplayEvents ? saved : saved with { Events = [], OutboxMessages = [], CommittedNow = false });
        }
        public Task<ProcessRuntimeCommitResult> CommitAsync(ProcessRuntimeCommitRequest request, CancellationToken cancellationToken) {
            Commits++;
            if (CommitFailure is not null) {
                throw CommitFailure;
            }
            if (RefuseCommit) {
                return Task.FromResult(ProcessRuntimeCommitResult.FromMutation(ProcessRuntimeMutation.Rejected(State, "test-refusal", "Runtime owner refused.")));
            }
            State = request.Mutation.State;
            var result = ProcessRuntimeCommitResult.FromMutation(request.Mutation) with { CommittedNow = true };
            Saved.Add(request.CommandId, result);
            if (LoseAcknowledgement) {
                throw new IOException("Original cancellation committed before the acknowledgement was lost.");
            }
            return Task.FromResult(result);
        }
    }
}
