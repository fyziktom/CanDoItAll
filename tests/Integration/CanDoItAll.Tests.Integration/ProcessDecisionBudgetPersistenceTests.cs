using System.Collections.Concurrent;
using System.Data.Common;
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

namespace CanDoItAll.Tests.Integration.Processes;

public sealed class ProcessDecisionBudgetPersistenceTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Initial_assignment_creates_only_its_admitted_balance_and_replay_keeps_identity(bool admitted) {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var initial = Initial(admitted);
        var owner = new EfProcessRuntimeUnitOfWork(context);
        var first = await owner.CommitAsync(initial);
        var stepId = first.State.Steps.Single().StepInstanceId;
        var store = new EfProcessDecisionBudgetStore(context);
        var balance = await store.ReadAsync(first.State.RunId, stepId);
        Assert.Equal(admitted, balance is not null);
        if (admitted) {
            Assert.Equal(initial.InitialAssignments!.Single().DecisionDataPolicy!.Policy.Budget, balance!.Ceiling);
            Assert.Equal(1, balance.Revision);
            Assert.Equal(0, balance.AllocationCount);
        }
        Assert.True((await owner.CommitAsync(initial)).Succeeded);
        Assert.Equal(balance, await store.ReadAsync(first.State.RunId, stepId));
        Assert.Equal(admitted ? 1 : 0, await context.DecisionBudgets.CountAsync());
    }

    [Fact]
    public async Task Restart_and_later_settlement_preserve_original_command_receipt_separately_from_current_balance() {
        await using var environment = CanDoItAllTestEnvironment.Create("process-decision-budget");
        var profile = environment.CreatePostgreSqlProfile("budget-restart");
        var harness = new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = profile };
        ProcessRuntimeCommitRequest reservation;
        ProcessDecisionBudgetReceipt originalReceipt;
        await using (var app = await TestApplication.CreateAsync(harness)) {
            await using var context = Context(app.Services);
            var owner = new EfProcessRuntimeUnitOfWork(context);
            var state = (await owner.CommitAsync(Initial())).State;
            var balance = await BalanceAsync(context, state);
            reservation = Advance(state, Reserve(state, balance, 100));
            originalReceipt = (await owner.CommitAsync(reservation)).DecisionBudget!;
            Assert.Null(originalReceipt.Change.Allocation.Settlement);
        }
        await using var restarted = await TestApplication.CreateAsync(harness);
        await using var reopened = Context(restarted.Services);
        var runtime = new EfProcessRuntimeUnitOfWork(reopened);
        var current = (await runtime.LoadAsync(reservation.OriginalState.RunId))!;
        var settled = await runtime.CommitAsync(Advance(current, new ProcessDecisionBudgetSettlement(
            new(current.Steps.Single().StepInstanceId, originalReceipt.Change.Balance.BudgetId, originalReceipt.Change.Balance.Revision),
            originalReceipt.Command.AllocationId, new(7, new(60, 0)))));
        var replay = await runtime.CommitAsync(reservation);
        Assert.Equal(originalReceipt, replay.DecisionBudget);
        Assert.Equal(settled.State.UpdatedAtUtc, replay.State.UpdatedAtUtc);
        Assert.Equal(60, (await BalanceAsync(reopened, current)).ConsumedWorkTicks);
        Assert.Equal(0, (await BalanceAsync(reopened, current)).ReservedWorkTicks);
        Assert.Equal(originalReceipt, (await runtime.FindCompletedCommandAsync(reservation.CommandId))!.DecisionBudget);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Reusing_runtime_command_cannot_change_remove_or_add_accounting(int change) {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var owner = new EfProcessRuntimeUnitOfWork(context);
        var state = (await owner.CommitAsync(Initial())).State;
        var balance = await BalanceAsync(context, state);
        var command = Reserve(state, balance, 100);
        var request = Advance(state, change == 3 ? null : command);
        var first = await owner.CommitAsync(request);
        ProcessDecisionBudgetCommand? replacement = change switch {
            0 => command with { Grant = new(101, 0) },
            1 => command with { Expectation = command.Expectation with { Revision = 2 } },
            2 => null,
            _ => command
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.CommitAsync(request with { DecisionBudget = replacement }));
        Assert.Equal(first.State.UpdatedAtUtc, (await owner.LoadAsync(state.RunId))!.UpdatedAtUtc);
        Assert.Equal(change == 3 ? 0 : 1, (await BalanceAsync(context, state)).AllocationCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Failure_after_SQL_flush_rolls_back_both_accounting_and_runtime_and_retry_is_safe(bool initialCommit) {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var owner = new EfProcessRuntimeUnitOfWork(context);
        var request = Initial();
        DecisionBudgetBalance? before = null;
        if (!initialCommit) {
            var state = (await owner.CommitAsync(request)).State;
            before = await BalanceAsync(context, state);
            request = Advance(state, Reserve(state, before, 100));
        }
        var failure = new InvalidOperationException("Injected failure after the owning SQL flush.");
        var fault = new AfterFlushFault(failure);
        await using (var broken = Context(app.Services, fault)) {
            Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => new EfProcessRuntimeUnitOfWork(broken).CommitAsync(request)));
            Assert.True(fault.Flushed);
        }
        var observed = await new EfProcessDecisionBudgetStore(context).ReadAsync(request.OriginalState.RunId, request.OriginalState.Steps.Single().StepInstanceId);
        Assert.Equal(before, observed);
        Assert.Empty(await context.DecisionBudgetAllocations.ToArrayAsync());
        Assert.Null(await owner.FindCompletedCommandAsync(request.CommandId));
        if (!initialCommit) {
            Assert.Equal(request.OriginalState.UpdatedAtUtc, (await owner.LoadAsync(request.OriginalState.RunId))!.UpdatedAtUtc);
        }
        Assert.True((await owner.CommitAsync(request)).CommittedNow);
    }

    [Fact]
    public async Task Competing_reservations_have_one_winner_and_cannot_spend_the_same_remaining_work() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var state = (await new EfProcessRuntimeUnitOfWork(context).CommitAsync(Initial())).State;
        var balance = await BalanceAsync(context, state);
        var commands = new[] { Advance(state, Reserve(state, balance, balance.Ceiling.MaximumWorkDuration.Ticks)),
            Advance(state, Reserve(state, balance, balance.Ceiling.MaximumWorkDuration.Ticks)) };
        var attempts = await Task.WhenAll(commands.Select(async request => {
            await using var contender = Context(app.Services);
            return await Record.ExceptionAsync(() => new EfProcessRuntimeUnitOfWork(contender).CommitAsync(request));
        }));
        Assert.Single(attempts, result => result is null);
        Assert.IsType<ProcessRuntimeOptimisticConcurrencyException>(Assert.Single(attempts, result => result is not null));
        var retained = await BalanceAsync(context, state);
        Assert.Equal(balance.Ceiling.MaximumWorkDuration.Ticks, retained.ReservedWorkTicks);
        Assert.Equal(1, retained.AllocationCount);
        Assert.Single(await context.DecisionBudgetAllocations.ToArrayAsync());
        var current = (await new EfProcessRuntimeUnitOfWork(context).LoadAsync(state.RunId))!;
        var exhausted = await Assert.ThrowsAsync<DecisionBudgetException>(() =>
            new EfProcessRuntimeUnitOfWork(context).CommitAsync(Advance(current, Reserve(current, retained, 1))));
        Assert.Equal(DecisionBudgetRefusal.WorkExhausted, exhausted.Reason);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Current_runtime_state_does_not_authorize_foreign_step_budget_or_stale_balance(int change) {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var owner = new EfProcessRuntimeUnitOfWork(context);
        var state = (await owner.CommitAsync(Initial())).State;
        var foreign = (await owner.CommitAsync(Initial())).State;
        var balance = await BalanceAsync(context, state);
        var other = await BalanceAsync(context, foreign);
        var command = Reserve(state, balance, 100);
        var expectation = command.Expectation;
        command = command with { Expectation = change switch {
            0 => expectation with { StepInstanceId = foreign.Steps.Single().StepInstanceId },
            1 => expectation with { BudgetId = other.BudgetId },
            _ => expectation with { Revision = expectation.Revision + 1 }
        } };
        if (change == 2) {
            var stale = await Assert.ThrowsAsync<ProcessDecisionException>(() => owner.CommitAsync(Advance(state, command)));
            Assert.Equal(ProcessDecisionRefusal.StaleExpectation, stale.Reason);
        } else {
            await Assert.ThrowsAsync<InvalidOperationException>(() => owner.CommitAsync(Advance(state, command)));
        }
        Assert.Equal(balance, await BalanceAsync(context, state));
        Assert.Equal(other, await BalanceAsync(context, foreign));
        Assert.Empty(await context.DecisionBudgetAllocations.ToArrayAsync());
    }

    [Fact]
    public async Task Closed_overrun_stays_charged_after_cancellation_and_other_children_can_still_settle() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var owner = new EfProcessRuntimeUnitOfWork(context);
        var state = (await owner.CommitAsync(Initial())).State;
        var first = await owner.CommitAsync(Advance(state, Reserve(state, await BalanceAsync(context, state), 100)));
        var second = await owner.CommitAsync(Advance(first.State, Reserve(first.State, first.DecisionBudget!.Change.Balance, 100)));
        var cancelled = await ProcessRootCancellationFixture.StopAsync(owner, second.State);
        var balance = await BalanceAsync(context, state);
        var refused = await owner.CommitAsync(Advance(cancelled, Reserve(state, balance, 1)));
        Assert.Contains(refused.Diagnostics, item => item.Code == EfProcessRootCancellationGuard.WorkAdmissionStopped);
        Assert.False(refused.Succeeded);
        var overrun = await owner.CommitAsync(Advance(cancelled, Settle(state, balance, first.DecisionBudget!.Command.AllocationId, 101)));
        Assert.True(overrun.DecisionBudget!.Change.Balance.OverrunDetected);
        var settled = await owner.CommitAsync(Advance(overrun.State, Settle(state, overrun.DecisionBudget.Change.Balance, second.DecisionBudget!.Command.AllocationId, 70)));
        Assert.True(settled.DecisionBudget!.Change.Balance.OverrunDetected);
        Assert.Equal(171, settled.DecisionBudget.Change.Balance.ConsumedWorkTicks);
        Assert.Equal(0, settled.DecisionBudget.Change.Balance.ReservedWorkTicks);
        Assert.Equal(2, settled.DecisionBudget.Change.Balance.AllocationCount);
        Assert.Equal(ProcessRuntimeStatus.CancelRequested, settled.State.Status);
        var finalized = await new ProcessRuntimeEngine(owner).FinalizeRootCancellationAsync(settled.State, ProcessRootCancellationFixture.Command(settled.State));
        Assert.True(finalized.Succeeded);
        Assert.Equal(ProcessRuntimeStatus.Cancelled, finalized.State.Status);
    }

    [Fact]
    public async Task Held_root_commit_does_not_serialize_an_unrelated_root() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var owner = new EfProcessRuntimeUnitOfWork(context);
        var state = (await owner.CommitAsync(Initial())).State;
        var other = (await owner.CommitAsync(Initial())).State;
        var firstRequest = Advance(state, Reserve(state, await BalanceAsync(context, state), 100));
        var otherRequest = Advance(other, Reserve(other, await BalanceAsync(context, other), 100));
        var pause = new BeforeCommitPause();
        await using var held = Context(app.Services, pause);
        var pending = new EfProcessRuntimeUnitOfWork(held).CommitAsync(firstRequest);
        try {
            await pause.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var independent = await owner.CommitAsync(otherRequest).WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(independent.Succeeded);
            Assert.False(pending.IsCompleted);
            Assert.Equal(0, (await BalanceAsync(context, state)).AllocationCount);
        } finally {
            pause.Release.TrySetResult(true);
            await pending.WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.Equal(1, (await BalanceAsync(context, state)).AllocationCount);
    }

    [Fact]
    public async Task Allocation_replay_and_settlement_use_exact_indexed_reads_without_loading_siblings() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var owner = new EfProcessRuntimeUnitOfWork(context);
        var state = (await owner.CommitAsync(Initial())).State;
        var first = await owner.CommitAsync(Advance(state, Reserve(state, await BalanceAsync(context, state), 100)));
        var second = await owner.CommitAsync(Advance(first.State, Reserve(first.State, first.DecisionBudget!.Change.Balance, 100)));
        var probe = new AccountingReadProbe();
        await using var observed = Context(app.Services, probe);
        var replayCommand = ((ProcessDecisionBudgetReservation)first.DecisionBudget!.Command) with {
            Expectation = first.DecisionBudget.Command.Expectation with { Revision = second.DecisionBudget!.Change.Balance.Revision }
        };
        var replay = await new EfProcessRuntimeUnitOfWork(observed).CommitAsync(Advance(second.State, replayCommand));
        Assert.False(replay.DecisionBudget!.Change.Changed);
        Assert.Equal(second.DecisionBudget.Change.Balance, replay.DecisionBudget.Change.Balance);
        Assert.Equal(2, probe.Reads.Count);
        Assert.All(probe.Reads, sql => Assert.Contains("WHERE", sql, StringComparison.Ordinal));
        Assert.Contains(probe.Reads, sql => sql.Contains("\"AllocationId\" =", StringComparison.Ordinal));
        var settlement = Settle(replay.State, replay.DecisionBudget.Change.Balance, first.DecisionBudget.Command.AllocationId, 60);
        var closed = await owner.CommitAsync(Advance(replay.State, settlement));
        var duplicate = await owner.CommitAsync(Advance(closed.State, settlement with {
            Expectation = settlement.Expectation with { Revision = closed.DecisionBudget!.Change.Balance.Revision }
        }));
        Assert.False(duplicate.DecisionBudget!.Change.Changed);
        Assert.Equal(100, duplicate.DecisionBudget.Change.Balance.ReservedWorkTicks);
        Assert.Equal(60, duplicate.DecisionBudget.Change.Balance.ConsumedWorkTicks);
    }

    [Fact]
    public async Task Lost_commit_acknowledgement_recovers_the_exact_reservation_without_spending_twice() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var owner = new EfProcessRuntimeUnitOfWork(context);
        var state = (await owner.CommitAsync(Initial())).State;
        var request = Advance(state, Reserve(state, await BalanceAsync(context, state), 100));
        var failure = new InvalidOperationException("Injected acknowledgement failure after commit.");
        await using (var interrupted = Context(app.Services, new AfterCommitFault(failure))) {
            Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => new EfProcessRuntimeUnitOfWork(interrupted).CommitAsync(request)));
        }
        var replay = await owner.CommitAsync(request);
        Assert.False(replay.CommittedNow);
        Assert.False((await owner.FindCompletedCommandAsync(request.CommandId))!.CommittedNow);
        Assert.Equal(100, replay.DecisionBudget!.Change.Balance.ReservedWorkTicks);
        Assert.Equal(1, replay.DecisionBudget.Change.Balance.AllocationCount);
        Assert.Equal(replay.DecisionBudget.Change.Balance, await BalanceAsync(context, state));
        Assert.Single(await context.DecisionBudgetAllocations.ToArrayAsync());
    }

    [Fact]
    public async Task Changed_closed_evidence_cannot_rewrite_a_settlement_or_refund_twice() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var owner = new EfProcessRuntimeUnitOfWork(context);
        var state = (await owner.CommitAsync(Initial())).State;
        var reservation = await owner.CommitAsync(Advance(state, Reserve(state, await BalanceAsync(context, state), 100)));
        var command = Settle(state, reservation.DecisionBudget!.Change.Balance, reservation.DecisionBudget.Command.AllocationId, 60);
        var settled = await owner.CommitAsync(Advance(reservation.State, command));
        var balance = settled.DecisionBudget!.Change.Balance;
        var changed = command with { Expectation = command.Expectation with { Revision = balance.Revision }, Settlement = new(2, new(0, 0)) };
        var refusal = await Assert.ThrowsAsync<DecisionBudgetException>(() => owner.CommitAsync(Advance(settled.State, changed)));
        Assert.Equal(DecisionBudgetRefusal.OriginalSettlementChanged, refusal.Reason);
        Assert.Equal(balance, await BalanceAsync(context, state));
    }

    [Fact]
    public async Task Missing_original_ledger_is_not_reconstructed_from_the_assignment_or_current_policy() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var owner = new EfProcessRuntimeUnitOfWork(context);
        var state = (await owner.CommitAsync(Initial())).State;
        var balance = await BalanceAsync(context, state);
        await context.DecisionBudgets.Where(item => item.BudgetId == balance.BudgetId.Value).ExecuteDeleteAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.CommitAsync(Advance(state, Reserve(state, balance, 100))));
        Assert.Empty(await context.DecisionBudgets.ToArrayAsync());
        Assert.Equal(state.UpdatedAtUtc, (await owner.LoadAsync(state.RunId))!.UpdatedAtUtc);
    }

    [Fact]
    public async Task Accounting_migration_round_trips_an_empty_ledger_without_rewriting_legacy_runtime() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var initial = Initial(false);
        var owner = new EfProcessRuntimeUnitOfWork(context);
        var before = await owner.CommitAsync(initial);
        await ApplyMigrationAsync(context, downgrade: true);
        await ApplyMigrationAsync(context, downgrade: false);
        Assert.True((await owner.CommitAsync(initial)).Succeeded);
        Assert.Equal(before.State.UpdatedAtUtc, (await owner.LoadAsync(before.State.RunId))!.UpdatedAtUtc);
        Assert.Empty(await context.DecisionBudgets.ToArrayAsync());
        Assert.Empty(await context.DecisionBudgetAllocations.ToArrayAsync());
    }

    [Fact]
    public async Task Downgrade_refuses_retained_accounting_before_dropping_any_evidence() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var owner = new EfProcessRuntimeUnitOfWork(context);
        var state = (await owner.CommitAsync(Initial())).State;
        var request = Advance(state, Reserve(state, await BalanceAsync(context, state), 100));
        var reservation = await owner.CommitAsync(request);
        var error = await Assert.ThrowsAsync<PostgresException>(() => ApplyMigrationAsync(context, downgrade: true));
        Assert.Equal(PostgresErrorCodes.RaiseException, error.SqlState);
        Assert.Equal(reservation.DecisionBudget!.Change.Balance, await BalanceAsync(context, state));
        Assert.Single(await context.DecisionBudgetAllocations.ToArrayAsync());
        Assert.Equal(reservation.DecisionBudget, (await owner.FindCompletedCommandAsync(request.CommandId))!.DecisionBudget);
    }

    private static Task ApplyMigrationAsync(ProcessPersistenceDbContext context, bool downgrade)
        => ProcessDecisionMigrationFixture.ApplyAsync(context, downgrade,
            new CanDoItAll.Migrations.PostgreSql.Migrations.AddProcessDecisionBudgets(),
            new CanDoItAll.Migrations.PostgreSql.Migrations.AddProcessDecisionGenerations(),
            new CanDoItAll.Migrations.PostgreSql.Migrations.AddProcessDecisionMaterials(),
            new CanDoItAll.Migrations.PostgreSql.Migrations.AddProcessSharedMaterialContracts(),
            new CanDoItAll.Migrations.PostgreSql.Migrations.AddProcessRunDecisionFunding(),
            new CanDoItAll.Migrations.PostgreSql.Migrations.AddProcessSharedRunMaterialScopes());

    private static ProcessRuntimeCommitRequest Initial(bool admitted = true) {
        var request = ProcessProjectAdmissionFixture.Initial();
        var assignment = request.InitialAssignments!.Single();
        return request with { InitialAssignments = [assignment with { DecisionDataPolicy = admitted
            ? ProcessDecisionPolicyFixture.Create(request.InitialPlan!.Definition.VersionId, assignment.StepInstanceId) : null }] };
    }

    private static ProcessRuntimeCommitRequest Advance(ProcessRuntimeStateSnapshot state, ProcessDecisionBudgetCommand? command)
        => new(RuntimeCommandId.New(), state, new(ProcessRuntimeTransitionOutcome.Applied,
            state with { UpdatedAtUtc = state.UpdatedAtUtc.AddSeconds(1) }, [], [], [], [])) { DecisionBudget = command };

    private static ProcessDecisionBudgetReservation Reserve(ProcessRuntimeStateSnapshot state, DecisionBudgetBalance balance, long workTicks)
        => new(new(state.Steps.Single().StepInstanceId, balance.BudgetId, balance.Revision), new(Guid.NewGuid()), new(workTicks, 0));

    private static ProcessDecisionBudgetSettlement Settle(ProcessRuntimeStateSnapshot state, DecisionBudgetBalance balance,
        DecisionBudgetAllocationId allocationId, long workTicks)
        => new(new(state.Steps.Single().StepInstanceId, balance.BudgetId, balance.Revision), allocationId, new(1, new(workTicks, 0)));

    private static async Task<DecisionBudgetBalance> BalanceAsync(ProcessPersistenceDbContext context, ProcessRuntimeStateSnapshot state)
        => (await new EfProcessDecisionBudgetStore(context).ReadAsync(state.RunId, state.Steps.Single().StepInstanceId))!;

    private static ProcessPersistenceDbContext Context(IServiceProvider services, params IInterceptor[] interceptors) {
        var options = new DbContextOptionsBuilder<ProcessPersistenceDbContext>();
        AppDbContextOptionsConfigurator.Configure(options, services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile);
        return new(options.AddInterceptors(interceptors).Options);
    }

    private sealed class AfterFlushFault(Exception failure) : SaveChangesInterceptor {
        public bool Flushed { get; private set; }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default) {
            Flushed = result > 0 && eventData.Context!.Database.CurrentTransaction is not null;
            throw failure;
        }
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

    private sealed class AfterCommitFault(Exception failure) : DbTransactionInterceptor {
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default) => throw failure;
    }

    private sealed class AccountingReadProbe : DbCommandInterceptor {
        public ConcurrentQueue<string> Reads { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            if (command.CommandText.StartsWith("SELECT", StringComparison.Ordinal) && command.CommandText.Contains("FROM process_decision_budget", StringComparison.Ordinal)) {
                Reads.Enqueue(command.CommandText);
            }
            return ValueTask.FromResult(result);
        }
    }
}
