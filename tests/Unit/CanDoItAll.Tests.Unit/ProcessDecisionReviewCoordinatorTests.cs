using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;
using Microsoft.Extensions.Logging.Abstractions;
using static CanDoItAll.Tests.Unit.ProcessDecisionReviewTestData;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessDecisionReviewCoordinatorTests {
    [Fact]
    public async Task Selected_review_observation_uses_one_query_and_creates_no_action_or_work() {
        var fixture = new ProcessDecisionReviewCoordinatorFixture();
        var observed = await ObserveAsync(fixture);
        Assert.Equal(fixture.Selection.Child, observed.Child);
        Assert.Equal(fixture.Request, observed.Pending!.ForAction(fixture.Request.CompletionKey.Value));
        Assert.Null(observed.Refusal);
        Assert.False(observed.IsSettled);
        Assert.Equal(1, fixture.Queries);
        Assert.Equal(1, fixture.InputReads);
        Assert.Equal(0, fixture.OutcomeReads);
        Assert.Equal(0, fixture.Commits);
        Assert.Empty(fixture.Saved);
    }

    [Theory]
    [InlineData(ChangedObservation.RootCancellation)]
    [InlineData(ChangedObservation.ActiveClaim)]
    [InlineData(ChangedObservation.LaterResult)]
    [InlineData(ChangedObservation.OtherWork)]
    [InlineData(ChangedObservation.SharedWork)]
    public async Task Changed_original_runtime_or_other_work_cannot_offer_a_new_review(ChangedObservation change) {
        var fixture = new ProcessDecisionReviewCoordinatorFixture();
        fixture.State = change switch {
            ChangedObservation.RootCancellation => fixture.State with { Status = ProcessRuntimeStatus.CancelRequested },
            ChangedObservation.ActiveClaim => fixture.State with {
                Steps = fixture.State.Steps.Select(step => step with { ActiveClaimToken = new(Id(90)) }).ToArray()
            },
            ChangedObservation.LaterResult => fixture.State with {
                AppliedResults = [.. fixture.State.AppliedResults,
                    fixture.State.AppliedResults[0] with {
                        IdempotencyKey = new(Id(91)), AppliedSequence = 5, Outcome = StrategyOutcome.Failed,
                        AppliedStepStatus = ProcessRuntimeStepStatus.Failed
                    }]
            },
            _ => fixture.State
        };
        fixture.Selection = fixture.Selection with {
            OtherWorkPending = change == ChangedObservation.OtherWork,
            SharedScopePending = change == ChangedObservation.SharedWork
        };
        var observed = await ObserveAsync(fixture);
        Assert.Null(observed.Pending);
        Assert.NotNull(observed.Refusal);
        Assert.False(observed.IsSettled);
        Assert.Equal(0, fixture.Commits);
        Assert.Equal(0, fixture.OutcomeReads);
    }

    [Fact]
    public async Task Accounted_child_work_without_a_runtime_completion_is_not_a_settled_review() {
        var fixture = new ProcessDecisionReviewCoordinatorFixture();
        var child = fixture.Selection.Child!;
        fixture.Selection = fixture.Selection with { Child = child with { Observation = child.Observation with {
            Closure = fixture.Closed.Closure, Settlement = fixture.Closed.Work
        } } };
        var observed = await ObserveAsync(fixture);
        Assert.False(observed.IsSettled);
        Assert.False(observed.RuntimeCompletionObserved);
        Assert.Null(observed.Pending);
        Assert.Equal(0, fixture.Commits);
        Assert.Equal(0, fixture.OutcomeReads);
    }

    [Fact]
    public async Task Completed_runtime_receipt_and_accounted_original_child_are_observed_without_another_outcome_read() {
        var fixture = new ProcessDecisionReviewCoordinatorFixture();
        var result = await fixture.CompleteAsync();
        fixture.State = result.State;
        var child = fixture.Selection.Child!;
        fixture.Selection = fixture.Selection with {
            Head = result.Decision!.Current, Budget = result.DecisionBudget!.Change.Balance,
            Child = child with { Observation = child.Observation with {
                Generation = result.Decision.Current, Closure = fixture.Closed.Closure, Settlement = fixture.Closed.Work
            } }
        };
        var observed = await ObserveAsync(fixture);
        Assert.True(observed.IsSettled);
        Assert.Null(observed.Pending);
        Assert.Equal(1, fixture.Commits);
        Assert.Equal(1, fixture.OutcomeReads);
    }

    private static Task<ProcessDecisionReviewObservation> ObserveAsync(ProcessDecisionReviewCoordinatorFixture fixture)
        => new ProcessDecisionReviewCoordinator(fixture, fixture, [fixture], fixture, fixture, NullLogger<ProcessDecisionReviewCoordinator>.Instance)
            .ObserveAsync(fixture.State, fixture.Request.Child.StepInstanceId, new(fixture.Request.Child.ChildId.Value));

    [Theory]
    [InlineData(ProcessDecisionChildTerminalStatus.Succeeded, StrategyOutcome.Succeeded)]
    [InlineData(ProcessDecisionChildTerminalStatus.Failed, StrategyOutcome.Failed)]
    [InlineData(ProcessDecisionChildTerminalStatus.Cancelled, StrategyOutcome.Canceled)]
    public async Task Actual_closed_child_outcome_and_exact_work_are_committed_together(
        ProcessDecisionChildTerminalStatus terminal, StrategyOutcome expected) {
        var fixture = new ProcessDecisionReviewCoordinatorFixture();
        fixture.Outcome = fixture.Closed with { Status = terminal };
        var result = await fixture.CompleteAsync();
        Assert.True(result.Succeeded);
        Assert.Equal(expected, result.State.AppliedResults[1].Outcome);
        Assert.Equal(StrategyOutcome.NeedsManager, result.State.AppliedResults[0].Outcome);
        Assert.Equal(fixture.Closed.Closure, Assert.IsType<ProcessDecisionClosure>(fixture.LastCommit!.Decision).Closure);
        Assert.Equal(fixture.Closed.Work, Assert.IsType<ProcessDecisionBudgetSettlement>(fixture.LastCommit.DecisionBudget).Settlement);
        Assert.Equal(0, result.Decision!.Current.UnclosedChildCount);
        Assert.Equal(1, fixture.Queries);
        Assert.Equal(1, fixture.InputReads);
        Assert.Equal(1, fixture.OutcomeReads);
        Assert.Equal(1, fixture.Commits);
    }

    [Theory]
    [InlineData(ProcessDecisionChildPendingReason.MissingJournal)]
    [InlineData(ProcessDecisionChildPendingReason.ActiveWork)]
    [InlineData(ProcessDecisionChildPendingReason.ApprovalRequired)]
    [InlineData(ProcessDecisionChildPendingReason.BasisChanged)]
    [InlineData(ProcessDecisionChildPendingReason.UnresolvedEffects)]
    [InlineData(ProcessDecisionChildPendingReason.ExecutionIncomplete)]
    public async Task Pending_child_evidence_cannot_complete_or_refund_work(ProcessDecisionChildPendingReason reason) {
        var fixture = new ProcessDecisionReviewCoordinatorFixture { Outcome = new ProcessDecisionChildResult.Pending(reason) };
        var result = await fixture.CompleteAsync();
        Assert.False(result.Succeeded);
        Assert.Equal("Runtime.ReviewOutcomePending", Assert.Single(result.Diagnostics).Code);
        Assert.Equal(0, fixture.Commits);
        Assert.Empty(fixture.Saved);
        Assert.Single(result.State.AppliedResults);
    }

    [Fact]
    public async Task Receipt_replay_precedes_later_phase_generation_time_trace_and_missing_journal() {
        var fixture = new ProcessDecisionReviewCoordinatorFixture();
        var first = await fixture.CompleteAsync();
        var command = fixture.Lookup;
        fixture.State = first.State;
        fixture.Selection = fixture.Selection with {
            Head = first.Decision!.Current with {
                Reference = first.Decision.Current.Reference with { Generation = 2 }, Revision = 7,
                Phase = ProcessDecisionPhase.Active, MaterialId = new(Id(80))
            },
            Budget = first.DecisionBudget!.Change.Balance
        };
        fixture.Context = fixture.Context with {
            CommandId = RuntimeCommandId.New(), CorrelationId = new("new-retry-trace"), OccurredAtUtc = fixture.Context.OccurredAtUtc.AddDays(1)
        };
        fixture.OutcomeFailure = new IOException("The child's journal is no longer readable.");
        var replay = await fixture.CompleteAsync();
        Assert.Equal(ProcessRuntimeTransitionOutcome.Duplicate, replay.Outcome);
        Assert.False(replay.CommittedNow);
        Assert.Equal(command, fixture.Lookup);
        Assert.Equal(first.Decision, replay.Decision);
        Assert.Equal(first.DecisionBudget, replay.DecisionBudget);
        Assert.Equal(1, fixture.OutcomeReads);
        Assert.Equal(1, fixture.Commits);
    }

    [Theory]
    [InlineData(ChangedCommand.Actor)]
    [InlineData(ChangedCommand.OriginalInput)]
    [InlineData(ChangedCommand.DecisionExpectation)]
    [InlineData(ChangedCommand.BudgetExpectation)]
    public async Task Changed_command_authority_cannot_borrow_a_prior_receipt(ChangedCommand change) {
        var fixture = new ProcessDecisionReviewCoordinatorFixture();
        var first = await fixture.CompleteAsync();
        var original = fixture.Lookup;
        fixture.State = first.State;
        fixture.Selection = fixture.Selection with { Head = first.Decision!.Current, Budget = first.DecisionBudget!.Change.Balance };
        if (change == ChangedCommand.Actor) {
            fixture.Context = fixture.Context with { Actor = fixture.Context.Actor with { Id = new("other-reviewer") } };
        } else if (change == ChangedCommand.OriginalInput) {
            fixture.Input = fixture.Input with { InputFingerprint = Hash('f') };
        } else if (change == ChangedCommand.DecisionExpectation) {
            fixture.Request = fixture.Request with { Decision = fixture.Request.Decision with { Revision = fixture.Request.Decision.Revision + 1 } };
        } else {
            fixture.Request = fixture.Request with { Budget = fixture.Request.Budget with { Revision = fixture.Request.Budget.Revision + 1 } };
        }
        var result = await fixture.CompleteAsync();
        Assert.False(result.Succeeded);
        Assert.NotEqual(original, fixture.Lookup);
        Assert.Equal(1, fixture.OutcomeReads);
        Assert.Equal(1, fixture.Commits);
    }

    [Fact]
    public async Task Existing_matching_settlement_only_advances_the_waiting_parent_phase() {
        var fixture = new ProcessDecisionReviewCoordinatorFixture();
        fixture.SettleChildWithoutCompletingStep();
        var original = fixture.Selection.Budget;
        var result = await fixture.CompleteAsync();
        Assert.True(result.Succeeded);
        Assert.True(result.Decision!.Changed);
        Assert.Equal(ProcessDecisionPhase.Completed, result.Decision.Current.Phase);
        Assert.False(result.DecisionBudget!.Change.Changed);
        Assert.Equal(original, result.DecisionBudget.Change.Balance);
    }

    [Fact]
    public async Task Runtime_receipt_without_compound_accounting_receipt_does_not_authorize_another_commit() {
        var fixture = new ProcessDecisionReviewCoordinatorFixture();
        fixture.State = (await fixture.CompleteAsync()).State;
        fixture.Saved.Clear();
        var result = await fixture.CompleteAsync();
        Assert.False(result.Succeeded);
        Assert.Equal("Runtime.ReviewReceiptMissing", Assert.Single(result.Diagnostics).Code);
        Assert.Equal(1, fixture.Commits);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unresolved_local_or_shared_work_refuses_before_reading_the_child(bool shared) {
        var fixture = new ProcessDecisionReviewCoordinatorFixture();
        fixture.Selection = fixture.Selection with { OtherWorkPending = !shared, SharedScopePending = shared };
        var result = await fixture.CompleteAsync();
        Assert.False(result.Succeeded);
        Assert.Equal(0, fixture.OutcomeReads);
        Assert.Equal(0, fixture.Commits);
    }

    [Fact]
    public async Task Journal_io_failure_remains_pending_without_fabricated_zero_work() {
        var fixture = new ProcessDecisionReviewCoordinatorFixture { OutcomeFailure = new IOException("Sensitive path must not enter a protocol response.") };
        var result = await fixture.CompleteAsync();
        Assert.False(result.Succeeded);
        Assert.Equal("Runtime.ReviewOutcomePending", Assert.Single(result.Diagnostics).Code);
        Assert.Equal(0, fixture.Commits);
    }

    [Theory]
    [InlineData(MalformedOutcome.Revision)]
    [InlineData(MalformedOutcome.RetainedClosure)]
    [InlineData(MalformedOutcome.RetainedWork)]
    [InlineData(MalformedOutcome.Status)]
    [InlineData(MalformedOutcome.PendingReason)]
    [InlineData(MalformedOutcome.Missing)]
    public async Task Extracted_outcome_owner_refuses_inconsistent_protocol_evidence(MalformedOutcome change) {
        var fixture = new ProcessDecisionReviewCoordinatorFixture();
        var child = fixture.Selection.Child!;
        if (change == MalformedOutcome.RetainedClosure) {
            child = child with { Observation = child.Observation with { Closure = fixture.Closed.Closure with { OwnerRevision = 99 } } };
        } else if (change == MalformedOutcome.RetainedWork) {
            child = child with { Observation = child.Observation with { Settlement = fixture.Closed.Work with { OwnerWorkRevision = 99 } } };
        } else {
            fixture.Outcome = change switch {
                MalformedOutcome.Revision => fixture.Closed with { Work = fixture.Closed.Work with { OwnerWorkRevision = 99 } },
                MalformedOutcome.Status => fixture.Closed with { Status = (ProcessDecisionChildTerminalStatus)int.MaxValue },
                MalformedOutcome.PendingReason => new ProcessDecisionChildResult.Pending((ProcessDecisionChildPendingReason)int.MaxValue),
                MalformedOutcome.Missing => null!,
                _ => throw new ArgumentOutOfRangeException(nameof(change))
            };
        }
        fixture.Selection = fixture.Selection with { Child = child };
        var owner = new ProcessDecisionChildOutcomes([fixture], NullLogger.Instance);
        await Assert.ThrowsAsync<InvalidDataException>(() => owner.ReadAsync(child, evidence: null));
        Assert.Equal(0, fixture.Commits);
    }

    [Fact]
    public async Task A_modified_saved_settlement_cannot_replay_as_the_original_review() {
        var fixture = new ProcessDecisionReviewCoordinatorFixture();
        var first = await fixture.CompleteAsync();
        var accounting = Assert.IsType<ProcessDecisionBudgetSettlement>(first.DecisionBudget!.Command);
        fixture.Saved[fixture.Lookup] = first with { DecisionBudget = first.DecisionBudget with {
            Command = accounting with { Settlement = accounting.Settlement with { OwnerWorkRevision = 99 } }
        } };
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.CompleteAsync());
        Assert.Equal(1, fixture.OutcomeReads);
        Assert.Equal(1, fixture.Commits);
    }

    [Fact]
    public async Task Consistently_substituted_work_still_disagrees_with_the_original_result_digest() {
        var fixture = new ProcessDecisionReviewCoordinatorFixture();
        var first = await fixture.CompleteAsync();
        var budget = first.DecisionBudget!;
        var accounting = Assert.IsType<ProcessDecisionBudgetSettlement>(budget.Command);
        var changed = accounting.Settlement with { Consumed = new(TimeSpan.FromSeconds(3).Ticks, 0) };
        fixture.Saved[fixture.Lookup] = first with { DecisionBudget = budget with {
            Command = accounting with { Settlement = changed },
            Change = budget.Change with {
                Allocation = budget.Change.Allocation with { Settlement = changed },
                Balance = budget.Change.Balance with { ConsumedWorkTicks = changed.Consumed.WorkTicks }
            }
        } };
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.CompleteAsync());
        Assert.Equal(1, fixture.OutcomeReads);
        Assert.Equal(1, fixture.Commits);
    }

    public enum ChangedCommand { Actor, OriginalInput, DecisionExpectation, BudgetExpectation }
    public enum MalformedOutcome { Revision, RetainedClosure, RetainedWork, Status, PendingReason, Missing }
    public enum ChangedObservation { RootCancellation, ActiveClaim, LaterResult, OtherWork, SharedWork }
}
