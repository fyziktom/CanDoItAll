using System.Data;
using System.Data.Common;
using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed partial class ProcessDecisionFundingPersistenceTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Failed_SQL_flush_rolls_back_root_local_and_runtime_together(bool initialCommit) {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var owner = Owner(context, app.Services);
        var request = Initial(app.Services);
        DecisionBudgetBalance? rootBefore = null;
        DecisionBudgetBalance? localBefore = null;
        if (!initialCommit) {
            var state = (await owner.CommitAsync(request)).State;
            rootBefore = await RootBudgetAsync(context, state);
            localBefore = await BudgetAsync(context, state);
            request = Change(state, Reserve(Step(state), localBefore, 100));
        }
        var fault = new AfterFlush();
        await using (var broken = Context(app.Services, fault)) {
            Assert.Same(fault.Failure, await Assert.ThrowsAsync<InvalidOperationException>(() => Owner(broken, app.Services).CommitAsync(request)));
        }
        Assert.True(fault.Flushed);
        Assert.Equal(rootBefore, await new EfProcessDecisionBudgetStore(context).ReadAsync(request.OriginalState.RootRunId, null));
        Assert.Equal(localBefore, await new EfProcessDecisionBudgetStore(context).ReadAsync(request.OriginalState.RunId, Step(request.OriginalState)));
        Assert.Empty(await context.DecisionBudgetAllocations.ToArrayAsync());
        Assert.Null(await owner.FindCompletedCommandAsync(request.CommandId));
        Assert.True((await owner.CommitAsync(request)).Succeeded);
    }

    [Fact]
    public async Task Lost_acknowledgement_recovers_both_original_receipts_without_funding_again() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var owner = Owner(context, app.Services);
        var state = (await owner.CommitAsync(Initial(app.Services))).State;
        var request = Change(state, Reserve(Step(state), await BudgetAsync(context, state), 100));
        var failure = new InvalidOperationException("Injected loss after the database accepted funded work.");
        await using (var interrupted = Context(app.Services, new AfterCommit(failure))) {
            Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => Owner(interrupted, app.Services).CommitAsync(request)));
        }
        var observed = (await owner.FindCompletedCommandAsync(request.CommandId))!;
        var replay = await owner.CommitAsync(request);
        Assert.Equal(observed.DecisionBudget, replay.DecisionBudget);
        Assert.NotNull(replay.DecisionBudget!.Funding);
        Assert.Equal(100, (await RootBudgetAsync(context, state)).ReservedWorkTicks);
        Assert.Equal(1, (await RootBudgetAsync(context, state)).AllocationCount);
        Assert.Equal(2, await context.DecisionBudgetAllocations.CountAsync());
    }

    [Fact]
    public async Task Actual_host_restart_preserves_root_provenance_and_historical_funding_after_settlement() {
        await using var environment = CanDoItAllTestEnvironment.Create("process-decision-budget");
        var profile = environment.CreatePostgreSqlProfile("funding-restart");
        var harness = new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = profile };
        ProcessRuntimeCommitRequest initial;
        ProcessRuntimeCommitRequest request;
        ProcessDecisionBudgetReceipt original;
        await using (var app = await TestApplication.CreateAsync(harness)) {
            await using var context = Context(app.Services);
            var owner = Owner(context, app.Services);
            initial = Initial(app.Services);
            var state = (await owner.CommitAsync(initial)).State;
            request = Change(state, Reserve(Step(state), await BudgetAsync(context, state), 100));
            original = (await owner.CommitAsync(request)).DecisionBudget!;
        }
        await using var restarted = await TestApplication.CreateAsync(harness);
        await using var reopened = Context(restarted.Services);
        var runtime = Owner(reopened, restarted.Services);
        var current = (await runtime.LoadAsync(request.OriginalState.RunId))!;
        var settled = await runtime.CommitAsync(Change(current, Settle(Step(current), await BudgetAsync(reopened, current), original.Command.AllocationId, 60)));
        Assert.Equal(60, settled.DecisionBudget!.Funding!.Receipt.Change.Balance.ConsumedWorkTicks);
        Assert.Equal(original, (await runtime.CommitAsync(request)).DecisionBudget);
        Assert.Null(original.Funding!.Receipt.Change.Allocation.Settlement);
        Assert.Equal(60, (await RootBudgetAsync(reopened, current)).ConsumedWorkTicks);
        Assert.Equal(0, (await RootBudgetAsync(reopened, current)).ReservedWorkTicks);
        Assert.True((await runtime.CommitAsync(initial)).Succeeded);
        var retained = (await reopened.DecisionBudgets.AsNoTracking().SingleAsync(item => item.StepInstanceId == null)).RunAdmissionJson;
        Assert.Equal(ProcessDecisionRunAdmissionCodec.Serialize(initial.InitialRunDecisionAdmission!), retained);
    }

    [Fact]
    public async Task Funding_migration_preserves_legacy_step_balances_and_exact_receipt_bytes() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var legacy = ProcessProjectAdmissionFixture.Initial();
        var assignment = legacy.InitialAssignments!.Single();
        legacy = legacy with { InitialAssignments = [assignment with {
            DecisionDataPolicy = ProcessDecisionPolicyFixture.Create(legacy.InitialPlan!.Definition.VersionId, assignment.StepInstanceId)
        }] };
        var owner = Owner(context, app.Services);
        var state = (await owner.CommitAsync(legacy)).State;
        var request = Change(state, Reserve(Step(state), await BudgetAsync(context, state), 100));
        var reserved = await owner.CommitAsync(request);
        var before = (await context.IdempotencyKeys.AsNoTracking().SingleAsync(item => item.CommandId == request.CommandId.Value)).DecisionBudgetReceiptJson;
        Assert.Equal(1, JsonNode.Parse(before!)!["version"]!.GetValue<int>());
        Assert.DoesNotContain("funding", before!, StringComparison.Ordinal);
        await FundingMigrationAsync(context, true);
        await FundingMigrationAsync(context, false);
        Assert.Equal(reserved.DecisionBudget, (await owner.CommitAsync(request)).DecisionBudget);
        Assert.Equal(before, (await context.IdempotencyKeys.AsNoTracking().SingleAsync(item => item.CommandId == request.CommandId.Value)).DecisionBudgetReceiptJson);
        Assert.Null((await context.DecisionBudgets.AsNoTracking().SingleAsync()).ParentBudgetId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Root_admission_blocks_downgrade_before_and_after_any_allocation(bool allocated) {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var owner = Owner(context, app.Services);
        var state = (await owner.CommitAsync(Initial(app.Services))).State;
        if (allocated) {
            state = (await owner.CommitAsync(Change(state, Reserve(Step(state), await BudgetAsync(context, state), 100)))).State;
        }
        var before = await RootBudgetAsync(context, state);
        var failure = await Assert.ThrowsAsync<PostgresException>(() => FundingMigrationAsync(context, true));
        Assert.Equal(PostgresErrorCodes.RaiseException, failure.SqlState);
        Assert.Equal(before, await RootBudgetAsync(context, state));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task Retained_funding_receipt_refuses_downgrade_missing_parent_and_changed_owner_evidence(int change) {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var owner = Owner(context, app.Services);
        var state = (await owner.CommitAsync(Initial(app.Services))).State;
        var request = Change(state, Reserve(Step(state), await BudgetAsync(context, state), 100));
        await owner.CommitAsync(request);
        var row = await context.IdempotencyKeys.SingleAsync(item => item.CommandId == request.CommandId.Value);
        var json = JsonNode.Parse(row.DecisionBudgetReceiptJson!)!.AsObject();
        switch (change) {
            case 0:
                json["version"] = 1;
                break;
            case 1:
                json.Remove("funding");
                break;
            case 2:
                json["funding"]!["rootRunId"] = Guid.Empty;
                break;
            case 3:
                json["funding"]!["receipt"]!["grant"]!["workTicks"] = 101;
                break;
            case 4:
                json["funding"]!["receipt"]!["allocationId"] = Guid.NewGuid();
                break;
        }
        row.DecisionBudgetReceiptJson = json.ToJsonString();
        await context.SaveChangesAsync();
        var failure = await Record.ExceptionAsync(() => owner.FindCompletedCommandAsync(request.CommandId));
        if (change == 2) {
            Assert.Equal("value", Assert.IsType<ArgumentException>(failure).ParamName);
        } else {
            Assert.True(failure is InvalidDataException or JsonException, failure?.ToString());
        }
        Assert.Equal(100, (await RootBudgetAsync(context, state)).ReservedWorkTicks);
    }

    [Fact]
    public async Task SQL_preserves_unique_real_root_and_original_admission_and_funding_references() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var owner = Owner(context, app.Services);
        var initial = Initial(app.Services);
        var state = (await owner.CommitAsync(initial)).State;
        var reserved = await owner.CommitAsync(Change(state, Reserve(Step(state), await BudgetAsync(context, state), 100)));
        var local = reserved.DecisionBudget!.Change.Balance.BudgetId.Value;
        var parent = reserved.DecisionBudget.Funding!.Receipt.Change.Balance.BudgetId.Value;
        var half = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE process_decision_budget_allocations SET \"FundingAllocationId\" = NULL WHERE \"BudgetId\" = {local}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, half.SqlState);
        var removeParent = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM process_decision_budgets WHERE \"BudgetId\" = {parent}"));
        Assert.Equal(PostgresErrorCodes.RestrictViolation, removeParent.SqlState);
        var removeAdmission = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM process_runtime_idempotency_keys WHERE \"RunId\" = {state.RunId.Value} AND \"CommandId\" = {initial.CommandId.Value}"));
        Assert.Equal(PostgresErrorCodes.RestrictViolation, removeAdmission.SqlState);
        var copy = await context.DecisionBudgets.AsNoTracking().SingleAsync(item => item.BudgetId == parent);
        copy.BudgetId = Guid.NewGuid();
        context.DecisionBudgets.Add(copy);
        var duplicate = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(duplicate.InnerException).SqlState);
        context.ChangeTracker.Clear();
        Assert.Equal(100, (await RootBudgetAsync(context, state)).ReservedWorkTicks);
    }

    [Fact]
    public async Task A_changed_parent_grant_is_not_adopted_by_local_reservation_replay() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var owner = Owner(context, app.Services);
        var state = (await owner.CommitAsync(Initial(app.Services))).State;
        var original = Reserve(Step(state), await BudgetAsync(context, state), 100);
        var result = await owner.CommitAsync(Change(state, original));
        var parent = result.DecisionBudget!.Funding!.Receipt;
        await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE process_decision_budget_allocations SET \"GrantedWorkTicks\" = 101 WHERE \"BudgetId\" = {parent.Change.Balance.BudgetId.Value}");
        var replay = original with { Expectation = original.Expectation with { Revision = result.DecisionBudget.Change.Balance.Revision } };
        await Assert.ThrowsAsync<InvalidDataException>(() => owner.CommitAsync(Change(result.State, replay)));
        Assert.Equal(result.DecisionBudget.Change.Balance, await BudgetAsync(context, state));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Actual_publication_fence_rechecks_root_overrun_or_cancellation(bool cancel) {
        await using var app = await TestApplication.CreateAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await using var context = Context(services);
        var initial = Initial(services);
        var owner = Owner(context, services);
        var state = (await owner.CommitAsync(initial)).State;
        var capture = await owner.CommitAsync(Change(state, Reserve(Step(state), await BudgetAsync(context, state), 10)));
        var materialScope = new ProcessDecisionMaterialScope(new(Guid.NewGuid()), initial.InitialRunDecisionAdmission!.DatabaseProfileId,
            initial.InitialRunDecisionAdmission.WorkspaceNamespaceId, state.RootRunId, state.RunId, Step(state));
        var material = ProcessDecisionMaterial.FromContent(materialScope, new(materialScope.Id, 1, new(Guid.NewGuid())),
            ProcessDecisionPolicyFixture.ContentContract(), ProcessProjectAdmissionFixture.Now, "{}");
        var started = await owner.CommitAsync(Change(capture.State, Settle(Step(state), capture.DecisionBudget!.Change.Balance,
            capture.DecisionBudget.Command.AllocationId, 5)) with {
            Decision = new ProcessDecisionStart(Step(state), new(Guid.NewGuid()), material.Reference.MaterialId) { Material = material.Stamp },
            DecisionMaterial = material
        });
        var grant = Reserve(Step(state), await BudgetAsync(context, state), 20);
        var child = new ProcessDecisionChild(started.Decision!.Current.Reference, new(Guid.NewGuid()), ProcessDecisionChildOwner.Agent, grant.AllocationId);
        var enrolled = await owner.CommitAsync(Change(started.State, grant) with {
            Decision = new ProcessDecisionEnrollment(Step(state), started.Decision.Current.Expectation, child)
        });
        state = await ActivateAsync(context, services, enrolled.State);
        var address = new ProcessDecisionChildAddress(state.RootRunId, state.RunId, Step(state), child.Reference, child.ChildId);
        var original = (await services.GetRequiredService<EfProcessDecisionChildQuery>().ReadAsync(address))!.Admission;
        await RequirePublicationAsync(services, original);
        if (cancel) {
            await owner.CommitAsync(ProcessProjectAdmissionFixture.Cancel(state));
        } else {
            var work = await owner.CommitAsync(Change(state, Reserve(null, await RootBudgetAsync(context, state), 1)));
            await owner.CommitAsync(Change(work.State, Settle(null, work.DecisionBudget!.Change.Balance, work.DecisionBudget.Command.AllocationId, 2)));
            Assert.False((await BudgetAsync(context, state)).OverrunDetected);
            Assert.True((await RootBudgetAsync(context, state)).OverrunDetected);
        }
        await Assert.ThrowsAsync<ProcessExecutionAuthorityMismatchException>(() => RequirePublicationAsync(services, original));
        Assert.Equal(20, (await BudgetAsync(context, state)).ReservedWorkTicks);
        Assert.NotNull(await services.GetRequiredService<EfProcessDecisionChildQuery>().ReadAsync(address));
    }

    [Fact]
    public async Task Root_allocation_capacity_is_shared_by_direct_and_local_work() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var owner = Owner(context, app.Services);
        var state = (await owner.CommitAsync(Initial(app.Services))).State;
        for (var index = 0; index < DecisionBudgetBalance.MaximumAllocations; index++) {
            state = (await owner.CommitAsync(Change(state, Reserve(null, await RootBudgetAsync(context, state), 1)))).State;
        }
        var local = await BudgetAsync(context, state);
        Assert.Equal(0, local.AllocationCount);
        var failure = await Assert.ThrowsAsync<DecisionBudgetException>(() => owner.CommitAsync(Change(state, Reserve(Step(state), local, 1))));
        Assert.Equal(DecisionBudgetRefusal.AllocationCapacity, failure.Reason);
        Assert.Equal(DecisionBudgetBalance.MaximumAllocations, (await RootBudgetAsync(context, state)).AllocationCount);
        Assert.Equal(local, await BudgetAsync(context, state));
    }

    internal static Task FundingMigrationAsync(ProcessPersistenceDbContext context, bool down)
        => ProcessDecisionMigrationFixture.ApplyAsync(context, down, new CanDoItAll.Migrations.PostgreSql.Migrations.AddProcessRunDecisionFunding(),
            new CanDoItAll.Migrations.PostgreSql.Migrations.AddProcessSharedRunMaterialScopes());

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task A_real_descendant_cannot_drop_or_replace_its_original_root_funding(int change) {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var owner = Owner(context, app.Services);
        var initial = Initial(app.Services);
        var parent = await ActivateAsync(context, app.Services, (await owner.CommitAsync(initial)).State);
        var request = Descendant(parent, initial.InitialRunDecisionAdmission!);
        var assignment = request.InitialAssignments!.Single();
        request = change switch {
            0 => request with { InitialAssignments = [assignment with { DecisionDataPolicy = null }] },
            1 => request with { InitialAssignments = [assignment with { DecisionDataPolicy = assignment.DecisionDataPolicy! with { Funding = null } }] },
            2 => request with { InitialAssignments = [assignment with { DecisionDataPolicy = assignment.DecisionDataPolicy! with {
                Funding = new(parent.RootRunId, new(Guid.NewGuid()))
            } }] },
            _ => request with { InitialRunDecisionAdmission = initial.InitialRunDecisionAdmission }
        };
        await Assert.ThrowsAsync<InvalidDataException>(() => owner.CommitAsync(request));
        Assert.False(await context.RuntimeStates.AnyAsync(item => item.RunId == request.OriginalState.RunId.Value));
        Assert.Equal(2, await context.DecisionBudgets.CountAsync());
        Assert.Empty(await context.DecisionBudgetAllocations.ToArrayAsync());
    }

    [Fact]
    public async Task Holding_a_funded_commit_does_not_block_an_unrelated_root() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var owner = Owner(context, app.Services);
        var first = (await owner.CommitAsync(Initial(app.Services))).State;
        var second = (await owner.CommitAsync(Initial(app.Services))).State;
        var firstRequest = Change(first, Reserve(Step(first), await BudgetAsync(context, first), 100));
        var secondRequest = Change(second, Reserve(Step(second), await BudgetAsync(context, second), 100));
        var pause = new BeforeCommit();
        await using var held = Context(app.Services, pause);
        var pending = Owner(held, app.Services).CommitAsync(firstRequest);
        try {
            await pause.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var independent = await owner.CommitAsync(secondRequest).WaitAsync(TimeSpan.FromSeconds(10));
            Assert.NotNull(independent.DecisionBudget!.Funding);
            Assert.False(pending.IsCompleted);
            Assert.Equal(0, (await RootBudgetAsync(context, first)).AllocationCount);
            Assert.Equal(1, (await RootBudgetAsync(context, second)).AllocationCount);
        } finally {
            pause.Release.TrySetResult();
            await pending.WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.Equal(1, (await RootBudgetAsync(context, first)).AllocationCount);
    }

    private static async Task RequirePublicationAsync(IServiceProvider services, ProcessDecisionChildAdmission original) {
        await using var context = Context(services);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        using var participation = services.GetRequiredService<CoordinatedDatabaseTransaction>().Enter(context);
        await services.GetRequiredService<IProcessDecisionPublicationFence>().RequireAsync(original, ProcessDecisionPublicationKind.Dispatch);
        await transaction.CommitAsync();
    }

    private sealed class AfterFlush : SaveChangesInterceptor {
        internal InvalidOperationException Failure { get; } = new("Injected failure after funded SQL flush.");
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
            await Release.Task.WaitAsync(cancellationToken);
            return result;
        }
    }
}
