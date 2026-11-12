using System.Data.Common;
using System.Diagnostics;
using System.Text.Json;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit.Abstractions;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;
using static CanDoItAll.Tests.Integration.Processes.ProcessRootCancellationFixture;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed class ProcessRootQuiescenceTests(ITestOutputHelper output) {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Quiescent_root_cancellation_retains_original_command_replay(bool direct) {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var owner = new EfProcessRuntimeUnitOfWork(context);
        var initial = ProcessProjectAdmissionFixture.Initial();
        var state = (await owner.CommitAsync(initial)).State;
        if (!direct) {
            state = await StopAsync(owner, state);
        }
        var engine = new ProcessRuntimeEngine(owner);
        var result = direct ? await engine.RequestCancellationAsync(state, Command(state))
            : await engine.FinalizeRootCancellationAsync(state, Command(state));
        Assert.True(result.CommittedNow);
        Assert.Equal(ProcessRuntimeStatus.Cancelled, result.State.Status);
        var before = await RetainedAsync(context, result.State);
        var replay = await owner.CommitAsync(initial);
        Assert.False(replay.CommittedNow);
        Assert.Equal(ProcessRuntimeStatus.Cancelled, replay.State.Status);
        Assert.Equal(before, await RetainedAsync(context, result.State));
    }

    [Theory]
    [InlineData(DecisionWork.Active)]
    [InlineData(DecisionWork.WaitingForReview)]
    [InlineData(DecisionWork.Reconciling)]
    [InlineData(DecisionWork.MissingCurrent)]
    [InlineData(DecisionWork.SupersededCurrent)]
    [InlineData(DecisionWork.LegacyCurrentChild)]
    [InlineData(DecisionWork.LegacyHistoricalChild)]
    public async Task Current_and_retained_decision_work_prevent_finalization_without_mutation_residue(DecisionWork work) {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var owner = new EfProcessRuntimeUnitOfWork(context);
        var state = (await StartAsync(context)).State;
        if (work is DecisionWork.LegacyCurrentChild or DecisionWork.LegacyHistoricalChild) {
            var enrolled = await EnrollAsync(context, state);
            state = (await CloseAsync(context, enrolled.State, enrolled.Decision!.Child!, NoEffect(), 5)).State;
            state = (await PhaseAsync(context, state, ProcessDecisionPhaseChange.BeginReconciliation)).State;
            if (work == DecisionWork.LegacyHistoricalChild) {
                state = (await RefreshAsync(context, state, ProcessDecisionRefreshKind.ManagerRefresh)).State;
                state = (await PhaseAsync(context, state, ProcessDecisionPhaseChange.BeginReconciliation)).State;
            }
            state = (await PhaseAsync(context, state, ProcessDecisionPhaseChange.Cancel)).State;
            var child = await context.DecisionChildren.SingleAsync();
            child.OwnerRevision = null;
            child.Outcome = null;
            child.AcceptedReceiptId = null;
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();
        } else if (work is DecisionWork.MissingCurrent or DecisionWork.SupersededCurrent) {
            var generation = await context.DecisionGenerations.SingleAsync();
            if (work == DecisionWork.MissingCurrent) {
                context.DecisionGenerations.Remove(generation);
            } else {
                generation.Phase = ProcessDecisionPhase.Superseded;
            }
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();
        } else if (work != DecisionWork.Active) {
            state = (await PhaseAsync(context, state, work == DecisionWork.WaitingForReview
                ? ProcessDecisionPhaseChange.WaitForReview : ProcessDecisionPhaseChange.BeginReconciliation)).State;
        }
        state = await StopAsync(owner, state);
        await AssertPendingAsync(context, owner, state, ProcessRootCancellationPending.Decisions);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Original_unsettled_allocations_block_even_zero_work_or_inconsistent_legacy_reserves(bool replanOnly, bool legacyZeroReserves) {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var owner = new EfProcessRuntimeUnitOfWork(context);
        var state = (await owner.CommitAsync(LocalBudget())).State;
        var budget = await BudgetAsync(context, state);
        var reserved = await owner.CommitAsync(Request(state, null, new ProcessDecisionBudgetReservation(
            new(Step(state), budget.BudgetId, budget.Revision), new(Guid.NewGuid()), replanOnly ? new(0, 1) : new(20, 0))));
        state = await StopAsync(owner, reserved.State);
        if (legacyZeroReserves) {
            var row = await context.DecisionBudgets.SingleAsync();
            row.ReservedWorkTicks = 0;
            row.ReservedReplans = 0;
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();
        }
        await AssertPendingAsync(context, owner, state, ProcessRootCancellationPending.Budget);
        if (legacyZeroReserves) {
            return;
        }
        budget = reserved.DecisionBudget!.Change.Balance;
        var settled = await owner.CommitAsync(Request(state, null, new ProcessDecisionBudgetSettlement(
            new(Step(state), budget.BudgetId, budget.Revision), reserved.DecisionBudget.Command.AllocationId,
            new(7, replanOnly ? new(0, 1) : new(5, 0)))));
        Assert.True(settled.CommittedNow);
        Assert.True((await new ProcessRuntimeEngine(owner).FinalizeRootCancellationAsync(settled.State, Command(settled.State))).CommittedNow);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Shared_scope_must_finish_its_original_lifecycle_before_the_root(bool captured) {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var owner = ProcessDecisionRunScopeFixture.Owner(context, app.Services);
        var initial = ProcessDecisionRunScopeFixture.Initial(app.Services);
        var state = (await owner.CommitAsync(initial)).State;
        var scope = (await ProcessDecisionRunScopeFixture.Scopes(context, app.Services)
            .ReadAsync(initial.InitialRunDecisionAdmission!.MaterialScope!.ScopeId))!;
        if (captured) {
            var result = await ProcessDecisionRunScopeFixture.CaptureAsync(context, app.Services, state, scope);
            state = result.State;
            scope = result.DecisionRunScope!.State;
        }
        state = await StopAsync(owner, state);
        await AssertPendingAsync(context, owner, state, ProcessRootCancellationPending.SharedScope);
        if (captured) {
            var result = await owner.CommitAsync(ProcessDecisionRunScopeFixture.ScopeRequest(state,
                new(scope.Expectation, ProcessDecisionRunScopeCommandKind.BeginReconciliation)));
            state = result.State;
            scope = result.DecisionRunScope!.State;
        }
        var closed = await owner.CommitAsync(ProcessDecisionRunScopeFixture.ScopeRequest(state,
            new(scope.Expectation, ProcessDecisionRunScopeCommandKind.Cancel)));
        Assert.True(closed.CommittedNow);
        Assert.True((await new ProcessRuntimeEngine(owner).FinalizeRootCancellationAsync(closed.State, Command(closed.State))).CommittedNow);
    }

    [Fact]
    public async Task Interrupted_capture_requires_original_measured_abort_before_shared_scope_and_root_close() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var owner = ProcessDecisionRunScopeFixture.Owner(context, app.Services);
        var initial = ProcessDecisionRunScopeFixture.Initial(app.Services);
        var state = (await owner.CommitAsync(initial)).State;
        var scope = (await ProcessDecisionRunScopeFixture.Scopes(context, app.Services)
            .ReadAsync(initial.InitialRunDecisionAdmission!.MaterialScope!.ScopeId))!;
        var begun = await owner.CommitAsync(await ProcessDecisionRunScopeFixture.BeginRequestAsync(context, state, scope));
        state = await StopAsync(owner, begun.State);
        scope = begun.DecisionRunScope!.State;
        await AssertPendingAsync(context, owner, state, ProcessRootCancellationPending.Budget | ProcessRootCancellationPending.SharedScope);
        var completion = await ProcessDecisionRunScopeFixture.CompleteRequestAsync(context, state, scope);
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.CommitAsync(completion));
        var aborted = await owner.CommitAsync(ProcessDecisionRunScopeFixture.ScopeRequest(state,
            new(scope.Expectation, ProcessDecisionRunScopeCommandKind.AbortCapture), completion.DecisionBudget));
        Assert.True(aborted.CommittedNow);
        Assert.Equal(3, aborted.DecisionBudget!.Change.Balance.ConsumedWorkTicks);
        Assert.Empty(await context.DecisionMaterials.ToArrayAsync());
        var closed = await owner.CommitAsync(ProcessDecisionRunScopeFixture.ScopeRequest(aborted.State,
            new(aborted.DecisionRunScope!.State.Expectation, ProcessDecisionRunScopeCommandKind.Cancel)));
        Assert.True((await new ProcessRuntimeEngine(owner).FinalizeRootCancellationAsync(closed.State, Command(closed.State))).CommittedNow);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(128)]
    [InlineData(1024)]
    public async Task Quiescence_is_one_root_scoped_scalar_query_without_payload_or_descendant_materialization(int descendants) {
        await using var app = await TestApplication.CreateAsync();
        var probe = new QueryProbe();
        await using var context = Context(app.Services, probe);
        var owner = new EfProcessRuntimeUnitOfWork(context);
        var state = (await owner.CommitAsync(ProcessProjectAdmissionFixture.Initial())).State;
        for (var index = 0; index < descendants; index++) {
            context.RuntimeStates.Add(ProcessPersistenceMappers.ToEntity(state with {
                RunId = ProcessRunId.New(), Status = ProcessRuntimeStatus.Cancelled, Steps = [], Claims = []
            }));
        }
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        probe.Enabled = true;
        for (var sample = -2; sample < 5; sample++) {
            var clock = Stopwatch.StartNew();
            Assert.Equal(ProcessRootCancellationPending.None, await EfProcessRootCancellationGuard.ReadPendingAsync(context, state.RunId));
            clock.Stop();
            if (sample >= 0) {
                output.WriteLine(JsonSerializer.Serialize(new { Workload = "root-quiescence-sql", Descendants = descendants, Sample = sample,
                    ElapsedMilliseconds = clock.Elapsed.TotalMilliseconds, SqlCommands = 1, ScalarColumns = probe.Columns }));
            }
        }
        probe.Enabled = false;
        Assert.Equal(7, probe.Commands);
        Assert.Equal(4, probe.Columns);
        Assert.Contains("EXISTS", probe.Sql, StringComparison.Ordinal);
        Assert.DoesNotContain("PayloadJson", probe.Sql, StringComparison.Ordinal);
        Assert.DoesNotContain("AdmissionJson", probe.Sql, StringComparison.Ordinal);
        Assert.Equal(ProcessRootCancellationPending.MissingRoot, await EfProcessRootCancellationGuard.ReadPendingAsync(context, ProcessRunId.New()));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => EfProcessRootCancellationGuard.ReadPendingAsync(context, default));
    }

    private static async Task AssertPendingAsync(ProcessPersistenceDbContext context, EfProcessRuntimeUnitOfWork owner,
        ProcessRuntimeStateSnapshot state, ProcessRootCancellationPending expected) {
        Assert.Equal(expected, await EfProcessRootCancellationGuard.ReadPendingAsync(context, state.RootRunId));
        var before = await RetainedAsync(context, state);
        var refused = await new ProcessRuntimeEngine(owner).FinalizeRootCancellationAsync(state, Command(state));
        Assert.False(refused.Succeeded);
        Assert.False(refused.CommittedNow);
        Assert.Contains(refused.Diagnostics, item => item.Code == EfProcessRootCancellationGuard.WorkPending);
        Assert.Equal(before, await RetainedAsync(context, state));
    }

    private sealed class QueryProbe : DbCommandInterceptor {
        internal bool Enabled { get; set; }
        internal int Commands { get; private set; }
        internal int Columns { get; private set; }
        internal string Sql { get; private set; } = string.Empty;

        public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default) {
            if (Enabled) {
                Commands++;
                Columns = result.FieldCount;
                Sql = command.CommandText;
            }
            return ValueTask.FromResult(result);
        }
    }

    public enum DecisionWork { Active, WaitingForReview, Reconciling, MissingCurrent, SupersededCurrent, LegacyCurrentChild, LegacyHistoricalChild }
}
