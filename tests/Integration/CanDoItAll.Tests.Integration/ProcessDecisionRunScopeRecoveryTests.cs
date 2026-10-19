using System.Data.Common;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionRunScopeFixture;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed class ProcessDecisionRunScopeRecoveryTests {
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task SQL_failure_rolls_back_scope_capture_budget_and_runtime_together(int stage) {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var initial = Initial(app.Services);
        var request = initial;
        ProcessDecisionRunScopeState? before = null;
        if (stage > 0) {
            var state = (await Owner(context, app.Services).CommitAsync(initial)).State;
            before = (await Scopes(context, app.Services).ReadAsync(initial.InitialRunDecisionAdmission!.MaterialScope!.ScopeId))!;
            request = await BeginRequestAsync(context, state, before);
            if (stage == 2) {
                var begun = await Owner(context, app.Services).CommitAsync(request);
                before = begun.DecisionRunScope!.State;
                request = await CompleteRequestAsync(context, begun.State, before);
            }
        }
        var root = await new EfProcessDecisionBudgetStore(context).ReadAsync(initial.OriginalState.RunId, null);
        var fault = new AfterFlush();
        await using (var failing = Context(app.Services, fault)) {
            Assert.Same(fault.Failure, await Assert.ThrowsAsync<InvalidOperationException>(() => Owner(failing, app.Services).CommitAsync(request)));
        }
        Assert.True(fault.Flushed);
        Assert.Equal(before, await Scopes(context, app.Services).ReadAsync(initial.InitialRunDecisionAdmission!.MaterialScope!.ScopeId));
        Assert.Equal(root, await new EfProcessDecisionBudgetStore(context).ReadAsync(initial.OriginalState.RunId, null));
        Assert.Empty(await context.DecisionMaterials.ToArrayAsync());
        Assert.Null(await Owner(context, app.Services).FindCompletedCommandAsync(request.CommandId));
        Assert.True((await Owner(context, app.Services).CommitAsync(request)).Succeeded);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Lost_capture_acknowledgement_recovers_the_exact_original_scope_and_charge(bool completion) {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var owner = Owner(context, app.Services);
        var initial = Initial(app.Services);
        var state = (await owner.CommitAsync(initial)).State;
        var scope = (await Scopes(context, app.Services).ReadAsync(initial.InitialRunDecisionAdmission!.MaterialScope!.ScopeId))!;
        var request = await BeginRequestAsync(context, state, scope);
        if (completion) {
            var begun = await owner.CommitAsync(request);
            request = await CompleteRequestAsync(context, begun.State, begun.DecisionRunScope!.State);
        }
        var failure = new InvalidOperationException("Injected lost shared capture acknowledgement.");
        await using (var interrupted = Context(app.Services, new AfterCommit(failure))) {
            Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => Owner(interrupted, app.Services).CommitAsync(request)));
        }
        var original = (await owner.FindCompletedCommandAsync(request.CommandId))!;
        var replay = await owner.CommitAsync(request);
        Assert.Equal(original.DecisionRunScope, replay.DecisionRunScope);
        Assert.Equal(original.DecisionBudget, replay.DecisionBudget);
        Assert.Equal(1, (await new EfProcessDecisionBudgetStore(context).ReadAsync(state.RunId, null))!.AllocationCount);
        Assert.Equal(completion ? 1 : 0, await context.DecisionMaterials.CountAsync());
    }

    [Fact]
    public async Task Actual_restart_recovers_pending_capture_without_reserving_or_reading_again() {
        await using var environment = CanDoItAllTestEnvironment.Create("process-decision-budget");
        var profile = environment.CreatePostgreSqlProfile("shared-scope-restart");
        var harness = new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = profile };
        ProcessRuntimeCommitRequest initial;
        ProcessRuntimeCommitRequest begin;
        ProcessDecisionRunScopeReceipt original;
        await using (var app = await TestApplication.CreateAsync(harness)) {
            await using var context = Context(app.Services);
            initial = Initial(app.Services);
            var state = (await Owner(context, app.Services).CommitAsync(initial)).State;
            var scope = (await Scopes(context, app.Services).ReadAsync(initial.InitialRunDecisionAdmission!.MaterialScope!.ScopeId))!;
            begin = await BeginRequestAsync(context, state, scope);
            original = (await Owner(context, app.Services).CommitAsync(begin)).DecisionRunScope!;
        }
        await using var restarted = await TestApplication.CreateAsync(harness);
        await using var reopened = Context(restarted.Services);
        var owner = Owner(reopened, restarted.Services);
        Assert.Equal(original, (await owner.CommitAsync(begin)).DecisionRunScope);
        var stateAfterRestart = (await owner.LoadAsync(initial.OriginalState.RunId))!;
        var completed = await owner.CommitAsync(await CompleteRequestAsync(reopened, stateAfterRestart, original.State));
        Assert.Equal(original.State.Capture!.MaterialId, completed.DecisionRunScope!.State.Material!.Stamp.Reference.MaterialId);
        Assert.Equal(1, completed.DecisionBudget!.Change.Balance.AllocationCount);
        Assert.True((await owner.CommitAsync(initial)).Succeeded);
    }

    [Fact]
    public async Task Explicit_abort_records_overrun_and_retains_retired_allocation_capacity() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var owner = Owner(context, app.Services);
        var initial = Initial(app.Services);
        var state = (await owner.CommitAsync(initial)).State;
        var scope = (await Scopes(context, app.Services).ReadAsync(initial.InitialRunDecisionAdmission!.MaterialScope!.ScopeId))!;
        var begun = await owner.CommitAsync(await BeginRequestAsync(context, state, scope));
        var completion = await CompleteRequestAsync(context, begun.State, begun.DecisionRunScope!.State, 11);
        Assert.Equal(DecisionBudgetRefusal.Overrun, (await Assert.ThrowsAsync<DecisionBudgetException>(() => owner.CommitAsync(completion))).Reason);
        var aborted = await owner.CommitAsync(ScopeRequest(begun.State,
            new(begun.DecisionRunScope.State.Expectation, ProcessDecisionRunScopeCommandKind.AbortCapture), completion.DecisionBudget));
        Assert.Equal(ProcessDecisionRunScopePhase.AwaitingCapture, aborted.DecisionRunScope!.State.Phase);
        Assert.True(aborted.DecisionBudget!.Change.Balance.OverrunDetected);
        Assert.Equal(11, aborted.DecisionBudget.Change.Balance.ConsumedWorkTicks);
        Assert.Equal(1, aborted.DecisionBudget.Change.Balance.AllocationCount);
        Assert.Equal(0, aborted.DecisionBudget.Change.Balance.ReservedWorkTicks);
        Assert.Empty(await context.DecisionMaterials.ToArrayAsync());
        var retry = await BeginRequestAsync(context, aborted.State, aborted.DecisionRunScope.State);
        await Assert.ThrowsAsync<DecisionBudgetException>(() => owner.CommitAsync(retry));
    }

    [Fact]
    public async Task Unknown_direct_root_work_blocks_refresh_until_its_actual_closed_usage_is_retained() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var owner = Owner(context, app.Services);
        var initial = Initial(app.Services);
        var state = (await owner.CommitAsync(initial)).State;
        var captured = await CaptureAsync(context, app.Services, state, (await Scopes(context, app.Services).ReadAsync(initial.InitialRunDecisionAdmission!.MaterialScope!.ScopeId))!);
        var balance = captured.DecisionBudget!.Change.Balance;
        var unknown = await owner.CommitAsync(Request(captured.State, null,
            new ProcessDecisionBudgetReservation(new(null, balance.BudgetId, balance.Revision), new(Guid.NewGuid()), new(0, 1))));
        var frozen = await owner.CommitAsync(ScopeRequest(unknown.State,
            new(captured.DecisionRunScope!.State.Expectation, ProcessDecisionRunScopeCommandKind.BeginReconciliation)));
        var begin = await BeginRequestAsync(context, frozen.State, frozen.DecisionRunScope!.State, ProcessDecisionRunCaptureKind.ManagerRefresh);
        Assert.Equal(ProcessDecisionRunScopeRefusal.UnclosedWork,
            (await Assert.ThrowsAsync<ProcessDecisionRunScopeException>(() => owner.CommitAsync(begin))).Reason);
        balance = unknown.DecisionBudget!.Change.Balance;
        var closed = await owner.CommitAsync(Request(frozen.State, null, new ProcessDecisionBudgetSettlement(new(null, balance.BudgetId, balance.Revision),
            unknown.DecisionBudget.Command.AllocationId, new(3, new(0, 0)))));
        var refreshed = await CaptureAsync(context, app.Services, closed.State, frozen.DecisionRunScope.State, ProcessDecisionRunCaptureKind.ManagerRefresh);
        Assert.Equal(2, refreshed.DecisionRunScope!.State.Material!.Stamp.Reference.Generation);
        Assert.Equal(3, refreshed.DecisionBudget!.Change.Balance.AllocationCount);
    }

    [Fact]
    public async Task Root_freeze_wins_over_a_waiting_descendant_join_without_blocking_another_root() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var owner = Owner(context, app.Services);
        var initial = Initial(app.Services);
        var state = (await owner.CommitAsync(initial)).State;
        var captured = await CaptureAsync(context, app.Services, state, (await Scopes(context, app.Services).ReadAsync(initial.InitialRunDecisionAdmission!.MaterialScope!.ScopeId))!);
        var parent = (await owner.CommitAsync(Claim(captured.State))).State;
        var descendant = (await owner.CommitAsync(Descendant(parent, initial.InitialRunDecisionAdmission!))).State;
        var join = await JoinRequestAsync(context, app.Services, descendant, captured.DecisionRunScope!.State);
        var second = Initial(app.Services);
        var independent = (await owner.CommitAsync(second)).State;
        var independentScope = (await Scopes(context, app.Services).ReadAsync(second.InitialRunDecisionAdmission!.MaterialScope!.ScopeId))!;
        var independentRequest = await BeginRequestAsync(context, independent, independentScope);
        var pause = new BeforeCommit();
        await using var held = Context(app.Services, pause);
        await using var contender = Context(app.Services);
        var frozen = Owner(held, app.Services).CommitAsync(ScopeRequest(parent,
            new(captured.DecisionRunScope.State.Expectation, ProcessDecisionRunScopeCommandKind.BeginReconciliation)));
        Task<ProcessRuntimeCommitResult>? waiting = null;
        try {
            await pause.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            waiting = Owner(contender, app.Services).CommitAsync(join);
            var progressed = await owner.CommitAsync(independentRequest).WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(ProcessDecisionRunScopePhase.Capturing, progressed.DecisionRunScope!.State.Phase);
            Assert.False(waiting.IsCompleted);
            Assert.False(frozen.IsCompleted);
        } finally {
            pause.Release.TrySetResult();
            await frozen.WaitAsync(TimeSpan.FromSeconds(10));
            if (waiting is not null) {
                Assert.Equal(ProcessDecisionRunScopeRefusal.StaleExpectation,
                    (await Assert.ThrowsAsync<ProcessDecisionRunScopeException>(() => waiting.WaitAsync(TimeSpan.FromSeconds(10)))).Reason);
            }
        }
        Assert.Empty(await context.DecisionRunScopeMembers.ToArrayAsync());
        Assert.Equal(10, (await BudgetAsync(context, descendant)).ReservedWorkTicks);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Downgrade_refuses_original_scope_before_and_after_capture(bool capture) {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var initial = Initial(app.Services);
        var state = (await Owner(context, app.Services).CommitAsync(initial)).State;
        var scope = (await Scopes(context, app.Services).ReadAsync(initial.InitialRunDecisionAdmission!.MaterialScope!.ScopeId))!;
        if (capture) {
            scope = (await CaptureAsync(context, app.Services, state, scope)).DecisionRunScope!.State;
        }
        var failure = await Assert.ThrowsAsync<PostgresException>(() => MigrateAsync(context, true));
        Assert.Equal(PostgresErrorCodes.RaiseException, failure.SqlState);
        Assert.Equal(scope, await Scopes(context, app.Services).ReadAsync(scope.Admission.ScopeId));
    }

    [Fact]
    public async Task Migration_roundtrip_preserves_legacy_funding_without_inventing_shared_material_authority() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var request = Initial(app.Services);
        request = request with { InitialRunDecisionAdmission = request.InitialRunDecisionAdmission! with { MaterialScope = null },
            InitialAssignments = request.InitialAssignments!.Select(item => item with { DecisionDataPolicy = item.DecisionDataPolicy! with { SharedRunScopeId = null } }).ToArray() };
        var state = (await Owner(context, app.Services).CommitAsync(request)).State;
        var before = await BudgetAsync(context, state);
        await MigrateAsync(context, true);
        await MigrateAsync(context, false);
        Assert.Equal(before, await BudgetAsync(context, state));
        Assert.Empty(await context.DecisionRunScopes.ToArrayAsync());
        Assert.Empty(await context.DecisionRunScopeMembers.ToArrayAsync());
        Assert.True((await Owner(context, app.Services).CommitAsync(request)).Succeeded);
    }

    internal static Task MigrateAsync(ProcessPersistenceDbContext context, bool down)
        => ProcessDecisionMigrationFixture.ApplyAsync(context, down, new CanDoItAll.Migrations.PostgreSql.Migrations.AddProcessSharedRunMaterialScopes());

    private sealed class AfterFlush : SaveChangesInterceptor {
        internal InvalidOperationException Failure { get; } = new("Injected failure after shared scope SQL flush.");
        internal bool Flushed { get; private set; }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default) {
            Flushed = result > 0 && eventData.Context!.Database.CurrentTransaction is not null;
            throw Failure;
        }
    }

    private sealed class AfterCommit(Exception failure) : DbTransactionInterceptor {
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
            => throw failure;
    }

    private sealed class BeforeCommit : DbTransactionInterceptor {
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData,
            InterceptionResult result, CancellationToken cancellationToken = default) {
            Entered.TrySetResult();
            await Release.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            return result;
        }
    }
}
