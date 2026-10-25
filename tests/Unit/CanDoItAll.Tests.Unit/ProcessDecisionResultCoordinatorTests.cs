using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessDecisionResultCoordinatorTests {
    [Theory]
    [InlineData(StrategyOutcome.Succeeded, ProcessRuntimeStepStatus.Completed, ProcessDecisionCompletion.Complete)]
    [InlineData(StrategyOutcome.Failed, ProcessRuntimeStepStatus.Failed, ProcessDecisionCompletion.Complete)]
    [InlineData(StrategyOutcome.Canceled, ProcessRuntimeStepStatus.Cancelled, ProcessDecisionCompletion.Cancel)]
    public async Task Closed_work_joins_the_normalized_runtime_result_in_one_commit(StrategyOutcome outcome,
        ProcessRuntimeStepStatus status, ProcessDecisionCompletion completion) {
        var fixture = new Fixture();
        var result = await fixture.SubmitAsync(fixture.Command with { Result = fixture.Command.Result with { Outcome = outcome } });
        Assert.True(result.Succeeded);
        Assert.Equal(status, result.State.Steps.Single().Status);
        Assert.Equal(completion, Assert.IsType<ProcessDecisionClosure>(fixture.Request!.Decision).Completion);
        Assert.Equal(fixture.Closed.Work, Assert.IsType<ProcessDecisionBudgetSettlement>(fixture.Request.DecisionBudget).Settlement);
        Assert.Null(result.State.Steps.Single().ActiveClaimToken);
        Assert.Single(result.State.AppliedResults);
        Assert.Equal(1, fixture.Commits);
    }

    [Theory]
    [InlineData(ProcessDecisionChildPendingReason.MissingJournal, ProcessDecisionPhase.Reconciling)]
    [InlineData(ProcessDecisionChildPendingReason.ActiveWork, ProcessDecisionPhase.Reconciling)]
    [InlineData(ProcessDecisionChildPendingReason.ApprovalRequired, ProcessDecisionPhase.WaitingForReview)]
    [InlineData(ProcessDecisionChildPendingReason.BasisChanged, ProcessDecisionPhase.WaitingForReview)]
    [InlineData(ProcessDecisionChildPendingReason.UnresolvedEffects, ProcessDecisionPhase.Reconciling)]
    [InlineData(ProcessDecisionChildPendingReason.ExecutionIncomplete, ProcessDecisionPhase.Reconciling)]
    public async Task Pending_evidence_keeps_the_allocation_and_suppresses_unverified_artifacts(
        ProcessDecisionChildPendingReason reason, ProcessDecisionPhase phase) {
        var fixture = new Fixture { Outcome = new ProcessDecisionChildResult.Pending(reason) };
        var result = await fixture.SubmitAsync();
        Assert.Equal(ProcessRuntimeStepStatus.Blocked, result.State.Steps.Single().Status);
        Assert.Equal(phase, result.Decision!.Current.Phase);
        Assert.Null(fixture.Request!.DecisionBudget);
        Assert.Empty(result.State.AppliedResults.Single().ProducedArtifacts);
        Assert.Equal(1, result.Decision.Current.UnclosedChildCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Other_local_work_or_shared_capture_keeps_known_settlement_in_reconciliation(bool shared) {
        var fixture = new Fixture();
        fixture.Selection = fixture.Selection with { OtherWorkPending = !shared, SharedScopePending = shared };
        var result = await fixture.SubmitAsync();
        Assert.Equal(ProcessDecisionPhase.Reconciling, result.Decision!.Current.Phase);
        Assert.Equal(ProcessRuntimeStepStatus.Blocked, result.State.Steps.Single().Status);
        Assert.Equal(fixture.Closed.Work, Assert.IsType<ProcessDecisionBudgetSettlement>(fixture.Request!.DecisionBudget).Settlement);
        Assert.Equal(0, result.Decision.Current.UnclosedChildCount);
    }

    [Fact]
    public async Task Root_dispatch_refusal_does_not_prevent_exact_original_accounting() {
        var fixture = new Fixture();
        var child = fixture.Selection.Child!;
        fixture.Selection = fixture.Selection with { Child = child with { Observation = child.Observation with { IsCurrent = false } } };
        var result = await fixture.SubmitAsync();
        Assert.Equal(ProcessRuntimeStepStatus.Completed, result.State.Steps.Single().Status);
        Assert.NotNull(result.DecisionBudget);
    }

    [Fact]
    public async Task Missing_required_artifact_preserves_its_runtime_diagnostic_and_settles_known_work() {
        var fixture = new Fixture();
        fixture.State = fixture.State with {
            Steps = [fixture.State.Steps.Single() with { ProducedArtifactSlots = new HashSet<ArtifactSlotId> { new(Id(40)) } }]
        };
        var result = await fixture.SubmitAsync();
        Assert.Equal(ProcessRuntimeStepStatus.Blocked, result.State.Steps.Single().Status);
        Assert.Equal(ProcessDecisionPhase.WaitingForReview, result.Decision!.Current.Phase);
        Assert.Contains(result.State.AppliedResults.Single().Diagnostics, item => item.Code == ProcessRuntimeDiagnosticCodes.MissingExpectedOutputArtifact);
        Assert.NotNull(result.DecisionBudget);
    }

    [Theory]
    [InlineData(ProcessDecisionChildTerminalStatus.Failed)]
    [InlineData(ProcessDecisionChildTerminalStatus.Cancelled)]
    public async Task Claimed_success_cannot_override_a_different_original_owner_outcome(ProcessDecisionChildTerminalStatus status) {
        var fixture = new Fixture();
        fixture.Outcome = fixture.Closed with { Status = status };
        var result = await fixture.SubmitAsync();
        Assert.Equal(ProcessRuntimeStepStatus.Blocked, result.State.Steps.Single().Status);
        Assert.Empty(result.State.AppliedResults.Single().ProducedArtifacts);
        Assert.NotNull(result.DecisionBudget);
    }

    [Fact]
    public async Task Previously_settled_child_hands_off_parent_completion_without_charging_twice() {
        var fixture = new Fixture();
        var child = fixture.Selection.Child!;
        var closed = ProcessDecisionTransitions.CloseChild(fixture.Selection.Head!, fixture.Selection.Head!.Expectation,
            new(child.Original.Child.Address.Reference, child.Original.Child.Address.ChildId, child.Original.Child.Owner, child.Original.Child.AllocationId),
            fixture.Closed.Closure);
        var work = DecisionBudgetTransitions.Settle(fixture.Selection.Budget,
            new(child.Original.Child.BudgetId, child.Original.Child.AllocationId, child.Original.Child.Grant), fixture.Closed.Work);
        fixture.Selection = fixture.Selection with {
            Head = closed.Generation, Budget = work.Balance,
            Child = child with { Observation = child.Observation with { Generation = closed.Generation, Closure = fixture.Closed.Closure, Settlement = fixture.Closed.Work } }
        };
        var result = await fixture.SubmitAsync();
        Assert.False(result.DecisionBudget!.Change.Changed);
        Assert.True(result.Decision!.Changed);
        Assert.Equal(ProcessDecisionPhase.Completed, result.Decision.Current.Phase);
    }

    [Fact]
    public async Task Lost_acknowledgement_replays_before_reading_a_now_missing_journal() {
        var fixture = new Fixture();
        var first = await fixture.SubmitAsync();
        var commandId = fixture.Request!.CommandId;
        fixture.Failure = new IOException("journal no longer available");
        var replay = await fixture.SubmitAsync();
        Assert.Equal(ProcessRuntimeTransitionOutcome.Duplicate, replay.Outcome);
        Assert.Equal(commandId, fixture.Lookup);
        Assert.Equal(first.Decision, replay.Decision);
        Assert.Equal(1, fixture.Reads);
        Assert.Equal(1, fixture.Commits);
    }

    [Fact]
    public async Task Changed_metadata_under_a_used_result_key_cannot_borrow_the_compound_receipt() {
        var fixture = new Fixture();
        fixture.State = (await fixture.SubmitAsync()).State;
        var changed = fixture.Command with { Result = fixture.Command.Result with { UserSafeSummary = "Changed summary." } };
        var result = await fixture.SubmitAsync(changed);
        Assert.Equal(ProcessRuntimeTransitionOutcome.Rejected, result.Outcome);
        Assert.Equal("Runtime.DecisionResultReceiptMissing", Assert.Single(result.Diagnostics).Code);
        Assert.Equal(1, fixture.Reads);
        Assert.Equal(1, fixture.Commits);
    }

    [Fact]
    public async Task Corrupt_owner_evidence_propagates_without_writing_or_disclosing_it() {
        var fixture = new Fixture { Failure = new InvalidDataException("invalid original evidence") };
        Assert.Same(fixture.Failure, await Assert.ThrowsAsync<InvalidDataException>(() => fixture.SubmitAsync()));
        Assert.Equal(0, fixture.Commits);
    }

    [Fact]
    public async Task Unavailable_owner_evidence_becomes_explicit_reconciliation_without_settlement() {
        var fixture = new Fixture { Failure = new IOException("unavailable original evidence") };
        var result = await fixture.SubmitAsync();
        Assert.Equal(ProcessDecisionPhase.Reconciling, result.Decision!.Current.Phase);
        Assert.Null(result.DecisionBudget);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Claim_and_generation_are_checked_before_opening_original_evidence(bool oldGeneration) {
        var fixture = new Fixture();
        if (oldGeneration) {
            fixture.Selection = fixture.Selection with { Head = fixture.Selection.Head! with {
                Reference = fixture.Selection.Head!.Reference with { Generation = 2 }, Revision = 8
            } };
        } else {
            fixture.State = fixture.State with { Claims = [fixture.State.Claims.Single() with { ExpiresAtUtc = fixture.Context.OccurredAtUtc }] };
        }
        var result = await fixture.SubmitAsync();
        Assert.False(result.Succeeded);
        Assert.Equal(0, fixture.Reads);
        Assert.Equal(0, fixture.Commits);
    }

    [Theory]
    [InlineData(StrategyOutcome.Failed)]
    [InlineData(StrategyOutcome.Canceled)]
    [InlineData(StrategyOutcome.Succeeded)]
    public async Task Missing_execution_cannot_fabricate_an_owner_settlement(StrategyOutcome outcome) {
        var fixture = new Fixture();
        fixture.Selection = fixture.Selection with { Child = null };
        var command = fixture.Command with { Result = fixture.Command.Result with { ExecutionRunId = null, Outcome = outcome } };
        var result = await fixture.SubmitAsync(command);
        Assert.Equal(ProcessRuntimeStepStatus.Blocked, result.State.Steps.Single().Status);
        Assert.Null(result.DecisionBudget);
        Assert.Equal(ProcessDecisionPhase.Reconciling, result.Decision!.Current.Phase);
        Assert.Equal(0, fixture.Reads);
    }

    [Fact]
    public async Task Mutable_input_is_captured_before_the_first_query_yield() {
        var fixture = new Fixture();
        var artifacts = fixture.Command.Result.ProducedArtifacts.ToArray();
        var original = artifacts[0];
        fixture.OnQuery = () => artifacts[0] = original with { ArtifactId = new(Id(45)) };
        var result = await fixture.SubmitAsync(fixture.Command with { Result = fixture.Command.Result with { ProducedArtifacts = artifacts } });
        Assert.Equal(original.ArtifactId, Assert.Single(result.State.AppliedResults.Single().ProducedArtifacts).ArtifactId);
    }

    [Fact]
    public async Task Mismatched_command_result_key_fails_before_any_storage_read() {
        var fixture = new Fixture();
        var result = await fixture.SubmitAsync(fixture.Command with { IdempotencyKey = new(Id(44)) });
        Assert.False(result.Succeeded);
        Assert.Equal(0, fixture.Queries);
        Assert.Equal(0, fixture.Reads);
    }

    [Fact]
    public void Identity_includes_metadata_but_ignores_clock_and_mutable_revisions() {
        var fixture = new Fixture();
        var identity = ProcessDecisionResultIdentity.Derive(fixture.State, fixture.Command, fixture.Selection);
        Assert.Equal(identity, ProcessDecisionResultIdentity.Derive(fixture.State with { UpdatedAtUtc = fixture.State.UpdatedAtUtc.AddDays(1) },
            fixture.Command, fixture.Selection with { Budget = fixture.Selection.Budget with { Revision = 100 } }));
        Assert.NotEqual(identity, ProcessDecisionResultIdentity.Derive(fixture.State,
            fixture.Command with { Result = fixture.Command.Result with { UserSafeSummary = "A different result." } }, fixture.Selection));
        Assert.NotEqual(identity, ProcessDecisionResultIdentity.Derive(fixture.State,
            fixture.Command with { ClaimToken = new(Id(46)) }, fixture.Selection));
        Assert.Equal("f67fe4da-ce3e-a440-1b9e-c53896b9935f", identity.Value.ToString("D"));
    }

    private static Guid Id(int value) => Guid.Parse($"00000000-0000-0000-0000-{value:D12}");

    [Fact]
    public async Task Live_outcome_uses_selected_original_and_replay_needs_no_transient_evidence() {
        var fixture = new Fixture();
        var evidence = new ProcessExecutionOwnerEvidence<string>("closed-startup");
        fixture.ExpectedEvidence = evidence;
        fixture.Outcome = new ProcessDecisionChildResult.Closed(ProcessDecisionChildTerminalStatus.Failed,
            new(1, ProcessDecisionChildOutcome.NoEffect, null), new(1, new(120, 0)));
        var command = fixture.Command with { Result = fixture.Command.Result with {
            Outcome = StrategyOutcome.Failed, ProducedArtifacts = [], OwnerEvidence = evidence
        } };
        var first = await fixture.SubmitAsync(command);
        Assert.Equal(ProcessRuntimeStepStatus.Failed, first.State.Steps.Single().Status);
        Assert.Equal(120, first.DecisionBudget!.Change.Balance.ConsumedWorkTicks);
        Assert.True(first.DecisionBudget.Change.Balance.OverrunDetected);
        fixture.State = first.State;
        fixture.Failure = new InvalidDataException("Replay cannot read the owner again.");
        var replay = await fixture.SubmitAsync(command with { Result = command.Result with { OwnerEvidence = null } });
        Assert.Equal(ProcessRuntimeTransitionOutcome.Duplicate, replay.Outcome);
        Assert.Equal(first.DecisionBudget, replay.DecisionBudget);
        Assert.Equal(1, fixture.Reads);
    }

    [Fact]
    public async Task Live_evidence_without_an_original_selected_child_cannot_commit() {
        var fixture = new Fixture();
        fixture.Selection = fixture.Selection with { Child = null };
        var result = await fixture.SubmitAsync(fixture.Command with { Result = fixture.Command.Result with {
            OwnerEvidence = new ProcessExecutionOwnerEvidence<string>("unbound")
        } });
        Assert.False(result.Succeeded);
        Assert.Equal(0, fixture.Reads);
        Assert.Equal(0, fixture.Commits);
    }

    private sealed class Fixture : IProcessDecisionResultQuery, IProcessDecisionLiveOutcomeReader, IProcessIdempotencyStore, IProcessRuntimeUnitOfWork {
        internal Fixture() {
            var now = new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
            ProcessRunId run = new(Id(1));
            ProcessStepInstanceId step = new(Id(2));
            DispatchClaimToken claim = new(Id(3));
            DispatcherOwnerId owner = new("result-fixture");
            State = new(run, run, new(Id(4)), "sha256:" + new string('a', 64), ProcessRuntimeStatus.Active,
                [new(step, new(Id(5)), ProcessRuntimeStepStatus.Running, true, 1, new HashSet<ProcessStepInstanceId>(),
                    new HashSet<ArtifactSlotId>(), claim, null)],
                [new(claim, step, owner, DispatchClaimStatus.Claimed, 1, now, now.AddMinutes(10), null, null)], [],
                new HashSet<ArtifactSlotId>(), now);
            Context = new(new(Id(6)), new(ProcessEventActorKind.System, new("fixture")), new("result-fixture"), now.AddSeconds(1));
            var budget = DecisionBudgetTransitions.Create(new(Id(7)), new(2, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5)));
            var reservation = DecisionBudgetTransitions.Reserve(budget, new(Id(8)), new(100, 0));
            ProcessDecisionReference reference = new(new(Id(9)), 1);
            ProcessDecisionChildAdmission admission = new(new(run, run, step, reference, new(Id(10))),
                new(Id(11)), ProcessDecisionChildOwner.Agent, budget.BudgetId, reservation.Allocation.Id, reservation.Allocation.Grant);
            ProcessDecisionChildMaterialBinding binding = new(admission, new(new(Id(12)), Id(13), Id(14), run, run, step),
                new(new(new(Id(12)), 1, admission.MaterialId), new string('A', 64)), Contract);
            ProcessDecisionGeneration head = new(reference, 2, ProcessDecisionPhase.Active, admission.MaterialId, budget.BudgetId, 1, 1);
            Selection = new(run, run, step, reservation.Balance, head, new(binding, new(admission, head, null, null, true)), false, false);
            Command = new(step, owner, claim, new(Id(15)), new(new("result-fixture"), "1.0.0", Id(15), StrategyOutcome.Succeeded,
                [new(new(Id(16)), new(Id(17)), "sha256:" + new string('b', 64))], [], [], [], "sha256:" + new string('c', 64)) {
                    ExecutionRunId = new(Id(10))
                });
            Closed = new(ProcessDecisionChildTerminalStatus.Succeeded, new(7, ProcessDecisionChildOutcome.Accepted, new(Id(18))), new(7, new(120, 0)));
            Outcome = Closed;
        }

        internal ProcessRuntimeStateSnapshot State { get; set; }
        internal RuntimeCommandContext Context { get; }
        internal SubmitStrategyResultCommand Command { get; }
        internal ProcessDecisionResultSelection Selection { get; set; }
        internal ProcessDecisionChildResult.Closed Closed { get; }
        internal ProcessDecisionChildResult Outcome { get; set; }
        internal Exception? Failure { get; set; }
        internal Action? OnQuery { get; set; }
        internal int Queries { get; private set; }
        internal int Reads { get; private set; }
        internal int Commits { get; private set; }
        internal ProcessExecutionOwnerEvidence? ExpectedEvidence { get; set; }
        internal ProcessRuntimeCommitRequest? Request { get; private set; }
        internal RuntimeCommandId Lookup { get; private set; }
        private readonly Dictionary<RuntimeCommandId, ProcessRuntimeCommitResult> saved = [];
        public ProcessDecisionExecutionContract Contract { get; } = new(Id(19), 1, ProcessDecisionChildOwner.Agent);

        internal Task<ProcessRuntimeCommitResult> SubmitAsync(SubmitStrategyResultCommand? command = null)
            => new ProcessDecisionResultCoordinator(this, [this], this, this, NullLogger<ProcessDecisionResultCoordinator>.Instance)
                .SubmitAsync(State, Context with { CommandId = RuntimeCommandId.New() }, command ?? Command);

        public Task<ProcessDecisionResultSelection?> ReadAsync(ProcessRunId runId, ProcessStepInstanceId stepId,
            ProcessExecutionRunId? executionRunId, CancellationToken cancellationToken = default) {
            Queries++;
            OnQuery?.Invoke();
            return Task.FromResult<ProcessDecisionResultSelection?>(Selection);
        }

        public Task<ProcessDecisionChildResult> ReadAsync(ProcessDecisionChildMaterialBinding original, CancellationToken cancellationToken = default) {
            Reads++;
            Assert.Equal(Selection.Child!.Original, original);
            return Failure is null ? Task.FromResult(Outcome) : Task.FromException<ProcessDecisionChildResult>(Failure);
        }

        public Task<ProcessDecisionChildResult> ReadAsync(ProcessDecisionResultChild original, ProcessExecutionOwnerEvidence evidence,
            CancellationToken cancellationToken = default) {
            Assert.Same(Selection.Child, original);
            Assert.Same(ExpectedEvidence, evidence);
            return ReadAsync(original.Original, cancellationToken);
        }

        public Task<ProcessRuntimeCommitResult?> FindCompletedCommandAsync(RuntimeCommandId commandId, CancellationToken cancellationToken = default) {
            Lookup = commandId;
            return Task.FromResult(saved.GetValueOrDefault(commandId));
        }

        public Task<ProcessRuntimeCommitResult> CommitAsync(ProcessRuntimeCommitRequest request, CancellationToken cancellationToken = default) {
            Commits++;
            Request = request;
            var result = ProcessRuntimeCommitResult.FromMutation(request.Mutation);
            if (request.Decision is ProcessDecisionClosure closure) {
                var child = Selection.Child!;
                var change = ProcessDecisionTransitions.CloseChild(Selection.Head!, closure.Expectation,
                    new(child.Original.Child.Address.Reference, child.Original.Child.Address.ChildId, child.Original.Child.Owner,
                        child.Original.Child.AllocationId, child.Observation.Closure), closure.Closure, closure.Completion);
                var accounting = Assert.IsType<ProcessDecisionBudgetSettlement>(request.DecisionBudget);
                var settled = DecisionBudgetTransitions.Settle(Selection.Budget,
                    new(child.Original.Child.BudgetId, child.Original.Child.AllocationId, child.Original.Child.Grant, child.Observation.Settlement),
                    accounting.Settlement);
                result = result with { Decision = new(closure, change.Changed, change.Generation, Child: change.Child),
                    DecisionBudget = new(accounting, settled) };
            } else if (request.Decision is ProcessDecisionChangePhase phase) {
                var generation = phase.Change == ProcessDecisionPhaseChange.WaitForReview
                    ? ProcessDecisionTransitions.WaitForReview(Selection.Head!, phase.Expectation)
                    : ProcessDecisionTransitions.BeginReconciliation(Selection.Head!, phase.Expectation);
                result = result with { Decision = new(phase, true, generation) };
            }
            saved.Add(request.CommandId, result);
            return Task.FromResult(result);
        }
    }
}
