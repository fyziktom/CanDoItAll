using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;

namespace CanDoItAll.Tests.Integration;

[Trait("Category", "FileSystemPortability")]
public sealed class ProcessDecisionTerminalAccountingIntegrationTests {
    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot, AgentPlanningReviewChoice.Approve)]
    [InlineData(ReadBasisMode.RunSnapshot, AgentPlanningReviewChoice.Reject)]
    [InlineData(ReadBasisMode.StepSnapshot, AgentPlanningReviewChoice.Approve)]
    [InlineData(ReadBasisMode.StepSnapshot, AgentPlanningReviewChoice.Reject)]
    public async Task Parent_stop_retains_the_actual_effect_and_settles_exact_work_without_reopening(
        ReadBasisMode mode, AgentPlanningReviewChoice choice) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(mode, steps: 2);
        var closed = await fixture.CloseWithoutAccountingAsync(choice);
        var beforeAgent = JsonSerializer.Serialize(await fixture.Entry.Files.Store.GetExecutionRunDetailAsync(closed.Id));
        var stopped = await StopAsync(fixture, reconcileScope: mode == ReadBasisMode.RunSnapshot);
        var request = await RequestAsync(fixture, stopped);
        await using var budgetContext = Context(fixture.Services);
        var rootBefore = (await new EfProcessDecisionBudgetStore(budgetContext).ReadAsync(stopped.RootRunId, null))!;
        var completed = await fixture.Services.GetRequiredService<ProcessDecisionTerminalAccountingCoordinator>()
            .CompleteAsync(stopped, fixture.CommandContext, request);
        Assert.True(completed.CommittedNow);
        Assert.Equal(ProcessRuntimeStatus.CancelRequested, completed.State.Status);
        Assert.Equal(JsonSerializer.Serialize(stopped), JsonSerializer.Serialize(completed.State with { UpdatedAtUtc = stopped.UpdatedAtUtc }));
        Assert.Empty(completed.Events);
        Assert.Empty(completed.OutboxMessages);
        Assert.Null(completed.DecisionRunScope);
        Assert.Equal(ProcessDecisionPhase.Cancelled, completed.Decision!.Current.Phase);
        Assert.Equal(choice == AgentPlanningReviewChoice.Approve ? ProcessDecisionChildOutcome.Accepted : ProcessDecisionChildOutcome.NoEffect,
            completed.Decision.Child!.Closure!.Outcome);
        Assert.Equal(closed.ToolAdmission!.PlanningDecision!.Work.ClosedWorkTicks,
            completed.DecisionBudget!.Change.Allocation.Settlement!.Consumed.WorkTicks);
        Assert.Equal(0, completed.DecisionBudget.Change.Balance.ReservedWorkTicks);
        var funding = completed.DecisionBudget.Funding!.Receipt;
        Assert.Equal(completed.DecisionBudget.Change.Allocation.Settlement, funding.Change.Allocation.Settlement);
        Assert.Equal(0, funding.Change.Balance.ReservedWorkTicks);
        Assert.Equal(rootBefore.ConsumedWorkTicks + completed.DecisionBudget.Change.Allocation.Settlement!.Consumed.WorkTicks,
            funding.Change.Balance.ConsumedWorkTicks);
        Assert.Equal(funding.Change.Balance, await new EfProcessDecisionBudgetStore(budgetContext).ReadAsync(stopped.RootRunId, null));
        await using var scope = fixture.Services.CreateAsyncScope();
        var current = await scope.ServiceProvider.GetRequiredService<IProcessRuntimeStateStore>().LoadAsync(stopped.RunId);
        var replay = await scope.ServiceProvider.GetRequiredService<ProcessDecisionTerminalAccountingCoordinator>()
            .CompleteAsync(current!, fixture.CommandContext, request);
        Assert.Equal(ProcessRuntimeTransitionOutcome.Duplicate, replay.Outcome);
        Assert.Equal(completed.DecisionBudget, replay.DecisionBudget);
        Assert.False(replay.CommittedNow);
        Assert.Equal(beforeAgent, JsonSerializer.Serialize(await fixture.Entry.Files.Store.GetExecutionRunDetailAsync(closed.Id)));
        Assert.Single(fixture.Entry.Wire.Requests);
        await fixture.Entry.Files.Sql.VerifyAsync(choice == AgentPlanningReviewChoice.Approve ? 1 : 0, choice == AgentPlanningReviewChoice.Approve ? 1 : 0);
    }

    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public async Task Accounting_replay_after_failed_step_rework_returns_current_runtime_without_recharging(ReadBasisMode mode) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(mode);
        var closed = await fixture.CloseWithoutAccountingAsync(AgentPlanningReviewChoice.Approve);
        var beforeAgent = JsonSerializer.Serialize(await fixture.Entry.Files.Store.GetExecutionRunDetailAsync(closed.Id));
        var owner = fixture.Services.GetRequiredService<IProcessRuntimeUnitOfWork>();
        var current = (await fixture.Services.GetRequiredService<IProcessRuntimeStateStore>().LoadAsync(fixture.State.RunId))!;
        var failed = current with {
            Steps = current.Steps.Select(step => step with { Status = ProcessRuntimeStepStatus.Failed }).ToArray(),
            UpdatedAtUtc = current.UpdatedAtUtc.AddTicks(10)
        };
        failed = (await owner.CommitAsync(new(RuntimeCommandId.New(), current,
            new(ProcessRuntimeTransitionOutcome.Applied, failed, [], [], [], [])))).State;
        var request = await RequestAsync(fixture, failed);
        var accounted = await fixture.Services.GetRequiredService<ProcessDecisionTerminalAccountingCoordinator>()
            .CompleteAsync(failed, fixture.CommandContext, request);
        Assert.True(accounted.CommittedNow);
        var reworked = await new ProcessRuntimeEngine(owner).RequestStepReworkAsync(accounted.State,
            fixture.CommandContext with { CommandId = RuntimeCommandId.New(), OccurredAtUtc = accounted.State.UpdatedAtUtc.AddTicks(10) },
            new(request.Child.StepInstanceId, "Retry after terminal accounting."));
        Assert.True(reworked.CommittedNow);
        Assert.Equal(ProcessRuntimeStepStatus.Ready, reworked.State.Steps.Single().Status);
        await using var scope = fixture.Services.CreateAsyncScope();
        current = (await scope.ServiceProvider.GetRequiredService<IProcessRuntimeStateStore>().LoadAsync(failed.RunId))!;
        var replay = await scope.ServiceProvider.GetRequiredService<ProcessDecisionTerminalAccountingCoordinator>()
            .CompleteAsync(current, fixture.CommandContext, request);
        Assert.Equal(ProcessRuntimeTransitionOutcome.Duplicate, replay.Outcome);
        Assert.False(replay.CommittedNow);
        Assert.Equal(JsonSerializer.Serialize(current), JsonSerializer.Serialize(replay.State));
        Assert.Equal(accounted.Decision, replay.Decision);
        Assert.Equal(accounted.DecisionBudget, replay.DecisionBudget);
        await using var budgetContext = Context(scope.ServiceProvider);
        Assert.Equal(accounted.DecisionBudget!.Funding!.Receipt.Change.Balance,
            await new EfProcessDecisionBudgetStore(budgetContext).ReadAsync(failed.RootRunId, null));
        Assert.Equal(beforeAgent, JsonSerializer.Serialize(await fixture.Entry.Files.Store.GetExecutionRunDetailAsync(closed.Id)));
        Assert.Single(fixture.Entry.Wire.Requests);
        await fixture.Entry.Files.Sql.VerifyAsync(1, 1);
    }

    [Fact]
    public async Task Lost_commit_acknowledgement_recovers_in_a_fresh_scope() {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync();
        await fixture.CloseWithoutAccountingAsync(AgentPlanningReviewChoice.Approve);
        var stopped = await StopAsync(fixture);
        var request = await RequestAsync(fixture, stopped);
        var fault = new IOException("Terminal accounting acknowledgement was lost.");
        var hooked = new CommitHook(fixture.Services.GetRequiredService<IProcessRuntimeUnitOfWork>(),
            after: _ => Task.FromException(fault));
        var coordinator = ActivatorUtilities.CreateInstance<ProcessDecisionTerminalAccountingCoordinator>(fixture.Services, hooked);
        Assert.Same(fault, await Assert.ThrowsAsync<IOException>(() => coordinator.CompleteAsync(stopped, fixture.CommandContext, request)));
        await using var scope = fixture.Services.CreateAsyncScope();
        var current = (await scope.ServiceProvider.GetRequiredService<IProcessRuntimeStateStore>().LoadAsync(stopped.RunId))!;
        var replay = await scope.ServiceProvider.GetRequiredService<ProcessDecisionTerminalAccountingCoordinator>()
            .CompleteAsync(current, fixture.CommandContext, request);
        Assert.Equal(ProcessRuntimeTransitionOutcome.Duplicate, replay.Outcome);
        Assert.False(replay.CommittedNow);
        Assert.Single(fixture.Entry.Wire.Requests);
        await fixture.Entry.Files.Sql.VerifyAsync(1, 1);
    }

    [Fact]
    public async Task Root_stop_does_not_turn_pending_Agent_work_into_a_closed_child() {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync();
        var stopped = await StopAsync(fixture);
        var request = await RequestAsync(fixture, stopped);
        var result = await fixture.Services.GetRequiredService<ProcessDecisionTerminalAccountingCoordinator>()
            .CompleteAsync(stopped, fixture.CommandContext, request);
        Assert.False(result.Succeeded);
        var original = await fixture.Services.GetRequiredService<IProcessDecisionChildReader>().ReadAsync(request.Child);
        Assert.Null(original!.Closure);
        Assert.Null(original.Settlement);
        Assert.Single(fixture.Entry.Wire.Requests);
        await fixture.Entry.Files.Sql.VerifyAsync(0, 0);
    }

    [Fact]
    public async Task Competing_terminal_actions_commit_only_one_settlement() {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync();
        await fixture.CloseWithoutAccountingAsync(AgentPlanningReviewChoice.Reject);
        var stopped = await StopAsync(fixture);
        var request = await RequestAsync(fixture, stopped);
        var arrived = 0;
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task BeforeAsync(CancellationToken token) {
            if (Interlocked.Increment(ref arrived) == 2) {
                release.TrySetResult();
            }
            await release.Task.WaitAsync(TimeSpan.FromSeconds(45), token);
        }
        async Task<bool> ExecuteAsync(ProcessDecisionTerminalAccountingRequest action) {
            await using var scope = fixture.Services.CreateAsyncScope();
            var owner = new CommitHook(scope.ServiceProvider.GetRequiredService<IProcessRuntimeUnitOfWork>(), before: BeforeAsync);
            var coordinator = ActivatorUtilities.CreateInstance<ProcessDecisionTerminalAccountingCoordinator>(scope.ServiceProvider, owner);
            try {
                return (await coordinator.CompleteAsync(stopped, fixture.CommandContext, action)).CommittedNow;
            } catch (ProcessRuntimeOptimisticConcurrencyException) {
                return false;
            }
        }
        var results = await Task.WhenAll(ExecuteAsync(request), ExecuteAsync(request with { ActionId = RuntimeCommandId.New() }));
        Assert.Equal(2, arrived);
        Assert.Single(results, value => value);
        Assert.NotNull((await fixture.Services.GetRequiredService<IProcessDecisionChildReader>().ReadAsync(request.Child))!.Settlement);
        Assert.Single(fixture.Entry.Wire.Requests);
        await fixture.Entry.Files.Sql.VerifyAsync(0, 0);
    }

    [Fact]
    public async Task Failure_after_SQL_flush_rolls_back_closure_and_budget_together() {
        var fault = new AccountingFault();
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(configureServices: services =>
            services.ConfigureDbContext<ProcessPersistenceDbContext>((_, options) => options.AddInterceptors(fault)));
        await fixture.CloseWithoutAccountingAsync(AgentPlanningReviewChoice.Approve);
        var stopped = await StopAsync(fixture);
        var request = await RequestAsync(fixture, stopped);
        fault.Enabled = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Services.GetRequiredService<ProcessDecisionTerminalAccountingCoordinator>()
            .CompleteAsync(stopped, fixture.CommandContext, request));
        Assert.False(fault.Enabled);
        await using var scope = fixture.Services.CreateAsyncScope();
        var child = await scope.ServiceProvider.GetRequiredService<IProcessDecisionChildReader>().ReadAsync(request.Child);
        Assert.Null(child!.Closure);
        Assert.Null(child.Settlement);
        var current = (await scope.ServiceProvider.GetRequiredService<IProcessRuntimeStateStore>().LoadAsync(stopped.RunId))!;
        Assert.Equal(JsonSerializer.Serialize(stopped), JsonSerializer.Serialize(current));
        Assert.True((await scope.ServiceProvider.GetRequiredService<ProcessDecisionTerminalAccountingCoordinator>()
            .CompleteAsync(current, fixture.CommandContext, request)).CommittedNow);
        Assert.Single(fixture.Entry.Wire.Requests);
        await fixture.Entry.Files.Sql.VerifyAsync(1, 1);
    }

    private static async Task<ProcessRuntimeStateSnapshot> StopAsync(ProjectTaskPlanReviewFixture fixture, bool reconcileScope = false) {
        var owner = fixture.Services.GetRequiredService<IProcessRuntimeUnitOfWork>();
        var state = (await fixture.Services.GetRequiredService<IProcessRuntimeStateStore>().LoadAsync(fixture.State.RunId))!;
        var stopped = await new ProcessRuntimeEngine(owner).BeginRootCancellationAsync(state, fixture.CommandContext);
        Assert.True(stopped.Succeeded);
        state = stopped.State;
        if (reconcileScope) {
            var root = await fixture.Services.GetRequiredService<IProcessDecisionRootQuery>().ReadAsync(state);
            var updated = state with { UpdatedAtUtc = state.UpdatedAtUtc.AddTicks(10) };
            state = (await owner.CommitAsync(new(RuntimeCommandId.New(), state,
                new(ProcessRuntimeTransitionOutcome.Applied, updated, [], [], [], [])) {
                DecisionRunScope = new(root!.Scope.Expectation, ProcessDecisionRunScopeCommandKind.BeginReconciliation)
            })).State;
        }
        return state;
    }

    private static async Task<ProcessDecisionTerminalAccountingRequest> RequestAsync(ProjectTaskPlanReviewFixture fixture,
        ProcessRuntimeStateSnapshot state) {
        var selected = (await fixture.Services.GetRequiredService<IProcessDecisionResultQuery>()
            .ReadAsync(state.RunId, fixture.Request.Child.StepInstanceId, new(fixture.Waiting.Id)))!;
        return new(fixture.Request.Child, selected.Head!.Expectation,
            new(fixture.Request.Child.StepInstanceId, selected.Budget.BudgetId, selected.Budget.Revision),
            state.Steps.Single(step => step.StepInstanceId == fixture.Request.Child.StepInstanceId).Status, RuntimeCommandId.New());
    }

    private sealed class CommitHook(IProcessRuntimeUnitOfWork inner, Func<CancellationToken, Task>? before = null,
        Func<CancellationToken, Task>? after = null) : IProcessRuntimeUnitOfWork {
        public async Task<ProcessRuntimeCommitResult> CommitAsync(ProcessRuntimeCommitRequest request, CancellationToken cancellationToken) {
            if (before is not null) {
                await before(cancellationToken);
            }
            var result = await inner.CommitAsync(request, cancellationToken);
            if (after is not null) {
                await after(cancellationToken);
            }
            return result;
        }
    }

    private sealed class AccountingFault : SaveChangesInterceptor {
        internal bool Enabled { get; set; }

        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default) {
            if (Enabled) {
                Enabled = false;
                throw new InvalidOperationException("Terminal accounting fixture rollback after SQL flush.");
            }
            return ValueTask.FromResult(result);
        }
    }
}
