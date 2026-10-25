using System.Collections.Concurrent;
using System.Data.Common;
using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed class ProcessDecisionPersistenceTests {
    [Fact]
    public async Task Quiescence_waits_for_all_children_and_preserves_effects_and_accounting_across_refresh() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var started = await StartAsync(context);
        var first = await EnrollAsync(context, started.State);
        var second = await EnrollAsync(context, first.State, ProcessDecisionChildOwner.Workflow);
        var waiting = await PhaseAsync(context, second.State, ProcessDecisionPhaseChange.WaitForReview);
        var reconciling = await PhaseAsync(context, waiting.State, ProcessDecisionPhaseChange.BeginReconciliation);
        var store = new EfProcessDecisionStore(context);
        var observed = await store.ReadReconciliationAsync(reconciling.State.RunId, Step(reconciling.State), reconciling.Decision!.Current.Expectation);
        Assert.Equal(2, observed.Children.Count);
        Assert.All(observed.Children, child => Assert.Null(child.Closure));
        var blocked = await Assert.ThrowsAsync<ProcessDecisionException>(() => EnrollAsync(context, reconciling.State));
        Assert.Equal(ProcessDecisionRefusal.InvalidPhase, blocked.Reason);
        var accepted = new ProcessDecisionChildClosure(7, ProcessDecisionChildOutcome.Accepted, new(Guid.NewGuid()));
        var closedFirst = await CloseAsync(context, reconciling.State, first.Decision!.Child!, accepted, 12);
        Assert.Equal(1, closedFirst.Decision!.Current.UnclosedChildCount);
        var pending = await Assert.ThrowsAsync<ProcessDecisionException>(() => PhaseAsync(context, closedFirst.State, ProcessDecisionPhaseChange.Cancel));
        Assert.Equal(ProcessDecisionRefusal.UnclosedChildren, pending.Reason);
        var closedSecond = await CloseAsync(context, closedFirst.State, second.Decision!.Child!, NoEffect(), 8);
        var next = await RefreshAsync(context, closedSecond.State, ProcessDecisionRefreshKind.AutomaticReplan);
        Assert.Equal(2, next.Decision!.Current.Reference.Generation);
        Assert.Equal(started.Decision!.Current.BudgetId, next.Decision.Current.BudgetId);
        Assert.Equal(ProcessDecisionPhase.Superseded, next.Decision.Previous!.Phase);
        Assert.Equal(0, next.Decision.Current.ChildCount);
        Assert.Equal(1, next.DecisionBudget!.Change.Balance.ConsumedReplans);
        Assert.Equal(30, next.DecisionBudget.Change.Balance.ConsumedWorkTicks);
        Assert.Equal(0, next.DecisionBudget.Change.Balance.ReservedWorkTicks);
        Assert.Equal(next.Decision.Previous, await store.ReadGenerationAsync(next.State.RunId, Step(next.State), started.Decision.Current.Reference));
        var retained = await context.DecisionChildren.AsNoTracking().SingleAsync(child => child.ChildId == first.Decision.Child.ChildId.Value);
        Assert.Equal(accepted.AcceptedReceipt!.Value.Value, retained.AcceptedReceiptId);
        Assert.Equal(2, await context.DecisionGenerations.CountAsync());
        Assert.Equal(4, await context.DecisionBudgetAllocations.CountAsync());
    }

    [Fact]
    public async Task Restart_replays_the_original_decision_receipt_separately_from_the_later_head() {
        await using var environment = CanDoItAllTestEnvironment.Create("process-decision-generation");
        var profile = environment.CreatePostgreSqlProfile("generation-restart");
        var harness = new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = profile };
        ProcessRuntimeCommitRequest original;
        ProcessDecisionReceipt originalReceipt;
        await using (var app = await TestApplication.CreateAsync(harness)) {
            await using var context = Context(app.Services);
            var start = await StartAsync(context);
            original = Request(start.State, new ProcessDecisionChangePhase(Step(start.State), start.Decision!.Current.Expectation,
                ProcessDecisionPhaseChange.BeginReconciliation));
            var first = await new EfProcessRuntimeUnitOfWork(context).CommitAsync(original);
            originalReceipt = first.Decision!;
            await RefreshAsync(context, first.State, ProcessDecisionRefreshKind.ManagerRefresh);
        }
        await using var restarted = await TestApplication.CreateAsync(harness);
        await using var reopened = Context(restarted.Services);
        var owner = new EfProcessRuntimeUnitOfWork(reopened);
        var replay = await owner.CommitAsync(original);
        Assert.Equal(originalReceipt, replay.Decision);
        Assert.Equal(1, replay.Decision!.Current.Reference.Generation);
        var current = await new EfProcessDecisionStore(reopened).ReadAsync(original.OriginalState.RunId, Step(original.OriginalState));
        Assert.Equal(2, current!.Reference.Generation);
        Assert.True(replay.State.UpdatedAtUtc > original.Mutation.State.UpdatedAtUtc);
        Assert.Equal(originalReceipt, (await owner.FindCompletedCommandAsync(original.CommandId))!.Decision);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Runtime_command_identity_rejects_changed_removed_or_added_decision_intent(int change) {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var started = await StartAsync(context);
        var phase = new ProcessDecisionChangePhase(Step(started.State), started.Decision!.Current.Expectation, ProcessDecisionPhaseChange.BeginReconciliation);
        var request = Request(started.State, change == 3 ? null : phase);
        var owner = new EfProcessRuntimeUnitOfWork(context);
        var first = await owner.CommitAsync(request);
        var changed = change switch {
            0 => phase with { Change = ProcessDecisionPhaseChange.WaitForReview },
            1 => phase with { Expectation = phase.Expectation with { Revision = phase.Expectation.Revision + 1 } },
            2 => null,
            _ => phase
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.CommitAsync(request with { Decision = changed }));
        Assert.Equal(first.State.UpdatedAtUtc, (await owner.LoadAsync(started.State.RunId))!.UpdatedAtUtc);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SQL_flush_failure_rolls_back_generation_child_budget_and_runtime_together(bool closure) {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var started = await StartAsync(context);
        var current = closure ? await EnrollAsync(context, started.State) : started;
        var request = closure ? await ClosureRequestAsync(context, current.State, current.Decision!.Child!, NoEffect(), 4)
            : await EnrollmentRequestAsync(context, current.State);
        var beforeBudget = await BudgetAsync(context, current.State);
        var beforeDecision = await DecisionAsync(context, current.State);
        var fault = new AfterFlushFault();
        await using (var broken = Context(app.Services, fault)) {
            Assert.Same(fault.Failure, await Assert.ThrowsAsync<InvalidOperationException>(() => new EfProcessRuntimeUnitOfWork(broken).CommitAsync(request)));
            Assert.True(fault.Flushed);
        }
        Assert.Equal(beforeBudget, await BudgetAsync(context, current.State));
        Assert.Equal(beforeDecision, await DecisionAsync(context, current.State));
        var owner = new EfProcessRuntimeUnitOfWork(context);
        Assert.Null(await owner.FindCompletedCommandAsync(request.CommandId));
        Assert.Equal(current.State.UpdatedAtUtc, (await owner.LoadAsync(current.State.RunId))!.UpdatedAtUtc);
        Assert.True((await owner.CommitAsync(request)).Succeeded);
    }

    [Fact]
    public async Task Lost_acknowledgement_recovers_the_original_child_without_another_reservation() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var started = await StartAsync(context);
        var request = await EnrollmentRequestAsync(context, started.State);
        var failure = new InvalidOperationException("Injected lost Process commit acknowledgement.");
        await using (var broken = Context(app.Services, new AfterCommitFault(failure))) {
            Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => new EfProcessRuntimeUnitOfWork(broken).CommitAsync(request)));
        }
        var recovered = await new EfProcessRuntimeUnitOfWork(context).CommitAsync(request);
        Assert.Equal(((ProcessDecisionEnrollment)request.Decision!).Child, recovered.Decision!.Child);
        Assert.Equal(1, recovered.Decision.Current.ChildCount);
        Assert.Single(await context.DecisionChildren.ToArrayAsync());
        Assert.Equal(2, (await BudgetAsync(context, started.State)).AllocationCount);
    }

    [Fact]
    public async Task Competing_enrollment_has_one_winner_and_stale_generation_cannot_follow_the_new_runtime_state() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var started = await StartAsync(context);
        var first = await EnrollmentRequestAsync(context, started.State);
        var second = await EnrollmentRequestAsync(context, started.State);
        await using var other = Context(app.Services);
        var attempts = await Task.WhenAll(AttemptAsync(new EfProcessRuntimeUnitOfWork(context), first), AttemptAsync(new EfProcessRuntimeUnitOfWork(other), second));
        Assert.Single(attempts, error => error is null);
        Assert.Single(attempts, error => error is ProcessRuntimeOptimisticConcurrencyException);
        Assert.Single(await context.DecisionChildren.AsNoTracking().ToArrayAsync());
        var owner = new EfProcessRuntimeUnitOfWork(context);
        var current = (await owner.LoadAsync(started.State.RunId))!;
        var stale = Request(current, new ProcessDecisionChangePhase(Step(current), started.Decision!.Current.Expectation, ProcessDecisionPhaseChange.BeginReconciliation));
        var refused = await Assert.ThrowsAsync<ProcessDecisionException>(() => owner.CommitAsync(stale));
        Assert.Equal(ProcessDecisionRefusal.StaleExpectation, refused.Reason);
    }

    [Fact]
    public async Task Held_decision_commit_does_not_block_an_independent_root() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var first = await StartAsync(context);
        var other = await StartAsync(context);
        var request = await EnrollmentRequestAsync(context, first.State);
        var independent = await EnrollmentRequestAsync(context, other.State);
        var pause = new BeforeCommitPause();
        await using var held = Context(app.Services, pause);
        var pending = new EfProcessRuntimeUnitOfWork(held).CommitAsync(request);
        try {
            await pause.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True((await new EfProcessRuntimeUnitOfWork(context).CommitAsync(independent).WaitAsync(TimeSpan.FromSeconds(10))).Succeeded);
            Assert.False(pending.IsCompleted);
            Assert.Equal(0, (await DecisionAsync(context, first.State)).ChildCount);
        } finally {
            pause.Release.TrySetResult(true);
            await pending.WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Closure_cannot_release_another_child_or_settle_without_its_generation(bool standalone) {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var first = await EnrollAsync(context, (await StartAsync(context)).State);
        var second = await EnrollAsync(context, first.State);
        var request = await ClosureRequestAsync(context, second.State, first.Decision!.Child!, NoEffect(), 7);
        request = standalone ? request with { Decision = null }
            : request with { DecisionBudget = ((ProcessDecisionBudgetSettlement)request.DecisionBudget!) with { AllocationId = second.Decision!.Child!.AllocationId } };
        var before = await BudgetAsync(context, second.State);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new EfProcessRuntimeUnitOfWork(context).CommitAsync(request));
        Assert.Equal(before, await BudgetAsync(context, second.State));
        Assert.Equal(2, (await DecisionAsync(context, second.State)).UnclosedChildCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Existing_child_identity_or_allocation_cannot_be_reused_for_another_child(bool sameIdentity) {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var first = await EnrollAsync(context, (await StartAsync(context)).State);
        var current = first;
        if (sameIdentity) {
            current = await CloseAsync(context, current.State, first.Decision!.Child!, NoEffect(), 7);
            current = await PhaseAsync(context, current.State, ProcessDecisionPhaseChange.BeginReconciliation);
            current = await RefreshAsync(context, current.State, ProcessDecisionRefreshKind.ManagerRefresh);
        }
        var request = await EnrollmentRequestAsync(context, current.State);
        var enrollment = (ProcessDecisionEnrollment)request.Decision!;
        request = sameIdentity ? request with { Decision = enrollment with { Child = enrollment.Child with { ChildId = first.Decision!.Child!.ChildId } } }
            : request with {
                Decision = enrollment with { Child = enrollment.Child with { AllocationId = first.Decision!.Child!.AllocationId } },
                DecisionBudget = ((ProcessDecisionBudgetReservation)request.DecisionBudget!) with { AllocationId = first.Decision.Child.AllocationId }
            };
        var before = await BudgetAsync(context, current.State);
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => new EfProcessRuntimeUnitOfWork(context).CommitAsync(request));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
        Assert.Equal(before, await BudgetAsync(context, current.State));
        Assert.Single(await context.DecisionChildren.AsNoTracking().ToArrayAsync());
    }

    [Fact]
    public async Task Capture_allocation_is_not_available_to_enroll_a_child_or_refresh_again() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var started = await StartAsync(context);
        var request = await EnrollmentRequestAsync(context, started.State);
        var capture = started.DecisionBudget!.Change.Allocation;
        var enrollment = (ProcessDecisionEnrollment)request.Decision!;
        request = request with {
            Decision = enrollment with { Child = enrollment.Child with { AllocationId = capture.Id } },
            DecisionBudget = ((ProcessDecisionBudgetReservation)request.DecisionBudget!) with { AllocationId = capture.Id, Grant = capture.Grant }
        };
        var owner = new EfProcessRuntimeUnitOfWork(context);
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.CommitAsync(request));
        var reconciling = await PhaseAsync(context, started.State, ProcessDecisionPhaseChange.BeginReconciliation);
        var refresh = new ProcessDecisionRefresh(Step(started.State), reconciling.Decision!.Current.Expectation, new(Guid.NewGuid()), ProcessDecisionRefreshKind.ManagerRefresh);
        var budget = await BudgetAsync(context, reconciling.State);
        var reused = Request(reconciling.State, refresh, new ProcessDecisionBudgetSettlement(new(Step(started.State), budget.BudgetId, budget.Revision), capture.Id, capture.Settlement!));
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.CommitAsync(reused));
        Assert.Single(await context.DecisionGenerations.ToArrayAsync());
    }

    [Fact]
    public async Task Automatic_refresh_consumes_the_original_replan_allowance_and_manual_review_does_not_reset_it() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var started = await StartAsync(context);
        var first = await PhaseAsync(context, started.State, ProcessDecisionPhaseChange.BeginReconciliation);
        var refreshed = await RefreshAsync(context, first.State, ProcessDecisionRefreshKind.AutomaticReplan);
        var second = await PhaseAsync(context, refreshed.State, ProcessDecisionPhaseChange.BeginReconciliation);
        var exhausted = await Assert.ThrowsAsync<DecisionBudgetException>(() => RefreshAsync(context, second.State, ProcessDecisionRefreshKind.AutomaticReplan));
        Assert.Equal(DecisionBudgetRefusal.ReplansExhausted, exhausted.Reason);
        var manual = await RefreshAsync(context, second.State, ProcessDecisionRefreshKind.ManagerRefresh);
        Assert.Equal(1, manual.DecisionBudget!.Change.Balance.ConsumedReplans);
        Assert.Equal(15, manual.DecisionBudget.Change.Balance.ConsumedWorkTicks);
        Assert.Equal(3, manual.Decision!.Current.Reference.Generation);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Foreign_or_missing_decision_cannot_be_targeted_using_another_current_runtime(bool missing) {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var first = await StartAsync(context);
        var other = await StartAsync(context);
        var expected = missing ? first.Decision!.Current.Expectation with { Reference = new(new(Guid.NewGuid()), 1) } : first.Decision!.Current.Expectation;
        var request = Request(other.State, new ProcessDecisionChangePhase(Step(other.State), expected, ProcessDecisionPhaseChange.BeginReconciliation));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new EfProcessRuntimeUnitOfWork(context).CommitAsync(request));
        Assert.Equal(ProcessDecisionPhase.Active, (await DecisionAsync(context, first.State)).Phase);
        Assert.Equal(ProcessDecisionPhase.Active, (await DecisionAsync(context, other.State)).Phase);
    }

    [Fact]
    public async Task Legacy_assignment_cannot_be_retrofitted_with_a_decision_and_empty_migration_round_trips() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var owner = new EfProcessRuntimeUnitOfWork(context);
        var initial = ProcessProjectAdmissionFixture.Initial();
        var state = (await owner.CommitAsync(initial)).State;
        var request = Request(state, new ProcessDecisionStart(Step(state), new(Guid.NewGuid()), new(Guid.NewGuid())));
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.CommitAsync(request));
        await ApplyMigrationAsync(context, true);
        await ApplyMigrationAsync(context, false);
        Assert.True((await owner.CommitAsync(initial)).Succeeded);
        Assert.Equal(state.UpdatedAtUtc, (await owner.LoadAsync(state.RunId))!.UpdatedAtUtc);
        Assert.Empty(await context.DecisionHeads.ToArrayAsync());
    }

    [Fact]
    public async Task Migration_downgrade_refuses_retained_generation_and_command_receipt() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var started = await StartAsync(context);
        var error = await Assert.ThrowsAsync<PostgresException>(() => ApplyMigrationAsync(context, true));
        Assert.Equal(PostgresErrorCodes.RaiseException, error.SqlState);
        Assert.Equal(started.Decision!.Current, await DecisionAsync(context, started.State));
        Assert.Contains(await context.IdempotencyKeys.ToArrayAsync(), key => key.DecisionReceiptJson is not null);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Retained_receipt_rejects_unknown_version_noncanonical_shape_unknown_members_or_invalid_result(int change) {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var started = await StartAsync(context);
        var key = await context.IdempotencyKeys.SingleAsync(item => item.RunId == started.State.RunId.Value && item.DecisionReceiptJson != null);
        var document = JsonNode.Parse(key.DecisionReceiptJson!)!;
        switch (change) {
            case 0:
                document["version"] = 2;
                break;
            case 2:
                document["unexpected"] = true;
                break;
            case 3:
                document["current"]!["revision"] = 0;
                break;
        }
        key.DecisionReceiptJson = change == 1 ? " " + key.DecisionReceiptJson : document.ToJsonString();
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var read = () => new EfProcessRuntimeUnitOfWork(context).FindCompletedCommandAsync(new(key.CommandId));
        if (change == 2) {
            await Assert.ThrowsAsync<JsonException>(read);
        } else {
            await Assert.ThrowsAsync<InvalidDataException>(read);
        }
    }

    [Fact]
    public async Task Enrollment_queries_one_current_generation_and_child_without_scanning_event_history() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var started = await StartAsync(context);
        var request = await EnrollmentRequestAsync(context, started.State);
        var probe = new DecisionReadProbe();
        await using var measured = Context(app.Services, probe);
        Assert.True((await new EfProcessRuntimeUnitOfWork(measured).CommitAsync(request)).Succeeded);
        var head = Assert.Single(probe.Reads, sql => sql.Contains("FROM process_decision_heads", StringComparison.Ordinal));
        Assert.Contains("WHERE", head, StringComparison.Ordinal);
        var child = Assert.Single(probe.Reads, sql => sql.Contains("FROM process_decision_children", StringComparison.Ordinal));
        Assert.Contains("\"ChildId\" =", child, StringComparison.Ordinal);
        var scope = Assert.Single(probe.Reads, sql => sql.Contains("FROM process_decision_run_scopes", StringComparison.Ordinal));
        Assert.Contains("\"RunId\" =", scope, StringComparison.Ordinal);
        Assert.Contains("\"StepInstanceId\" IS NULL", scope, StringComparison.Ordinal);
        Assert.DoesNotContain("PayloadJson", scope, StringComparison.Ordinal);
        Assert.DoesNotContain(probe.Reads, sql => sql.Contains("FROM process_runtime_events", StringComparison.Ordinal));
        Assert.InRange(probe.Reads.Count, 4, 5);
    }

    [Fact]
    public async Task Initial_decision_and_capture_settlement_roll_back_together_after_flush() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var request = await StartRequestAsync(context);
        var before = await BudgetAsync(context, request.OriginalState);
        var fault = new AfterFlushFault();
        await using (var broken = Context(app.Services, fault)) {
            Assert.Same(fault.Failure, await Assert.ThrowsAsync<InvalidOperationException>(() => new EfProcessRuntimeUnitOfWork(broken).CommitAsync(request)));
        }
        Assert.Empty(await context.DecisionHeads.ToArrayAsync());
        Assert.Empty(await context.DecisionGenerations.ToArrayAsync());
        Assert.Equal(before, await BudgetAsync(context, request.OriginalState));
        var owner = new EfProcessRuntimeUnitOfWork(context);
        Assert.Null(await owner.FindCompletedCommandAsync(request.CommandId));
        Assert.True((await owner.CommitAsync(request)).Succeeded);
    }

    [Fact]
    public async Task Review_requires_its_runtime_waiting_transition_in_the_same_commit() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var started = await StartAsync(context);
        var phase = new ProcessDecisionChangePhase(Step(started.State), started.Decision!.Current.Expectation, ProcessDecisionPhaseChange.WaitForReview);
        var request = Request(started.State, phase);
        var invalid = request with { Mutation = request.Mutation with { State = request.Mutation.State with { Steps = started.State.Steps } } };
        var owner = new EfProcessRuntimeUnitOfWork(context);
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.CommitAsync(invalid));
        Assert.Equal(started.Decision.Current, await DecisionAsync(context, started.State));
        Assert.Equal(started.State.UpdatedAtUtc, (await owner.LoadAsync(started.State.RunId))!.UpdatedAtUtc);
        var waiting = await owner.CommitAsync(request);
        Assert.Equal(ProcessRuntimeStepStatus.Waiting, waiting.State.Steps.Single().Status);
        Assert.Equal(ProcessDecisionPhase.WaitingForReview, waiting.Decision!.Current.Phase);
    }

    [Fact]
    public async Task Incorrect_child_counters_cannot_authorize_terminal_transition_or_reconciliation() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var enrolled = await EnrollAsync(context, (await StartAsync(context)).State);
        var reconciling = await PhaseAsync(context, enrolled.State, ProcessDecisionPhaseChange.BeginReconciliation);
        var generation = await context.DecisionGenerations.SingleAsync();
        generation.UnclosedChildCount = 0;
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var current = await DecisionAsync(context, reconciling.State);
        await Assert.ThrowsAsync<InvalidDataException>(() => new EfProcessDecisionStore(context).ReadReconciliationAsync(
            reconciling.State.RunId, Step(reconciling.State), current.Expectation));
        await Assert.ThrowsAsync<InvalidDataException>(() => PhaseAsync(context, reconciling.State, ProcessDecisionPhaseChange.Cancel));
        Assert.Equal(20, (await BudgetAsync(context, reconciling.State)).ReservedWorkTicks);
    }

    [Fact]
    public async Task Missing_current_generation_is_an_explicit_corruption_error() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var started = await StartAsync(context);
        await context.DecisionGenerations.ExecuteDeleteAsync();
        await Assert.ThrowsAsync<InvalidDataException>(() => new EfProcessDecisionStore(context).ReadAsync(started.State.RunId, Step(started.State)));
    }

    [Fact]
    public async Task Closure_retains_overrun_after_run_cancellation_and_refuses_further_work() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var enrolled = await EnrollAsync(context, (await StartAsync(context)).State);
        var owner = new EfProcessRuntimeUnitOfWork(context);
        var stopped = await ProcessRootCancellationFixture.StopAsync(owner, enrolled.State);
        var closure = await ClosureRequestAsync(context, stopped, enrolled.Decision!.Child!, NoEffect(), 21);
        var closed = await owner.CommitAsync(closure with {
            Decision = Assert.IsType<ProcessDecisionClosure>(closure.Decision) with { Completion = ProcessDecisionCompletion.Cancel }
        });
        Assert.True(closed.DecisionBudget!.Change.Balance.OverrunDetected);
        Assert.Equal(26, closed.DecisionBudget.Change.Balance.ConsumedWorkTicks);
        Assert.Equal(0, closed.Decision!.Current.UnclosedChildCount);
        var refused = await EnrollAsync(context, closed.State);
        Assert.False(refused.Succeeded);
        Assert.Contains(refused.Diagnostics, item => item.Code == EfProcessRootCancellationGuard.WorkAdmissionStopped);
        Assert.Equal(ProcessRuntimeStepStatus.Cancelled, closed.State.Steps.Single().Status);
        Assert.Equal(ProcessDecisionPhase.Cancelled, closed.Decision.Current.Phase);
        Assert.True((await BudgetAsync(context, closed.State)).OverrunDetected);
        Assert.True((await new ProcessRuntimeEngine(owner).FinalizeRootCancellationAsync(closed.State,
            ProcessRootCancellationFixture.Command(closed.State))).Succeeded);
    }

    private static async Task<Exception?> AttemptAsync(EfProcessRuntimeUnitOfWork owner, ProcessRuntimeCommitRequest request) {
        try {
            await owner.CommitAsync(request);
            return null;
        } catch (Exception error) {
            return error;
        }
    }

    private static Task ApplyMigrationAsync(ProcessPersistenceDbContext context, bool downgrade)
        => ProcessDecisionMigrationFixture.ApplyAsync(context, downgrade,
            new CanDoItAll.Migrations.PostgreSql.Migrations.AddProcessDecisionGenerations(),
            new CanDoItAll.Migrations.PostgreSql.Migrations.AddProcessSharedRunMaterialScopes());

    private sealed class AfterFlushFault : SaveChangesInterceptor {
        public Exception Failure { get; } = new InvalidOperationException("Injected failure after generation SQL flush.");
        public bool Flushed { get; private set; }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default) {
            Flushed = result > 0 && eventData.Context!.Database.CurrentTransaction is not null;
            throw Failure;
        }
    }

    private sealed class AfterCommitFault(Exception failure) : DbTransactionInterceptor {
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default) => throw failure;
    }

    private sealed class BeforeCommitPause : DbTransactionInterceptor {
        public TaskCompletionSource<bool> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData,
            InterceptionResult result, CancellationToken cancellationToken = default) {
            Entered.TrySetResult(true);
            await Release.Task.WaitAsync(cancellationToken);
            return result;
        }
    }

    private sealed class DecisionReadProbe : DbCommandInterceptor {
        public ConcurrentQueue<string> Reads { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            if (command.CommandText.StartsWith("SELECT", StringComparison.Ordinal) &&
                    (command.CommandText.Contains("FROM process_decision_", StringComparison.Ordinal) || command.CommandText.Contains("FROM process_runtime_events", StringComparison.Ordinal)) &&
                    !command.CommandText.Contains("FROM process_decision_budget", StringComparison.Ordinal)) {
                Reads.Enqueue(command.CommandText);
            }
            return ValueTask.FromResult(result);
        }
    }
}
