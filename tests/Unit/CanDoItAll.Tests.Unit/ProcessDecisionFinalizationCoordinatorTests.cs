using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using static CanDoItAll.Tests.Unit.ProcessDecisionReviewTestData;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessDecisionFinalizationCoordinatorTests {
    [Theory]
    [InlineData(ProcessRuntimeStepStatus.Cancelled)]
    [InlineData(ProcessRuntimeStepStatus.Completed)]
    [InlineData(ProcessRuntimeStepStatus.Failed)]
    [InlineData(ProcessRuntimeStepStatus.Skipped)]
    public async Task Finalization_changes_only_timestamp_and_decision_accounting(ProcessRuntimeStepStatus status) {
        var fixture = new Fixture(status);
        var original = fixture.State;
        var request = fixture.Request();
        var result = await fixture.CompleteAsync(request);
        Assert.True(result.CommittedNow);
        Assert.Equal(original, result.State with { UpdatedAtUtc = original.UpdatedAtUtc });
        Assert.True(result.State.UpdatedAtUtc > original.UpdatedAtUtc);
        Assert.Empty(result.Events);
        Assert.Empty(result.OutboxMessages);
        Assert.Empty(result.ArtifactLedgerEvents);
        Assert.Null(result.DecisionBudget);
        Assert.Null(result.DecisionRunScope);
        Assert.Null(result.Decision!.Child);
        Assert.Equal(request.Budget.BudgetId, result.Decision.Current.BudgetId);
        Assert.Equal(1, fixture.Queries);
        Assert.Equal(1, fixture.Commits);
        Assert.Null(fixture.LastCommit!.DecisionMaterial);
    }

    [Fact]
    public async Task Original_receipt_replays_before_rework_or_new_expectations_without_another_query_or_commit() {
        var fixture = new Fixture();
        var request = fixture.Request();
        var first = await fixture.CompleteAsync(request);
        fixture.State = fixture.State with { Steps = fixture.State.Steps.Select(step => step with { Status = ProcessRuntimeStepStatus.Ready }).ToArray() };
        fixture.Context = fixture.Context with { CommandId = RuntimeCommandId.New(), OccurredAtUtc = fixture.Context.OccurredAtUtc.AddMinutes(1) };
        var replay = await fixture.CompleteAsync(request);
        Assert.Equal(ProcessRuntimeTransitionOutcome.Duplicate, replay.Outcome);
        Assert.False(replay.CommittedNow);
        Assert.Equal(first.Decision, replay.Decision);
        Assert.Equal(1, fixture.Queries);
        Assert.Equal(1, fixture.Commits);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_different_action_or_actor_cannot_borrow_the_original_receipt(bool actor) {
        var fixture = new Fixture();
        var request = fixture.Request();
        await fixture.CompleteAsync(request);
        var identity = fixture.Lookup;
        if (actor) {
            fixture.Context = fixture.Context with { Actor = new(ProcessEventActorKind.System, new("another-recovery-owner")) };
        } else {
            request = request with { ActionId = RuntimeCommandId.New() };
        }
        Assert.False((await fixture.CompleteAsync(request)).Succeeded);
        Assert.NotEqual(identity, fixture.Lookup);
        Assert.Equal(1, fixture.Commits);
    }

    [Theory]
    [InlineData(StepChange.Status)]
    [InlineData(StepChange.Binding)]
    [InlineData(StepChange.Claim)]
    [InlineData(StepChange.Missing)]
    public async Task Changed_step_or_live_claim_is_refused_before_selection(StepChange change) {
        var fixture = new Fixture();
        var request = fixture.Request();
        var step = fixture.State.Steps[0];
        fixture.State = change switch {
            StepChange.Status => fixture.State with { Steps = [step with { Status = ProcessRuntimeStepStatus.Ready }] },
            StepChange.Binding => fixture.State with { Steps = [step with { ActiveClaimToken = new(Id(50)) }] },
            StepChange.Claim => fixture.State with { Claims = fixture.State.Claims.Select(claim => claim with { Status = DispatchClaimStatus.LeaseRenewed }).ToArray() },
            _ => fixture.State with { Steps = [] }
        };
        Assert.False((await fixture.CompleteAsync(request)).Succeeded);
        Assert.Equal(0, fixture.Queries);
        Assert.Equal(0, fixture.Commits);
    }

    [Theory]
    [InlineData(SelectionChange.BudgetRevision)]
    [InlineData(SelectionChange.DecisionRevision)]
    [InlineData(SelectionChange.OpenChildren)]
    [InlineData(SelectionChange.UnknownWork)]
    [InlineData(SelectionChange.Closed)]
    public async Task Stale_or_unfinished_selection_cannot_finalize(SelectionChange change) {
        var fixture = new Fixture();
        var request = fixture.Request();
        var selected = fixture.Selection;
        fixture.Selection = change switch {
            SelectionChange.BudgetRevision => selected with { Budget = selected.Budget with { Revision = selected.Budget.Revision + 1 } },
            SelectionChange.DecisionRevision => selected with { Head = selected.Head! with { Revision = selected.Head.Revision + 1 } },
            SelectionChange.OpenChildren => selected with { Head = selected.Head! with { Revision = 2, ChildCount = 1, UnclosedChildCount = 1 } },
            SelectionChange.UnknownWork => selected with { OtherWorkPending = true },
            _ => selected with { Head = selected.Head! with { Phase = ProcessDecisionPhase.Cancelled } }
        };
        Assert.False((await fixture.CompleteAsync(request)).Succeeded);
        Assert.Equal(0, fixture.Commits);
    }

    [Fact]
    public async Task A_stopped_shared_scope_does_not_block_independent_local_accounting() {
        var fixture = new Fixture();
        fixture.Selection = fixture.Selection with { SharedScopePending = true };
        Assert.True((await fixture.CompleteAsync(fixture.Request())).CommittedNow);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_corrupt_original_receipt_is_never_replaced_by_a_new_commit(bool budget) {
        var fixture = new Fixture();
        var request = fixture.Request();
        var first = await fixture.CompleteAsync(request);
        fixture.Saved[fixture.Lookup] = budget ? first with { Decision = first.Decision! with {
            Current = first.Decision.Current with { BudgetId = new(Id(100)) }
        } } : first with { State = first.State with { RootRunId = new(Id(100)) } };
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.CompleteAsync(request));
        Assert.Equal(1, fixture.Queries);
        Assert.Equal(1, fixture.Commits);
    }

    [Fact]
    public async Task Cancellation_and_missing_actor_or_action_fail_before_selection_and_commit() {
        var fixture = new Fixture();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.CompleteAsync(fixture.Request(), cancellation.Token));
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.CompleteAsync(fixture.Request() with { ActionId = default }));
        fixture.Context = fixture.Context with { Actor = new(ProcessEventActorKind.Unknown, new("unknown")) };
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.CompleteAsync(fixture.Request()));
        Assert.Equal(0, fixture.Queries);
        Assert.Equal(0, fixture.Commits);
    }

    private sealed class Fixture : IProcessDecisionResultQuery, IProcessIdempotencyStore, IProcessRuntimeUnitOfWork {
        internal Fixture(ProcessRuntimeStepStatus status = ProcessRuntimeStepStatus.Cancelled) {
            var data = new ProcessDecisionReviewTestData();
            State = data.State with { Steps = data.State.Steps.Select(step => step with { Status = status }).ToArray() };
            Context = data.Context;
            var budget = DecisionBudgetTransitions.Create(new(Id(18)), new(1, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5)));
            var head = ProcessDecisionTransitions.Create(new(Id(6)), new(Id(15)), budget.BudgetId);
            Selection = new(State.RootRunId, State.RunId, State.Steps[0].StepInstanceId, budget, head, null, false, false);
        }

        internal ProcessRuntimeStateSnapshot State { get; set; }
        internal RuntimeCommandContext Context { get; set; }
        internal ProcessDecisionResultSelection Selection { get; set; }
        internal int Queries { get; private set; }
        internal int Commits { get; private set; }
        internal RuntimeCommandId Lookup { get; private set; }
        internal ProcessRuntimeCommitRequest? LastCommit { get; private set; }
        internal Dictionary<RuntimeCommandId, ProcessRuntimeCommitResult> Saved { get; } = [];
        internal ProcessDecisionFinalizationRequest Request() => new(State.Steps[0].StepInstanceId, Selection.Head!.Expectation,
            new(State.Steps[0].StepInstanceId, Selection.Budget.BudgetId, Selection.Budget.Revision), State.Steps[0].Status, new(Id(90)));
        internal Task<ProcessRuntimeCommitResult> CompleteAsync(ProcessDecisionFinalizationRequest request, CancellationToken token = default)
            => new ProcessDecisionFinalizationCoordinator(this, this, this).CompleteAsync(State, Context, request, token);

        public Task<ProcessDecisionResultSelection?> ReadAsync(ProcessRunId run, ProcessStepInstanceId step,
            ProcessExecutionRunId? execution, CancellationToken cancellationToken = default) {
            Queries++;
            Assert.Equal(State.RunId, run);
            Assert.Equal(Selection.StepInstanceId, step);
            Assert.Null(execution);
            return Task.FromResult<ProcessDecisionResultSelection?>(Selection);
        }

        public Task<ProcessRuntimeCommitResult?> FindCompletedCommandAsync(RuntimeCommandId command, CancellationToken cancellationToken = default) {
            Lookup = command;
            return Task.FromResult(Saved.GetValueOrDefault(command));
        }

        public Task<ProcessRuntimeCommitResult> CommitAsync(ProcessRuntimeCommitRequest request, CancellationToken cancellationToken = default) {
            Commits++;
            LastCommit = request;
            var command = Assert.IsType<ProcessDecisionFinalization>(request.Decision);
            var head = ProcessDecisionTransitions.FinalizeTerminal(Selection.Head!, command.Expectation, command.StepStatus);
            var result = ProcessRuntimeCommitResult.FromMutation(request.Mutation) with { Decision = new(command, true, head), CommittedNow = true };
            Saved.Add(request.CommandId, result);
            State = result.State;
            Selection = Selection with { Head = head };
            return Task.FromResult(result);
        }
    }

    public enum StepChange { Status, Binding, Claim, Missing }
    public enum SelectionChange { BudgetRevision, DecisionRevision, OpenChildren, UnknownWork, Closed }
}
