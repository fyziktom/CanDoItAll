using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessCancellationScopeRecoveryStepTests {
    [Fact]
    public async Task Active_scope_retains_reconciliation_then_cancel_as_separate_original_actions() {
        var fixture = new Fixture(ProcessDecisionRunScopePhase.Active);
        await fixture.AdvanceAsync();
        var reconcile = Assert.IsType<ProcessCancellationChangeScope>(fixture.Claim.Intent);
        Assert.Equal(ProcessDecisionRunScopeCommandKind.BeginReconciliation, reconcile.Command.Kind);
        Assert.Equal(0, fixture.Commits);
        await fixture.AdvanceAsync();
        Assert.Equal(ProcessCancellationRecoveryStage.SharedScope, fixture.Claim.Position.Stage);
        Assert.Equal(ProcessDecisionRunScopePhase.Reconciling, fixture.Selection!.Scope.Phase);
        await fixture.AdvanceAsync();
        var cancel = Assert.IsType<ProcessCancellationChangeScope>(fixture.Claim.Intent);
        Assert.Equal(ProcessDecisionRunScopeCommandKind.Cancel, cancel.Command.Kind);
        Assert.NotEqual(reconcile.Context.CommandId, cancel.Context.CommandId);
        await fixture.AdvanceAsync();
        Assert.Equal(ProcessCancellationRecoveryStage.Quiescence, fixture.Claim.Position.Stage);
        Assert.Equal(ProcessDecisionRunScopePhase.Cancelled, fixture.Selection.Scope.Phase);
        Assert.Equal(2, fixture.Commits);
    }

    [Fact]
    public async Task Lost_scope_acknowledgement_uses_exact_receipt_before_current_selection() {
        var fixture = new Fixture();
        await fixture.AdvanceAsync();
        var retained = fixture.Claim;
        fixture.LoseAcknowledgement = true;
        await Assert.ThrowsAsync<IOException>(() => fixture.AdvanceAsync());
        Assert.Equal(retained, fixture.Claim);
        fixture.SelectionFailure = new InvalidDataException("Current projection is unavailable.");
        var result = await fixture.AdvanceAsync();
        Assert.Equal(new(retained.Intent!.Context.CommandId, ProcessCancellationIntentOutcome.Committed), result.Resolution);
        Assert.Equal(ProcessCancellationRecoveryStage.Quiescence, result.Position.Stage);
        Assert.Equal(1, fixture.Commits);
        Assert.Equal(1, fixture.Reads);
        Assert.Equal(2, fixture.Loads);
    }

    [Fact]
    public async Task Interrupted_capture_remains_pending_without_manufacturing_abort_work() {
        var fixture = new Fixture(ProcessDecisionRunScopePhase.Capturing);
        var before = fixture.Selection;
        var result = await fixture.AdvanceAsync();
        Assert.Equal(ProcessCancellationRecoveryPending.CapturePending, result.Pending);
        Assert.True(result.Position.CycleHadPending);
        Assert.Equal(ProcessCancellationRecoveryStage.Quiescence, result.Position.Stage);
        Assert.Null(result.Intent);
        Assert.Equal(before, fixture.Selection);
        Assert.Equal(0, fixture.Commits);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task No_scope_or_already_closed_scope_preserves_earlier_pending_evidence(bool closed) {
        var fixture = new Fixture(ProcessDecisionRunScopePhase.Cancelled);
        if (!closed) {
            fixture.Selection = null;
        }
        fixture.Claim = fixture.Claim with { Position = fixture.Claim.Position with { CycleHadPending = true }, Pending = ProcessCancellationRecoveryPending.MissingJournal };
        await fixture.AdvanceAsync();
        Assert.True(fixture.Claim.Position.CycleHadPending);
        Assert.Equal(ProcessCancellationRecoveryPending.MissingJournal, fixture.Claim.Pending);
        Assert.Equal(0, fixture.Commits);
    }

    [Theory]
    [InlineData(ProcessDecisionRunScopeRefusal.StaleExpectation)]
    [InlineData(ProcessDecisionRunScopeRefusal.UnclosedWork)]
    public async Task Known_scope_refusal_yields_the_cycle_with_original_resolution(ProcessDecisionRunScopeRefusal reason) {
        var fixture = new Fixture();
        await fixture.AdvanceAsync();
        var action = fixture.Claim.Intent!.Context.CommandId;
        fixture.CommitFailure = new ProcessDecisionRunScopeException(reason);
        var result = await fixture.AdvanceAsync();
        Assert.Equal(new(action, ProcessCancellationIntentOutcome.RefusedWithoutCommit), result.Resolution);
        Assert.Equal(ProcessCancellationRecoveryStage.Quiescence, result.Position.Stage);
        Assert.True(result.Position.CycleHadPending);
        Assert.Empty(fixture.Saved);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Corrupt_original_scope_receipts_cannot_advance(int change) {
        var fixture = new Fixture();
        await fixture.AdvanceAsync();
        var retained = fixture.Claim;
        await fixture.AdvanceAsync();
        var action = retained.Intent!.Context.CommandId;
        var receipt = fixture.Saved[action];
        fixture.Saved[action] = change switch {
            0 => receipt with { State = receipt.State with { RootRunId = ProcessRunId.New() } },
            1 => receipt with { DecisionRunScope = receipt.DecisionRunScope! with {
                Command = receipt.DecisionRunScope.Command with { Expectation = receipt.DecisionRunScope.Command.Expectation with { Revision = 999 } }
            } },
            _ => receipt with { DecisionRunScope = receipt.DecisionRunScope! with { State = receipt.DecisionRunScope.State with { Phase = ProcessDecisionRunScopePhase.Completed } } }
        };
        fixture.Claim = retained;
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.AdvanceAsync());
        Assert.Equal(retained, fixture.Claim);
        Assert.Equal(1, fixture.Commits);
    }

    [Fact]
    public async Task Legacy_missing_source_projection_is_explicit_pending() {
        var fixture = new Fixture { SelectionFailure = new ProcessDecisionSourceProjectionRequiredException() };
        await fixture.AdvanceAsync();
        Assert.Equal(ProcessCancellationRecoveryPending.InvalidEvidence, fixture.Claim.Pending);
        Assert.True(fixture.Claim.Position.CycleHadPending);
        Assert.Equal(0, fixture.Commits);
    }

    private sealed class Fixture : IProcessDecisionRootQuery, IProcessRootCancellationBarrierReader,
        IProcessRuntimeStateStore, IProcessIdempotencyStore, IProcessRuntimeUnitOfWork {
        internal Fixture(ProcessDecisionRunScopePhase phase = ProcessDecisionRunScopePhase.AwaitingCapture) {
            Claim = RecoveryContractFixture.Claim() with { Position = new(ProcessCancellationRecoveryStage.SharedScope) };
            State = new(Claim.RootRunId, Claim.RootRunId, new(Guid.NewGuid()), new string('a', 64), ProcessRuntimeStatus.CancelRequested,
                [], [], [], new HashSet<ArtifactSlotId>(), RecoveryContractFixture.Now);
            var root = ProcessDecisionRunPolicyFixture.WithSharedScope(ProcessDecisionRunPolicyFixture.Root(State.RunId,
                ProcessDefinitionVersionId.New(), Guid.NewGuid(), ReadBasisMode.RunSnapshot, OnBasisChange.PauseForReview));
            var scope = ProcessDecisionRunScopeTransitions.Create(root.MaterialScope!);
            if (phase is ProcessDecisionRunScopePhase.Active or ProcessDecisionRunScopePhase.Reconciling or ProcessDecisionRunScopePhase.Completed) {
                var material = ProcessDecisionMaterial.FromContent(root.MaterialScope!.MaterialScope,
                    new(root.MaterialScope.ScopeId, 1, new(Guid.NewGuid())), root.MaterialScope.ContentContract, RecoveryContractFixture.Now, "{}");
                scope = scope with { Revision = 3, Material = ProcessDecisionRunScopeMaterial.From(material), Phase = phase };
            } else if (phase == ProcessDecisionRunScopePhase.Capturing) {
                scope = scope with { Revision = 2, Phase = phase, Capture = new(new(Guid.NewGuid()), new(Guid.NewGuid()), ProcessDecisionRunCaptureKind.Initial) };
            } else {
                scope = scope with { Phase = phase };
            }
            Selection = new(root, DecisionBudgetTransitions.Create(root.BudgetId, root.Policy.Budget), scope, State.Status,
                new(new ProcessLaunchPrincipal.LocalOperator(ProcessLaunchOperatorSurface.UserInterface), root.DatabaseProfileId, null, true, false, "scope-recovery-test"));
        }
        internal ProcessCancellationRecoveryClaim Claim { get; set; }
        internal ProcessRuntimeStateSnapshot State { get; private set; }
        internal ProcessDecisionRootSelection? Selection { get; set; }
        internal Exception? SelectionFailure { get; set; }
        internal Exception? CommitFailure { get; set; }
        internal bool LoseAcknowledgement { get; set; }
        internal int Reads { get; private set; }
        internal int Loads { get; private set; }
        internal int Commits { get; private set; }
        internal Dictionary<RuntimeCommandId, ProcessRuntimeCommitResult> Saved { get; } = [];
        internal async Task<ProcessCancellationRecoveryCheckpoint> AdvanceAsync() {
            var result = await new ProcessCancellationScopeRecoveryStep(this, this, this, this, this, TimeProvider.System).AdvanceAsync(Claim, default);
            result.Validate(Claim);
            Claim = Claim with { Position = result.Position, Pending = result.Pending, Intent = result.Intent };
            return result;
        }
        public Task<bool> IsStoppedAsync(ProcessRunId rootRunId, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<ProcessRuntimeStateSnapshot?> LoadAsync(ProcessRunId runId, CancellationToken cancellationToken) {
            Loads++;
            Assert.Equal(State.RunId, runId);
            return Task.FromResult<ProcessRuntimeStateSnapshot?>(State);
        }
        public Task<ProcessDecisionRootSelection?> ReadAsync(ProcessRuntimeStateSnapshot state, CancellationToken cancellationToken) {
            Reads++;
            return SelectionFailure is null ? Task.FromResult(Selection) : Task.FromException<ProcessDecisionRootSelection?>(SelectionFailure);
        }
        public Task<ProcessRuntimeCommitResult?> FindCompletedCommandAsync(RuntimeCommandId commandId, CancellationToken cancellationToken) {
            Assert.Equal(Claim.Intent!.Context.CommandId, commandId);
            var result = Saved.GetValueOrDefault(commandId);
            return Task.FromResult(result is null ? null : result with { CommittedNow = false });
        }
        public Task<ProcessRuntimeCommitResult> CommitAsync(ProcessRuntimeCommitRequest request, CancellationToken cancellationToken) {
            Commits++;
            if (CommitFailure is not null) {
                throw CommitFailure;
            }
            var command = request.DecisionRunScope!;
            var current = Selection!;
            var next = command.Kind == ProcessDecisionRunScopeCommandKind.BeginReconciliation
                ? ProcessDecisionRunScopeTransitions.BeginReconciliation(current.Scope, command.Expectation)
                : ProcessDecisionRunScopeTransitions.Finish(current.Scope, command.Expectation, true, [], current.Budget, []);
            State = request.Mutation.State;
            var result = ProcessRuntimeCommitResult.FromMutation(request.Mutation) with { DecisionRunScope = new(command, next), CommittedNow = true };
            Selection = current with { Scope = next };
            Saved.Add(request.CommandId, result);
            if (LoseAcknowledgement) {
                throw new IOException("The scope committed before acknowledgement.");
            }
            return Task.FromResult(result);
        }
    }
}
