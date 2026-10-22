using System.Data.Common;
using System.Text.Json;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;
using static CanDoItAll.Tests.Integration.Processes.ProcessRootCancellationFixture;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed class ProcessDecisionFinalizationIntegrationTests {
    [Theory]
    [InlineData(ProcessDecisionPhase.Active)]
    [InlineData(ProcessDecisionPhase.WaitingForReview)]
    [InlineData(ProcessDecisionPhase.Reconciling)]
    public async Task Empty_terminal_decision_closes_without_reopening_business_work_and_then_root_can_finalize(ProcessDecisionPhase phase) {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var owner = new EfProcessRuntimeUnitOfWork(context);
        var state = (await StartAsync(context)).State;
        if (phase != ProcessDecisionPhase.Active) {
            state = (await PhaseAsync(context, state, phase == ProcessDecisionPhase.WaitingForReview
                ? ProcessDecisionPhaseChange.WaitForReview : ProcessDecisionPhaseChange.BeginReconciliation)).State;
        }
        state = await StopAsync(owner, state);
        var request = await FinalizationRequestAsync(context, state);
        var budget = await BudgetAsync(context, state);
        var events = await context.RuntimeEvents.CountAsync();
        await using var serviceScope = app.Services.CreateAsyncScope();
        var coordinator = serviceScope.ServiceProvider.GetRequiredService<ProcessDecisionFinalizationCoordinator>();
        var result = await coordinator.CompleteAsync(state, Command(state), request);
        Assert.True(result.CommittedNow);
        Assert.Equal(Business(state), Business(result.State));
        Assert.Equal(ProcessDecisionPhase.Cancelled, result.Decision!.Current.Phase);
        Assert.Equal(budget, await BudgetAsync(context, result.State));
        Assert.Equal(events, await context.RuntimeEvents.CountAsync());
        Assert.Empty(result.Events);
        Assert.Null(result.DecisionBudget);
        Assert.Empty(await context.DecisionChildren.ToArrayAsync());
        var finished = await new ProcessRuntimeEngine(owner).FinalizeRootCancellationAsync(result.State, Command(result.State));
        Assert.True(finished.CommittedNow);
        var retained = await RetainedAsync(context, finished.State);
        var replay = await coordinator.CompleteAsync(finished.State, Command(finished.State), request);
        Assert.Equal(ProcessRuntimeTransitionOutcome.Duplicate, replay.Outcome);
        Assert.Equal(result.Decision, replay.Decision);
        Assert.Equal(retained, await RetainedAsync(context, finished.State));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Previously_closed_children_preserve_measured_work_and_accepted_effects(bool accepted) {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var initial = await StartAsync(context);
        var enrolled = await EnrollAsync(context, initial.State);
        var closure = accepted ? new ProcessDecisionChildClosure(7, ProcessDecisionChildOutcome.Accepted, new(Guid.NewGuid())) : NoEffect();
        var closed = await CloseAsync(context, enrolled.State, enrolled.Decision!.Child!, closure, 12);
        var owner = new EfProcessRuntimeUnitOfWork(context);
        var state = await StopAsync(owner, closed.State);
        var budget = await BudgetAsync(context, state);
        var children = JsonSerializer.Serialize(await context.DecisionChildren.AsNoTracking().ToArrayAsync());
        var allocations = JsonSerializer.Serialize(await context.DecisionBudgetAllocations.AsNoTracking().OrderBy(item => item.AllocationId).ToArrayAsync());
        var result = await owner.CommitAsync(await FinalizationCommitAsync(context, state));
        Assert.True(result.CommittedNow);
        Assert.Equal(budget, await BudgetAsync(context, state));
        Assert.Equal(children, JsonSerializer.Serialize(await context.DecisionChildren.AsNoTracking().ToArrayAsync()));
        Assert.Equal(allocations, JsonSerializer.Serialize(await context.DecisionBudgetAllocations.AsNoTracking().OrderBy(item => item.AllocationId).ToArrayAsync()));
        Assert.True((await new ProcessRuntimeEngine(owner).FinalizeRootCancellationAsync(result.State, Command(result.State))).CommittedNow);
    }

    [Theory]
    [InlineData(ProcessRuntimeStepStatus.Completed)]
    [InlineData(ProcessRuntimeStepStatus.Failed)]
    [InlineData(ProcessRuntimeStepStatus.Skipped)]
    public async Task Terminal_business_outcome_remains_exact_when_accounting_finishes(ProcessRuntimeStepStatus status) {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var owner = new EfProcessRuntimeUnitOfWork(context);
        var state = (await StartAsync(context)).State;
        var transition = Request(state, null);
        state = (await owner.CommitAsync(transition with { Mutation = transition.Mutation with {
            State = transition.Mutation.State with { Steps = [state.Steps.Single() with { Status = status }] }
        } })).State;
        var result = await owner.CommitAsync(await FinalizationCommitAsync(context, state));
        Assert.True(result.CommittedNow);
        Assert.Equal(Business(state), Business(result.State));
        Assert.Equal(ProcessDecisionPhase.Completed, result.Decision!.Current.Phase);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Stale_decision_or_budget_refuses_without_mutation(bool budget) {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var owner = new EfProcessRuntimeUnitOfWork(context);
        var state = await StopAsync(owner, (await StartAsync(context)).State);
        var request = await FinalizationCommitAsync(context, state);
        var command = Assert.IsType<ProcessDecisionFinalization>(request.Decision);
        request = request with { Decision = budget ? command with { Budget = command.Budget with { Revision = command.Budget.Revision + 1 } }
            : command with { Expectation = command.Expectation with { Revision = command.Expectation.Revision + 1 } } };
        var before = await RetainedAsync(context, state);
        Assert.Equal(ProcessDecisionRefusal.StaleExpectation,
            (await Assert.ThrowsAsync<ProcessDecisionException>(() => owner.CommitAsync(request))).Reason);
        Assert.Equal(before, await RetainedAsync(context, state));
    }

    [Theory]
    [InlineData(Pending.Child)]
    [InlineData(Pending.ZeroWorkReplan)]
    [InlineData(Pending.MissingChild)]
    [InlineData(Pending.MismatchedClosure)]
    public async Task Unknown_or_corrupt_original_work_cannot_be_assumed_finished(Pending pending) {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var owner = new EfProcessRuntimeUnitOfWork(context);
        var state = (await StartAsync(context)).State;
        if (pending == Pending.ZeroWorkReplan) {
            var budget = await BudgetAsync(context, state);
            state = (await owner.CommitAsync(Request(state, null, new ProcessDecisionBudgetReservation(
                new(Step(state), budget.BudgetId, budget.Revision), new(Guid.NewGuid()), new(0, 1))))).State;
        } else {
            var enrolled = await EnrollAsync(context, state);
            state = pending == Pending.Child ? enrolled.State :
                (await CloseAsync(context, enrolled.State, enrolled.Decision!.Child!, NoEffect(), 5)).State;
            if (pending is Pending.MissingChild or Pending.MismatchedClosure) {
                var row = await context.DecisionChildren.SingleAsync();
                if (pending == Pending.MissingChild) {
                    context.DecisionChildren.Remove(row);
                } else {
                    row.OwnerRevision++;
                }
                await context.SaveChangesAsync();
                context.ChangeTracker.Clear();
            }
        }
        state = await StopAsync(owner, state);
        var request = await FinalizationCommitAsync(context, state);
        var before = await RetainedAsync(context, state);
        if (pending == Pending.MissingChild) {
            await Assert.ThrowsAsync<InvalidDataException>(() => owner.CommitAsync(request));
        } else {
            await Assert.ThrowsAnyAsync<InvalidOperationException>(() => owner.CommitAsync(request));
        }
        Assert.Equal(before, await RetainedAsync(context, state));
    }

    [Fact]
    public async Task A_forged_original_terminal_step_cannot_override_persisted_ready_work() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var state = (await StartAsync(context)).State;
        var forged = state with { Steps = [state.Steps.Single() with { Status = ProcessRuntimeStepStatus.Cancelled }] };
        var request = await FinalizationCommitAsync(context, forged);
        var before = await RetainedAsync(context, state);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new EfProcessRuntimeUnitOfWork(context).CommitAsync(request));
        Assert.Equal(before, await RetainedAsync(context, state));
    }

    [Fact]
    public async Task A_persisted_live_claim_cannot_be_hidden_by_proposed_release() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var owner = new EfProcessRuntimeUnitOfWork(context);
        var state = (await StartAsync(context)).State;
        state = (await owner.CommitAsync(ProcessDecisionRunScopeFixture.Claim(state))).State;
        var forged = state with { Steps = [state.Steps.Single() with { Status = ProcessRuntimeStepStatus.Cancelled, ActiveClaimToken = null }] };
        var request = await FinalizationCommitAsync(context, forged);
        request = request with { Mutation = request.Mutation with { State = request.Mutation.State with {
            Claims = request.Mutation.State.Claims.Select(claim => claim with { Status = DispatchClaimStatus.Cancelled }).ToArray()
        } } };
        var before = await RetainedAsync(context, state);
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.CommitAsync(request));
        Assert.Equal(before, await RetainedAsync(context, state));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Finalization_rolls_back_before_or_after_flush_and_can_retry_the_original_command(bool after) {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var owner = new EfProcessRuntimeUnitOfWork(context);
        var state = await StopAsync(owner, (await StartAsync(context)).State);
        var request = await FinalizationCommitAsync(context, state);
        var before = await RetainedAsync(context, state);
        var fault = new FlushFault(after);
        await using (var broken = Context(app.Services, fault)) {
            Assert.Same(fault.Failure, await Assert.ThrowsAsync<InvalidOperationException>(() => new EfProcessRuntimeUnitOfWork(broken).CommitAsync(request)));
        }
        Assert.Equal(after, fault.Flushed);
        Assert.Equal(before, await RetainedAsync(context, state));
        Assert.Null(await owner.FindCompletedCommandAsync(request.CommandId));
        Assert.True((await owner.CommitAsync(request)).CommittedNow);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Independent_owners_commit_one_finalization_and_replay_only_the_same_command(bool sameCommand) {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var state = await StopAsync(new EfProcessRuntimeUnitOfWork(context), (await StartAsync(context)).State);
        var first = await FinalizationCommitAsync(context, state);
        var second = sameCommand ? first : first with { CommandId = RuntimeCommandId.New() };
        async Task<ProcessRuntimeCommitResult?> AttemptAsync(ProcessRuntimeCommitRequest request) {
            await using var independent = Context(app.Services);
            try {
                return await new EfProcessRuntimeUnitOfWork(independent).CommitAsync(request);
            } catch (ProcessRuntimeOptimisticConcurrencyException) {
                return null;
            }
        }
        var results = await Task.WhenAll(AttemptAsync(first), AttemptAsync(second));
        Assert.Single(results, result => result?.CommittedNow == true);
        Assert.Equal(sameCommand ? 0 : 1, results.Count(result => result is null));
        Assert.Equal(2, (await DecisionAsync(context, state)).Revision);
        if (sameCommand) {
            Assert.Equal(results[0]!.Decision, results[1]!.Decision);
        }
    }

    [Fact]
    public async Task Lost_acknowledgement_and_actual_restart_recover_the_original_terminal_receipt() {
        await using var environment = CanDoItAllTestEnvironment.Create("process-decision-generation");
        var harness = new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = environment.CreatePostgreSqlProfile("terminal-finalization-restart") };
        ProcessRuntimeCommitRequest request;
        string retained;
        ProcessDecisionReceipt receipt;
        await using (var app = await TestApplication.CreateAsync(harness)) {
            await using var context = Context(app.Services);
            var owner = new EfProcessRuntimeUnitOfWork(context);
            var state = await StopAsync(owner, (await StartAsync(context)).State);
            request = await FinalizationCommitAsync(context, state);
            var failure = new InvalidOperationException("Injected lost terminal decision acknowledgement.");
            await using (var broken = Context(app.Services, new LostAcknowledgement(failure))) {
                Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => new EfProcessRuntimeUnitOfWork(broken).CommitAsync(request)));
            }
            receipt = (await owner.FindCompletedCommandAsync(request.CommandId))!.Decision!;
            retained = await RetainedAsync(context, state);
        }
        await using var restarted = await TestApplication.CreateAsync(harness);
        await using var reopened = Context(restarted.Services);
        var replay = await new EfProcessRuntimeUnitOfWork(reopened).CommitAsync(request);
        Assert.False(replay.CommittedNow);
        Assert.Equal(receipt, replay.Decision);
        Assert.Equal(retained, await RetainedAsync(reopened, request.OriginalState));
    }

    [Fact]
    public async Task Shared_scope_reconciliation_closes_local_decisions_before_parent_finalization() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var owner = ProcessDecisionRunScopeFixture.Owner(context, app.Services);
        var initial = ProcessDecisionRunScopeFixture.Initial(app.Services);
        var state = (await owner.CommitAsync(initial)).State;
        var scope = (await ProcessDecisionRunScopeFixture.Scopes(context, app.Services).ReadAsync(initial.InitialRunDecisionAdmission!.MaterialScope!.ScopeId))!;
        var captured = await ProcessDecisionRunScopeFixture.CaptureAsync(context, app.Services, state, scope);
        scope = captured.DecisionRunScope!.State;
        state = (await owner.CommitAsync(await ProcessDecisionRunScopeFixture.JoinRequestAsync(context, app.Services, captured.State, scope))).State;
        state = await StopAsync(owner, state);
        scope = (await ProcessDecisionRunScopeFixture.Scopes(context, app.Services).ReadAsync(scope.Admission.ScopeId))!;
        var reconciling = await owner.CommitAsync(ProcessDecisionRunScopeFixture.ScopeRequest(state,
            new(scope.Expectation, ProcessDecisionRunScopeCommandKind.BeginReconciliation)));
        var closed = await owner.CommitAsync(await FinalizationCommitAsync(context, reconciling.State));
        Assert.Equal(ProcessDecisionPhase.Cancelled, closed.Decision!.Current.Phase);
        var scopeClosed = await owner.CommitAsync(ProcessDecisionRunScopeFixture.ScopeRequest(closed.State,
            new(reconciling.DecisionRunScope!.State.Expectation, ProcessDecisionRunScopeCommandKind.Cancel)));
        Assert.True((await new ProcessRuntimeEngine(owner).FinalizeRootCancellationAsync(scopeClosed.State, Command(scopeClosed.State))).CommittedNow);
    }

    [Fact]
    public async Task Finalization_reads_one_exact_guard_and_a_bounded_child_set_without_material_payloads() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var state = await StopAsync(new EfProcessRuntimeUnitOfWork(context), (await StartAsync(context)).State);
        var request = await FinalizationCommitAsync(context, state);
        var probe = new QueryProbe();
        await using var measured = Context(app.Services, probe);
        Assert.True((await new EfProcessRuntimeUnitOfWork(measured).CommitAsync(request)).CommittedNow);
        var guard = Assert.Single(probe.Reads, row => row.Sql.Contains("AS \"InconsistentChild\"", StringComparison.Ordinal));
        Assert.DoesNotContain("PayloadJson", guard.Sql, StringComparison.Ordinal);
        Assert.Contains(Assert.IsType<ProcessDecisionFinalization>(request.Decision).Budget.BudgetId.Value, guard.Values);
        var children = Assert.Single(probe.Reads, row => row.Sql.Contains("FROM process_decision_children", StringComparison.Ordinal) &&
            row.Values.Contains(ProcessDecisionGeneration.MaximumChildren + 1));
        Assert.Contains("LIMIT", children.Sql, StringComparison.Ordinal);
        Assert.DoesNotContain("JOIN", children.Sql, StringComparison.Ordinal);
    }

    private static string Business(ProcessRuntimeStateSnapshot state) => JsonSerializer.Serialize(state with { UpdatedAtUtc = default });

    private static async Task<ProcessDecisionFinalizationRequest> FinalizationRequestAsync(ProcessPersistenceDbContext context, ProcessRuntimeStateSnapshot state) {
        var head = await DecisionAsync(context, state);
        var budget = await BudgetAsync(context, state);
        return new(Step(state), head.Expectation, new(Step(state), budget.BudgetId, budget.Revision), state.Steps.Single().Status, RuntimeCommandId.New());
    }

    private static async Task<ProcessRuntimeCommitRequest> FinalizationCommitAsync(ProcessPersistenceDbContext context, ProcessRuntimeStateSnapshot state) {
        var request = await FinalizationRequestAsync(context, state);
        return Request(state, new ProcessDecisionFinalization(request.StepInstanceId, request.Decision, request.Budget, request.StepStatus));
    }

    private sealed class FlushFault(bool after) : SaveChangesInterceptor {
        internal Exception Failure { get; } = new InvalidOperationException("Injected terminal decision flush failure.");
        internal bool Flushed { get; private set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
            => after ? ValueTask.FromResult(result) : throw Failure;
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default) {
            Flushed = result > 0 && eventData.Context!.Database.CurrentTransaction is not null;
            throw Failure;
        }
    }

    private sealed class LostAcknowledgement(Exception failure) : DbTransactionInterceptor {
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
            => throw failure;
    }

    private sealed class QueryProbe : DbCommandInterceptor {
        internal List<(string Sql, object[] Values)> Reads { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            Reads.Add((command.CommandText, command.Parameters.Cast<DbParameter>().Select(parameter => parameter.Value).ToArray()));
            return ValueTask.FromResult(result);
        }
    }

    public enum Pending { Child, ZeroWorkReplan, MissingChild, MismatchedClosure }
}
