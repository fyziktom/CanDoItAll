using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessCancellationDecisionRecoveryStepTests {
    [Fact]
    public async Task Selection_retains_one_original_generation_before_loading_or_stopping_children() {
        var fixture = new Fixture();
        fixture.Claim = fixture.Claim with { Position = new(ProcessCancellationRecoveryStage.Decisions) };
        await fixture.AdvanceAsync();
        Assert.Equal(fixture.Data.Selection.Head!.Reference, fixture.Claim.Position.CurrentDecision);
        Assert.Equal(0, fixture.Loads);
        Assert.Equal(0, fixture.Stops);
        Assert.Equal(0, fixture.Data.Commits);
    }

    [Fact]
    public async Task Ready_child_retains_exact_accounting_before_owner_commit() {
        var fixture = new Fixture();
        await fixture.AdvanceAsync();
        var intent = Assert.IsType<ProcessCancellationAccountChild>(fixture.Claim.Intent);
        Assert.Equal(fixture.Data.Selection.Child!.Original.Child.Address, intent.Request.Child);
        Assert.Equal(fixture.Data.Selection.Head!.Expectation, intent.Request.Decision);
        Assert.Equal(fixture.Data.Selection.Budget.Revision, intent.Request.Budget.Revision);
        Assert.Equal(ProcessRuntimeStepStatus.Cancelled, intent.Request.StepStatus);
        Assert.Equal(intent.Context.CommandId, intent.Request.ActionId);
        Assert.Equal(1, fixture.Stops);
        Assert.Equal(0, fixture.Data.Commits);
    }

    [Fact]
    public async Task Lost_accounting_acknowledgement_recovers_original_measured_work_without_stopping_again() {
        var fixture = new Fixture { LoseAcknowledgement = true };
        await fixture.AdvanceAsync();
        var retained = fixture.Claim;
        await Assert.ThrowsAsync<IOException>(() => fixture.AdvanceAsync());
        Assert.Equal(retained, fixture.Claim);
        var receipt = Assert.Single(fixture.Data.Saved).Value;
        Assert.Equal(fixture.Data.Closed.Work, receipt.DecisionBudget!.Change.Allocation.Settlement);
        fixture.Data.OutcomeFailure = new IOException("Original owner is now unavailable.");
        var result = await fixture.AdvanceAsync();
        Assert.Equal(new(retained.Intent!.Context.CommandId, ProcessCancellationIntentOutcome.Committed), result.Resolution);
        Assert.Equal(fixture.Data.Request.Child.ChildId, result.Position.ChildAfter);
        Assert.Equal(1, fixture.Data.Commits);
        Assert.Equal(1, fixture.Data.OutcomeReads);
        Assert.Equal(1, fixture.Stops);
    }

    [Fact]
    public async Task Empty_terminal_decision_uses_existing_finalization_owner_and_replays_lost_acknowledgement() {
        var fixture = new Fixture();
        fixture.Data.SettleChildWithoutCompletingStep();
        fixture.NoNextChild = true;
        await fixture.AdvanceAsync();
        var retained = fixture.Claim;
        Assert.IsType<ProcessCancellationFinalizeDecision>(retained.Intent);
        fixture.LoseAcknowledgement = true;
        await Assert.ThrowsAsync<IOException>(() => fixture.AdvanceAsync());
        Assert.Equal(ProcessDecisionPhase.Cancelled, fixture.Data.Selection.Head!.Phase);
        var result = await fixture.AdvanceAsync();
        Assert.Equal(fixture.Data.Selection.Head.Reference.DecisionId, result.Position.DecisionAfter);
        Assert.Null(result.Position.CurrentDecision);
        Assert.Equal(ProcessCancellationIntentOutcome.Committed, result.Resolution!.Outcome);
        Assert.Equal(1, fixture.Finalizations);
        Assert.Equal(0, fixture.Stops);
    }

    [Theory]
    [InlineData(ProcessDecisionChildStopResult.ActiveWork, ProcessCancellationRecoveryPending.ActiveWork)]
    [InlineData(ProcessDecisionChildStopResult.LeaseBusy, ProcessCancellationRecoveryPending.LeaseBusy)]
    [InlineData(ProcessDecisionChildStopResult.MissingJournal, ProcessCancellationRecoveryPending.MissingJournal)]
    [InlineData(ProcessDecisionChildStopResult.UnresolvedEffects, ProcessCancellationRecoveryPending.UnresolvedEffects)]
    [InlineData(ProcessDecisionChildStopResult.CompletionPending, ProcessCancellationRecoveryPending.CompletionPending)]
    public async Task Unresolved_child_advances_its_cursor_with_sticky_pending(ProcessDecisionChildStopResult stopped, ProcessCancellationRecoveryPending pending) {
        var fixture = new Fixture { StopResult = stopped };
        await fixture.AdvanceAsync();
        Assert.Equal(pending, fixture.Claim.Pending);
        Assert.True(fixture.Claim.Position.CycleHadPending);
        Assert.Equal(fixture.Data.Request.Child.ChildId, fixture.Claim.Position.ChildAfter);
        Assert.Null(fixture.Claim.Intent);
        Assert.Equal(0, fixture.Data.Commits);
    }

    [Fact]
    public async Task Pending_first_child_does_not_starve_the_next_original_child() {
        var fixture = new Fixture { StopResult = ProcessDecisionChildStopResult.MissingJournal };
        var original = fixture.Data.Selection;
        var first = new ProcessDecisionChildId(new Guid("00000000-0000-0000-0000-000000000001"));
        var admission = original.Child!.Original.Child with { Address = original.Child.Original.Child.Address with { ChildId = first } };
        fixture.Data.Selection = original with { Child = original.Child with {
            Original = original.Child.Original with { Child = admission }, Observation = original.Child.Observation with { Admission = admission }
        } };
        await fixture.AdvanceAsync();
        Assert.Equal(first, fixture.Claim.Position.ChildAfter);
        fixture.Data.Selection = original;
        fixture.StopResult = ProcessDecisionChildStopResult.ReadyForAccounting;
        await fixture.AdvanceAsync();
        Assert.Equal(original.Child.Original.Child.Address, Assert.IsType<ProcessCancellationAccountChild>(fixture.Claim.Intent).Request.Child);
        Assert.True(fixture.Claim.Position.CycleHadPending);
        Assert.Equal(ProcessCancellationRecoveryPending.MissingJournal, fixture.Claim.Pending);
        Assert.Equal(2, fixture.Stops);
    }

    [Fact]
    public async Task Historical_unclosed_work_does_not_relabel_the_current_child_or_disappear_after_success() {
        var fixture = new Fixture { HistoricalPending = true };
        await fixture.AdvanceAsync();
        Assert.True(fixture.Claim.Position.CycleHadPending);
        Assert.Equal(ProcessCancellationRecoveryPending.InvalidEvidence, fixture.Claim.Pending);
        await fixture.AdvanceAsync();
        Assert.True(fixture.Claim.Position.CycleHadPending);
        Assert.Equal(ProcessCancellationRecoveryPending.InvalidEvidence, fixture.Claim.Pending);
        Assert.Equal(1, fixture.Data.Commits);
    }

    [Fact]
    public async Task Missing_selected_generation_advances_as_invalid_evidence_without_side_effects() {
        var fixture = new Fixture { MissingMetadata = true };
        await fixture.AdvanceAsync();
        Assert.Null(fixture.Claim.Position.CurrentDecision);
        Assert.Equal(ProcessCancellationRecoveryPending.InvalidEvidence, fixture.Claim.Pending);
        Assert.Equal(0, fixture.Loads);
        Assert.Equal(0, fixture.Stops);
    }

    [Fact]
    public async Task A_retained_child_closure_needs_no_new_stop_or_accounting_intent() {
        var fixture = new Fixture();
        fixture.Data.SettleChildWithoutCompletingStep();
        await fixture.AdvanceAsync();
        Assert.Equal(fixture.Data.Request.Child.ChildId, fixture.Claim.Position.ChildAfter);
        Assert.Null(fixture.Claim.Intent);
        Assert.Equal(0, fixture.Stops);
        Assert.Equal(0, fixture.Data.Commits);
    }

    [Fact]
    public async Task Changed_displayed_expectations_are_definite_refusal_not_new_accounting() {
        var fixture = new Fixture();
        await fixture.AdvanceAsync();
        fixture.Data.Selection = fixture.Data.Selection with { Budget = fixture.Data.Selection.Budget with { Revision = 50 } };
        var result = await fixture.AdvanceAsync();
        Assert.Equal(ProcessCancellationIntentOutcome.RefusedWithoutCommit, result.Resolution!.Outcome);
        Assert.True(result.Position.CycleHadPending);
        Assert.Equal(0, fixture.Data.Commits);
    }

    [Fact]
    public async Task Unavailable_child_owner_yields_but_caller_cancellation_is_not_swallowed() {
        var fixture = new Fixture { StopFailure = new IOException("Unavailable original owner.") };
        await fixture.AdvanceAsync();
        Assert.Equal(ProcessCancellationRecoveryPending.UnresolvedEffects, fixture.Claim.Pending);
        fixture.Claim = fixture.Claim with { Position = fixture.Claim.Position with { ChildAfter = null } };
        fixture.StopFailure = new OperationCanceledException();
        await Assert.ThrowsAsync<OperationCanceledException>(() => fixture.AdvanceAsync());
        Assert.Equal(0, fixture.Data.Commits);
    }

    [Fact]
    public async Task A_retained_action_cannot_be_rebound_to_another_generation() {
        var fixture = new Fixture();
        await fixture.AdvanceAsync();
        var loads = fixture.Loads;
        fixture.Claim = fixture.Claim with { Position = fixture.Claim.Position with {
            CurrentDecision = fixture.Claim.Position.CurrentDecision! with { Generation = 2 }
        } };
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.AdvanceAsync());
        Assert.Equal(loads, fixture.Loads);
        Assert.Equal(0, fixture.Data.Commits);
    }

    private sealed class Fixture : IProcessCancellationDecisionReader, IProcessRootCancellationBarrierReader,
        IProcessRuntimeStateStore, IProcessDecisionResultQuery, IProcessDecisionChildStopper, IProcessRuntimeUnitOfWork {
        internal Fixture() {
            Data.State = Data.State with { Steps = Data.State.Steps.Select(step => step with { Status = ProcessRuntimeStepStatus.Cancelled }).ToArray() };
            Claim = RecoveryContractFixture.Claim() with { RootRunId = Data.State.RootRunId,
                Position = new(ProcessCancellationRecoveryStage.Decisions, CurrentDecision: Data.Selection.Head!.Reference) };
        }
        internal ProcessDecisionReviewCoordinatorFixture Data { get; } = new();
        internal ProcessCancellationRecoveryClaim Claim { get; set; }
        internal ProcessDecisionChildStopResult StopResult { get; set; }
        internal bool NoNextChild { get; set; }
        internal bool HistoricalPending { get; set; }
        internal bool MissingMetadata { get; set; }
        internal bool LoseAcknowledgement { get; set; }
        internal Exception? StopFailure { get; set; }
        internal int Loads { get; private set; }
        internal int Stops { get; private set; }
        internal int Finalizations { get; private set; }
        public ProcessDecisionExecutionContract Contract => Data.Contract;
        internal async Task<ProcessCancellationRecoveryCheckpoint> AdvanceAsync() {
            var accounting = new ProcessDecisionTerminalAccountingCoordinator(Data, this, [Data], Data, this,
                NullLogger<ProcessDecisionTerminalAccountingCoordinator>.Instance);
            var finalization = new ProcessDecisionFinalizationCoordinator(this, Data, this);
            var step = new ProcessCancellationDecisionRecoveryStep(this, this, this, this, [this], accounting, finalization,
                TimeProvider.System, NullLogger<ProcessCancellationDecisionRecoveryStep>.Instance);
            var result = await step.AdvanceAsync(Claim, default);
            result.Validate(Claim);
            Claim = Claim with { Position = result.Position, Pending = result.Pending, Intent = result.Intent };
            return result;
        }
        public Task<bool> IsStoppedAsync(ProcessRunId rootRunId, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<ProcessRuntimeStateSnapshot?> LoadAsync(ProcessRunId runId, CancellationToken cancellationToken) {
            Loads++;
            Assert.Equal(Data.State.RunId, runId);
            return Task.FromResult<ProcessRuntimeStateSnapshot?>(Data.State);
        }
        public Task<ProcessDecisionReference?> NextAsync(ProcessRunId rootRunId, ProcessDecisionId? after, CancellationToken cancellationToken)
            => Task.FromResult(after is null ? Data.Selection.Head?.Reference : null);
        public Task<ProcessCancellationDecisionSelection?> ReadAsync(ProcessRunId rootRunId, ProcessDecisionReference decision, CancellationToken cancellationToken)
            => Task.FromResult(MissingMetadata ? null : new ProcessCancellationDecisionSelection(Data.State.RunId, Data.Selection.StepInstanceId, decision, HistoricalPending));
        public Task<ProcessDecisionChildId?> NextChildAsync(ProcessRunId rootRunId, ProcessDecisionReference decision,
            ProcessDecisionChildId? after, CancellationToken cancellationToken) {
            var child = Data.Selection.Child!.Original.Child.Address.ChildId;
            return Task.FromResult(NoNextChild || after is { } previous && child.Value.CompareTo(previous.Value) <= 0 ? null : (ProcessDecisionChildId?)child);
        }
        public Task<ProcessDecisionResultSelection?> ReadAsync(ProcessRunId runId, ProcessStepInstanceId stepId,
            ProcessExecutionRunId? execution, CancellationToken cancellationToken) {
            Assert.Equal(Data.State.RunId, runId);
            return Task.FromResult<ProcessDecisionResultSelection?>(execution is null ? Data.Selection with { Child = null } : Data.Selection);
        }
        public Task<ProcessDecisionChildStopResult> StopAsync(ProcessDecisionChildMaterialBinding original, CancellationToken cancellationToken) {
            Stops++;
            Assert.Equal(Data.Selection.Child!.Original, original);
            return StopFailure is null ? Task.FromResult(StopResult) : Task.FromException<ProcessDecisionChildStopResult>(StopFailure);
        }
        public async Task<ProcessRuntimeCommitResult> CommitAsync(ProcessRuntimeCommitRequest request, CancellationToken cancellationToken) {
            ProcessRuntimeCommitResult result;
            if (request.Decision is ProcessDecisionFinalization final) {
                Finalizations++;
                var head = ProcessDecisionTransitions.FinalizeTerminal(Data.Selection.Head!, final.Expectation, final.StepStatus);
                result = ProcessRuntimeCommitResult.FromMutation(request.Mutation) with { Decision = new(final, true, head), CommittedNow = true };
                Data.Saved.Add(request.CommandId, result);
                Data.Selection = Data.Selection with { Head = head };
            } else {
                result = await Data.CommitAsync(request, cancellationToken);
                var child = Data.Selection.Child!;
                Data.Selection = Data.Selection with { Head = result.Decision!.Current, Budget = result.DecisionBudget!.Change.Balance,
                    Child = child with { Observation = child.Observation with { Generation = result.Decision.Current,
                        Closure = result.Decision.Child!.Closure, Settlement = result.DecisionBudget.Change.Allocation.Settlement } } };
            }
            Data.State = result.State;
            if (LoseAcknowledgement) {
                throw new IOException("The original decision action committed before acknowledgement.");
            }
            return result;
        }
    }
}
