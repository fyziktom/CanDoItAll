using System.Text.Json;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging.Abstractions;
using static CanDoItAll.Tests.Unit.ProcessDecisionReviewTestData;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessDecisionTerminalAccountingTests {
    [Theory]
    [InlineData(ProcessRuntimeStepStatus.Cancelled, ProcessDecisionChildTerminalStatus.Succeeded)]
    [InlineData(ProcessRuntimeStepStatus.Cancelled, ProcessDecisionChildTerminalStatus.Failed)]
    [InlineData(ProcessRuntimeStepStatus.Cancelled, ProcessDecisionChildTerminalStatus.Cancelled)]
    [InlineData(ProcessRuntimeStepStatus.Failed, ProcessDecisionChildTerminalStatus.Succeeded)]
    [InlineData(ProcessRuntimeStepStatus.Failed, ProcessDecisionChildTerminalStatus.Failed)]
    [InlineData(ProcessRuntimeStepStatus.Failed, ProcessDecisionChildTerminalStatus.Cancelled)]
    [InlineData(ProcessRuntimeStepStatus.Completed, ProcessDecisionChildTerminalStatus.Succeeded)]
    [InlineData(ProcessRuntimeStepStatus.Completed, ProcessDecisionChildTerminalStatus.Failed)]
    [InlineData(ProcessRuntimeStepStatus.Completed, ProcessDecisionChildTerminalStatus.Cancelled)]
    public async Task Closed_child_accounting_preserves_the_existing_business_outcome(
        ProcessRuntimeStepStatus stepStatus, ProcessDecisionChildTerminalStatus childStatus) {
        var fixture = Create(stepStatus);
        fixture.Outcome = fixture.Closed with { Status = childStatus };
        var result = await CompleteAsync(fixture, Request(fixture));
        Assert.True(result.CommittedNow);
        Assert.Equal(JsonSerializer.Serialize(fixture.State), JsonSerializer.Serialize(result.State with { UpdatedAtUtc = fixture.State.UpdatedAtUtc }));
        Assert.Empty(result.Events);
        Assert.Empty(result.OutboxMessages);
        Assert.Empty(result.ArtifactLedgerEvents);
        Assert.Equal(fixture.Closed.Closure, result.Decision!.Child!.Closure);
        Assert.Equal(fixture.Closed.Work, result.DecisionBudget!.Change.Allocation.Settlement);
        Assert.Equal(stepStatus == ProcessRuntimeStepStatus.Cancelled ? ProcessDecisionPhase.Cancelled : ProcessDecisionPhase.Completed, result.Decision.Current.Phase);
        Assert.Equal(0, result.DecisionBudget.Change.Balance.ReservedWorkTicks);
        Assert.Equal(1, fixture.OutcomeReads);
    }

    [Theory]
    [InlineData(ProcessDecisionChildPendingReason.MissingJournal)]
    [InlineData(ProcessDecisionChildPendingReason.ActiveWork)]
    [InlineData(ProcessDecisionChildPendingReason.ApprovalRequired)]
    [InlineData(ProcessDecisionChildPendingReason.BasisChanged)]
    [InlineData(ProcessDecisionChildPendingReason.UnresolvedEffects)]
    [InlineData(ProcessDecisionChildPendingReason.ExecutionIncomplete)]
    public async Task Pending_owner_work_cannot_be_manufactured_into_a_settlement(ProcessDecisionChildPendingReason reason) {
        var fixture = Create();
        fixture.Outcome = new ProcessDecisionChildResult.Pending(reason);
        var result = await CompleteAsync(fixture, Request(fixture));
        Assert.False(result.Succeeded);
        Assert.Contains(result.Diagnostics, item => item.Code == "Runtime.TerminalAccountingOutcomePending");
        Assert.Equal(0, fixture.Commits);
    }

    [Theory]
    [InlineData(AdmissionChange.Waiting)]
    [InlineData(AdmissionChange.ActiveClaim)]
    [InlineData(AdmissionChange.Budget)]
    [InlineData(AdmissionChange.Decision)]
    [InlineData(AdmissionChange.ClosedDecision)]
    public async Task Changed_admission_refuses_without_reading_the_owner_or_committing(AdmissionChange change) {
        var fixture = Create();
        var request = Request(fixture);
        if (change is AdmissionChange.Waiting or AdmissionChange.ActiveClaim) {
            fixture.State = fixture.State with { Steps = fixture.State.Steps.Select(step => step with {
                Status = change == AdmissionChange.Waiting ? ProcessRuntimeStepStatus.Blocked : step.Status,
                ActiveClaimToken = change == AdmissionChange.ActiveClaim ? new(Id(93)) : null
            }).ToArray() };
        } else if (change == AdmissionChange.Budget) {
            request = request with { Budget = request.Budget with { Revision = request.Budget.Revision + 1 } };
        } else if (change == AdmissionChange.Decision) {
            request = request with { Decision = request.Decision with { Revision = request.Decision.Revision + 1 } };
        } else {
            var result = await CompleteAsync(fixture, request);
            Apply(fixture, result);
            request = Request(fixture) with { ActionId = new(Id(94)) };
        }
        var reads = fixture.OutcomeReads;
        var commits = fixture.Commits;
        Assert.False((await CompleteAsync(fixture, request)).Succeeded);
        Assert.Equal(reads, fixture.OutcomeReads);
        Assert.Equal(commits, fixture.Commits);
    }

    [Theory]
    [InlineData(ProcessRuntimeStepStatus.Ready)]
    [InlineData(ProcessRuntimeStepStatus.Blocked)]
    [InlineData(ProcessRuntimeStepStatus.Skipped)]
    public void Unsupported_runtime_status_is_not_a_terminal_accounting_request(ProcessRuntimeStepStatus status) {
        var request = Request(Create()) with { StepStatus = status };
        Assert.Throws<InvalidDataException>(request.Validate);
    }

    [Fact]
    public void Empty_action_cannot_be_used_for_recovery() {
        var request = Request(Create()) with { ActionId = default };
        Assert.Throws<InvalidDataException>(request.Validate);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Accounting_during_shared_reconciliation_preserves_other_pending_work(bool otherWork) {
        var fixture = Create();
        fixture.Selection = fixture.Selection with {
            SharedScopePending = true, OtherWorkPending = otherWork,
            Child = fixture.Selection.Child! with { Observation = fixture.Selection.Child!.Observation with { IsCurrent = false } }
        };
        var result = await CompleteAsync(fixture, Request(fixture));
        Assert.True(result.Succeeded);
        Assert.Null(result.DecisionRunScope);
        Assert.Equal(otherWork ? ProcessDecisionPhase.WaitingForReview : ProcessDecisionPhase.Cancelled, result.Decision!.Current.Phase);
        Assert.Equal(fixture.Closed.Work, result.DecisionBudget!.Change.Allocation.Settlement);
    }

    [Fact]
    public async Task Exact_receipt_recovery_needs_no_new_Agent_observation_and_does_not_recharge() {
        var fixture = Create();
        var request = Request(fixture);
        var committed = await CompleteAsync(fixture, request);
        Apply(fixture, committed);
        fixture.OutcomeFailure = new IOException("The child owner is temporarily unavailable.");
        var replay = await CompleteAsync(fixture, request);
        Assert.Equal(ProcessRuntimeTransitionOutcome.Duplicate, replay.Outcome);
        Assert.False(replay.CommittedNow);
        Assert.Equal(committed.DecisionBudget, replay.DecisionBudget);
        Assert.Equal(1, fixture.Commits);
        Assert.Equal(1, fixture.OutcomeReads);
    }

    [Theory]
    [InlineData(ProcessRuntimeStepStatus.Ready)]
    [InlineData(ProcessRuntimeStepStatus.Running)]
    [InlineData(ProcessRuntimeStepStatus.Blocked)]
    [InlineData(ProcessRuntimeStepStatus.Completed)]
    public async Task Recovered_receipt_preserves_current_runtime_after_later_rework(ProcessRuntimeStepStatus status) {
        var fixture = Create(ProcessRuntimeStepStatus.Failed);
        var request = Request(fixture);
        var committed = await CompleteAsync(fixture, request);
        Apply(fixture, committed);
        fixture.State = fixture.State with { Steps = fixture.State.Steps.Select(step => step with { Status = status }).ToArray() };
        fixture.Saved[fixture.Lookup] = committed with { State = fixture.State };
        fixture.OutcomeFailure = new IOException("The child owner is temporarily unavailable.");
        var replay = await CompleteAsync(fixture, request);
        Assert.Equal(ProcessRuntimeTransitionOutcome.Duplicate, replay.Outcome);
        Assert.False(replay.CommittedNow);
        Assert.Equal(fixture.State, replay.State);
        Assert.Equal(committed.Decision, replay.Decision);
        Assert.Equal(committed.DecisionBudget, replay.DecisionBudget);
        Assert.Equal(1, fixture.Commits);
        Assert.Equal(1, fixture.OutcomeReads);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Another_actor_or_action_cannot_adopt_an_existing_receipt(bool actor) {
        var fixture = Create();
        var request = Request(fixture);
        Apply(fixture, await CompleteAsync(fixture, request));
        if (actor) {
            fixture.Context = fixture.Context with { Actor = new(ProcessEventActorKind.Manager, new("another-manager")) };
        } else {
            request = request with { ActionId = new(Id(95)) };
        }
        Assert.False((await CompleteAsync(fixture, request)).Succeeded);
        Assert.Equal(1, fixture.Commits);
    }

    [Theory]
    [InlineData(CorruptReceipt.Material)]
    [InlineData(CorruptReceipt.Work)]
    [InlineData(CorruptReceipt.Child)]
    [InlineData(CorruptReceipt.Root)]
    [InlineData(CorruptReceipt.Completion)]
    public async Task Corrupt_original_receipt_is_not_reported_as_recovered(CorruptReceipt change) {
        var fixture = Create();
        var request = Request(fixture);
        var result = await CompleteAsync(fixture, request);
        var closure = (ProcessDecisionClosure)result.Decision!.Command;
        fixture.Saved[fixture.Lookup] = change switch {
            CorruptReceipt.Material => result with { Decision = result.Decision with { Current = result.Decision.Current with { MaterialId = new(Id(96)) } } },
            CorruptReceipt.Work => result with { DecisionBudget = result.DecisionBudget! with {
                Command = ((ProcessDecisionBudgetSettlement)result.DecisionBudget!.Command) with { Settlement = fixture.Closed.Work with { OwnerWorkRevision = 99 } }
            } },
            CorruptReceipt.Child => result with { Decision = result.Decision with { Command = closure with { ChildId = new(Id(97)) } } },
            CorruptReceipt.Root => result with { State = result.State with { RootRunId = new(Id(98)) } },
            _ => result with { Decision = result.Decision with { Command = closure with { Completion = ProcessDecisionCompletion.Review } } }
        };
        await Assert.ThrowsAsync<InvalidDataException>(() => CompleteAsync(fixture, request));
        Assert.Equal(1, fixture.Commits);
    }

    [Fact]
    public async Task Already_settled_child_can_finish_its_decision_without_a_second_charge() {
        var fixture = Create();
        fixture.SettleChildWithoutCompletingStep();
        var before = fixture.Selection.Budget;
        var result = await CompleteAsync(fixture, Request(fixture));
        Assert.True(result.Succeeded);
        Assert.Equal(ProcessDecisionPhase.Cancelled, result.Decision!.Current.Phase);
        Assert.False(result.DecisionBudget!.Change.Changed);
        Assert.Equal(before, result.DecisionBudget.Change.Balance);
    }

    [Fact]
    public async Task Actual_overrun_is_accounted_instead_of_refunded_or_lost() {
        var fixture = Create();
        fixture.Outcome = fixture.Closed with { Work = new(fixture.Closed.Work.OwnerWorkRevision, new(TimeSpan.FromMinutes(2).Ticks, 0)) };
        var result = await CompleteAsync(fixture, Request(fixture));
        Assert.True(result.Succeeded);
        Assert.True(result.DecisionBudget!.Change.Balance.OverrunDetected);
        Assert.Equal(TimeSpan.FromMinutes(2).Ticks, result.DecisionBudget.Change.Balance.ConsumedWorkTicks);
    }

    [Fact]
    public async Task Cancellation_before_observation_makes_no_commit() {
        var fixture = Create();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CompleteAsync(fixture, Request(fixture), cancellation.Token));
        Assert.Equal(0, fixture.Queries);
        Assert.Equal(0, fixture.Commits);
    }

    [Fact]
    public async Task Unavailable_original_owner_remains_pending() {
        var fixture = Create();
        fixture.OutcomeFailure = new IOException("Owner temporarily unavailable.");
        Assert.False((await CompleteAsync(fixture, Request(fixture))).Succeeded);
        Assert.Equal(0, fixture.Commits);
    }

    [Theory]
    [InlineData(OriginalChange.Root)]
    [InlineData(OriginalChange.Admission)]
    [InlineData(OriginalChange.Plan)]
    [InlineData(OriginalChange.Step)]
    [InlineData(OriginalChange.Contract)]
    public async Task Mismatched_accepted_input_cannot_authorize_accounting(OriginalChange change) {
        var fixture = Create();
        var input = fixture.Input;
        fixture.Input = change switch {
            OriginalChange.Root => input with { RootRunId = new(Id(100)) },
            OriginalChange.Admission => input with { AdmissionId = new(Id(100)) },
            OriginalChange.Plan => input with { Assignment = input.Assignment with { PlanId = new(Id(100)) } },
            OriginalChange.Step => input with { Assignment = input.Assignment with { StepInstanceId = new(Id(100)) } },
            _ => input with { Assignment = input.Assignment with {
                DecisionDataPolicy = input.Assignment.DecisionDataPolicy! with { ExecutionContract = new(Id(100), 1, ProcessDecisionChildOwner.Agent) }
            } }
        };
        await Assert.ThrowsAsync<InvalidDataException>(() => CompleteAsync(fixture, Request(fixture)));
        Assert.Equal(0, fixture.OutcomeReads);
        Assert.Equal(0, fixture.Commits);
    }

    [Theory]
    [InlineData(DispatchClaimStatus.Claimed)]
    [InlineData(DispatchClaimStatus.LeaseRenewed)]
    [InlineData(DispatchClaimStatus.Reclaimed)]
    public async Task A_live_selected_claim_blocks_accounting_even_without_an_active_token(DispatchClaimStatus status) {
        var fixture = Create();
        fixture.State = fixture.State with { Claims = fixture.State.Claims.Select(claim => claim with { Status = status }).ToArray() };
        Assert.False((await CompleteAsync(fixture, Request(fixture))).Succeeded);
        Assert.Equal(0, fixture.OutcomeReads);
        Assert.Equal(0, fixture.Commits);
    }

    [Fact]
    public async Task A_live_sibling_keeps_its_claim_and_business_state() {
        var fixture = Create();
        var sibling = fixture.State.Steps[0] with { StepInstanceId = new(Id(101)), Status = ProcessRuntimeStepStatus.Running, ActiveClaimToken = new(Id(102)) };
        var claim = fixture.State.Claims[0] with { StepInstanceId = sibling.StepInstanceId, ClaimToken = sibling.ActiveClaimToken!.Value, Status = DispatchClaimStatus.Claimed };
        fixture.State = fixture.State with { Steps = [.. fixture.State.Steps, sibling], Claims = [.. fixture.State.Claims, claim] };
        var result = await CompleteAsync(fixture, Request(fixture));
        Assert.True(result.CommittedNow);
        Assert.Equal(sibling, result.State.Steps.Single(step => step.StepInstanceId == sibling.StepInstanceId));
        Assert.Contains(claim, result.State.Claims);
        Assert.Equal(ProcessRuntimeStatus.Active, result.State.Status);
    }

    [Theory]
    [InlineData(-30, 0)]
    [InlineData(0, 0)]
    [InlineData(1, 3)]
    public async Task Accounting_preserves_a_monotonic_database_timestamp(int seconds, int ticks) {
        var fixture = Create();
        fixture.Context = fixture.Context with { OccurredAtUtc = fixture.State.UpdatedAtUtc.AddSeconds(seconds).AddTicks(ticks) };
        var result = await CompleteAsync(fixture, Request(fixture));
        Assert.True(result.State.UpdatedAtUtc > fixture.State.UpdatedAtUtc);
        Assert.Equal(0, result.State.UpdatedAtUtc.UtcTicks % 10);
    }

    [Theory]
    [InlineData(ProcessEventActorKind.Unknown, false)]
    [InlineData((ProcessEventActorKind)99, false)]
    [InlineData(ProcessEventActorKind.System, true)]
    public async Task An_unidentified_actor_cannot_create_a_recovery_identity(ProcessEventActorKind kind, bool emptyId) {
        var fixture = Create();
        fixture.Context = fixture.Context with { Actor = new(kind, emptyId ? default : new("accounting-owner")) };
        await Assert.ThrowsAsync<InvalidDataException>(() => CompleteAsync(fixture, Request(fixture)));
        Assert.Equal(0, fixture.Queries);
        Assert.Equal(0, fixture.Commits);
    }

    private static ProcessDecisionReviewCoordinatorFixture Create(ProcessRuntimeStepStatus status = ProcessRuntimeStepStatus.Cancelled) {
        var fixture = new ProcessDecisionReviewCoordinatorFixture();
        fixture.State = fixture.State with { Steps = fixture.State.Steps.Select(step => step with { Status = status }).ToArray() };
        return fixture;
    }

    private static ProcessDecisionTerminalAccountingRequest Request(ProcessDecisionReviewCoordinatorFixture fixture)
        => new(fixture.Request.Child, fixture.Selection.Head!.Expectation,
            new(fixture.Request.Child.StepInstanceId, fixture.Selection.Budget.BudgetId, fixture.Selection.Budget.Revision),
            fixture.State.Steps[0].Status, new(Id(90)));

    private static Task<ProcessRuntimeCommitResult> CompleteAsync(ProcessDecisionReviewCoordinatorFixture fixture,
        ProcessDecisionTerminalAccountingRequest request, CancellationToken cancellationToken = default)
        => new ProcessDecisionTerminalAccountingCoordinator(fixture, fixture, [fixture], fixture, fixture,
            NullLogger<ProcessDecisionTerminalAccountingCoordinator>.Instance).CompleteAsync(fixture.State, fixture.Context, request, cancellationToken);

    private static void Apply(ProcessDecisionReviewCoordinatorFixture fixture, ProcessRuntimeCommitResult result) {
        var child = fixture.Selection.Child!;
        fixture.State = result.State;
        fixture.Selection = fixture.Selection with { Head = result.Decision!.Current, Budget = result.DecisionBudget!.Change.Balance,
            Child = child with { Observation = child.Observation with {
                Generation = result.Decision.Current, Closure = result.Decision.Child!.Closure,
                Settlement = result.DecisionBudget.Change.Allocation.Settlement
            } } };
    }

    public enum AdmissionChange { Waiting, ActiveClaim, Budget, Decision, ClosedDecision }
    public enum CorruptReceipt { Material, Work, Child, Root, Completion }
    public enum OriginalChange { Root, Admission, Plan, Step, Contract }
}
