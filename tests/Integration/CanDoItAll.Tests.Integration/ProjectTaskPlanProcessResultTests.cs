using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;

namespace CanDoItAll.Tests.Integration;

[Trait("Category", "FileSystemPortability")]
public sealed class ProjectTaskPlanProcessResultTests(ITestOutputHelper output) {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Result_selection_retains_original_binding_after_assignment_repair_with_bounded_metadata_queries(bool repair) {
        await using var fixture = await ProjectTaskPlanProcessRuntimeFixture.CreateAsync();
        var source = fixture.Source;
        if (repair) {
            await using var context = Context(source.Sql.Services);
            var assignment = await context.RuntimeStepAssignments.SingleAsync(item => item.RunId == source.Process.State.RunId.Value);
            assignment.ExecutorId = Guid.NewGuid().ToString("D");
            assignment.DecisionDataPolicyJson = null;
            await context.SaveChangesAsync();
        }
        var probe = new Probe();
        var query = new EfProcessDecisionResultQuery(new Factory(Options(source.Sql.Services, probe)));
        var elapsed = Stopwatch.StartNew();
        var selected = await query.ReadAsync(source.Process.State.RunId, Step(source.Process.State), new(source.Original.Id));
        output.WriteLine($"Original selection: queries={probe.Commands.Count}; elapsedMilliseconds={elapsed.Elapsed.TotalMilliseconds:F3}; assignmentRepaired={repair}");
        Assert.NotNull(selected);
        selected.RequireOriginal(source.Process.State, Step(source.Process.State), new(source.Original.Id));
        Assert.Equal(source.Process.Parent, selected.Child!.Original);
        Assert.False(selected.OtherWorkPending);
        Assert.False(selected.SharedScopePending);
        Assert.Equal(4, probe.Commands.Count);
        Assert.All(probe.Commands, sql => {
            Assert.StartsWith("SELECT", sql.TrimStart(), StringComparison.Ordinal);
            Assert.Contains("LIMIT 2", sql, StringComparison.Ordinal);
            if (sql.Contains("process_decision_materials", StringComparison.Ordinal)) {
                Assert.DoesNotContain("PayloadJson", sql, StringComparison.Ordinal);
            }
        });
        Assert.Single(probe.Commands, sql => sql.Contains("process_accepted_assignment_sources", StringComparison.Ordinal));
        Assert.Empty(fixture.Wire.Requests);
    }

    [Theory]
    [InlineData(StrategyOutcome.Succeeded, ProcessRuntimeStepStatus.Completed)]
    [InlineData(StrategyOutcome.Failed, ProcessRuntimeStepStatus.Failed)]
    [InlineData(StrategyOutcome.Canceled, ProcessRuntimeStepStatus.Cancelled)]
    public async Task Registered_result_coordinator_commits_owner_work_and_exact_process_outcome_once(
        StrategyOutcome outcome, ProcessRuntimeStepStatus status) {
        await using var fixture = await ProjectTaskPlanProcessRuntimeFixture.CreateAsync();
        var published = ProjectTaskPlanAgentFixture.Result(await fixture.PublishAsync(await fixture.PrepareAsync()));
        var source = fixture.Source;
        var services = source.Sql.Services;
        var coordinator = services.GetRequiredService<ProcessDecisionResultCoordinator>();
        var command = Command(fixture, outcome);
        var result = await coordinator.SubmitAsync(source.Process.State, ResultContext(fixture), command);
        Assert.True(result.Succeeded);
        Assert.Equal(status, result.State.Steps.Single().Status);
        Assert.Equal(new ProcessDecisionEffectReceiptId(published.OperationId), result.Decision!.Child!.Closure!.AcceptedReceipt);
        Assert.Equal(source.Process.Parent.Child.AllocationId, result.DecisionBudget!.Change.Allocation.Id);
        Assert.Equal(0, result.DecisionBudget.Change.Balance.ReservedWorkTicks);
        Assert.Equal(status == ProcessRuntimeStepStatus.Cancelled ? ProcessDecisionPhase.Cancelled : ProcessDecisionPhase.Completed, result.Decision.Current.Phase);
        source.Generations.Advance();
        var replay = await coordinator.SubmitAsync(result.State, ResultContext(fixture), command);
        Assert.Equal(ProcessRuntimeTransitionOutcome.Duplicate, replay.Outcome);
        Assert.Equal(result.Decision, replay.Decision);
        Assert.Equal(result.DecisionBudget, replay.DecisionBudget);
        Assert.Single(replay.State.AppliedResults);
        Assert.Single(fixture.Wire.Requests);
        await source.Sql.VerifyAsync(1, 1);
    }

    [Theory]
    [InlineData(false, ProcessDecisionPhase.Reconciling)]
    [InlineData(true, ProcessDecisionPhase.WaitingForReview)]
    public async Task Registered_pending_result_blocks_dispatch_without_releasing_original_work(bool approval, ProcessDecisionPhase phase) {
        await using var fixture = await ProjectTaskPlanProcessRuntimeFixture.CreateAsync();
        if (approval) {
            await fixture.PrepareAsync();
        }
        var source = fixture.Source;
        var result = await source.Sql.Services.GetRequiredService<ProcessDecisionResultCoordinator>()
            .SubmitAsync(source.Process.State, ResultContext(fixture), Command(fixture, StrategyOutcome.Succeeded));
        Assert.Equal(ProcessRuntimeStepStatus.Blocked, result.State.Steps.Single().Status);
        Assert.Equal(phase, result.Decision!.Current.Phase);
        Assert.Null(result.DecisionBudget);
        await using var context = Context(source.Sql.Services);
        Assert.Equal(source.Process.Parent.Child.Grant.WorkTicks, (await BudgetAsync(context, result.State)).ReservedWorkTicks);
        Assert.Equal(1, result.Decision.Current.UnclosedChildCount);
        Assert.Null(result.State.Steps.Single().ActiveClaimToken);
        Assert.Empty(result.State.AppliedResults.Single().ProducedArtifacts);
        await source.Sql.VerifyAsync(0, 0);
    }

    [Fact]
    public async Task Known_owner_effect_stays_accounted_when_runtime_output_contract_needs_review() {
        await using var fixture = await ProjectTaskPlanProcessRuntimeFixture.CreateAsync();
        await fixture.PublishAsync(await fixture.PrepareAsync());
        var source = fixture.Source;
        var state = source.Process.State;
        var slot = ArtifactSlotId.New();
        await using var context = Context(source.Sql.Services);
        var owner = source.Sql.Services.GetRequiredService<IProcessRuntimeUnitOfWork>();
        var changed = Request(state, null);
        changed = changed with { Mutation = changed.Mutation with { State = changed.Mutation.State with {
            Steps = [state.Steps.Single() with { ProducedArtifactSlots = new HashSet<ArtifactSlotId> { slot } }],
        } } };
        state = (await owner.CommitAsync(changed)).State;
        var result = await source.Sql.Services.GetRequiredService<ProcessDecisionResultCoordinator>()
            .SubmitAsync(state, ResultContext(fixture), Command(fixture, StrategyOutcome.Succeeded));
        Assert.Equal(ProcessRuntimeStepStatus.Blocked, result.State.Steps.Single().Status);
        Assert.Equal(ProcessDecisionPhase.WaitingForReview, result.Decision!.Current.Phase);
        Assert.Equal(0, result.DecisionBudget!.Change.Balance.ReservedWorkTicks);
        Assert.Equal(0, result.Decision.Current.UnclosedChildCount);
    }

    [Fact]
    public async Task Competing_result_submitters_converge_on_one_original_receipt_and_charge() {
        await using var fixture = await ProjectTaskPlanProcessRuntimeFixture.CreateAsync();
        await fixture.PublishAsync(await fixture.PrepareAsync());
        var source = fixture.Source;
        await using var firstScope = source.Sql.Services.CreateAsyncScope();
        await using var secondScope = source.Sql.Services.CreateAsyncScope();
        var command = Command(fixture, StrategyOutcome.Succeeded);
        var results = await Task.WhenAll(
            firstScope.ServiceProvider.GetRequiredService<ProcessDecisionResultCoordinator>().SubmitAsync(source.Process.State, ResultContext(fixture), command),
            secondScope.ServiceProvider.GetRequiredService<ProcessDecisionResultCoordinator>().SubmitAsync(source.Process.State, ResultContext(fixture), command));
        Assert.All(results, result => Assert.True(result.Succeeded));
        Assert.Equal(results[0].Decision, results[1].Decision);
        Assert.Equal(results[0].DecisionBudget, results[1].DecisionBudget);
        await using var context = Context(source.Sql.Services);
        Assert.Single((await new EfProcessRuntimeUnitOfWork(context).LoadAsync(source.Process.State.RunId))!.AppliedResults);
        Assert.Equal(0, (await BudgetAsync(context, source.Process.State)).ReservedWorkTicks);
        Assert.Single(fixture.Wire.Requests);
    }

    private static SubmitStrategyResultCommand Command(ProjectTaskPlanProcessRuntimeFixture fixture, StrategyOutcome outcome) {
        var state = fixture.Source.Process.State;
        var claim = state.Claims.Single(item => item.ClaimToken == state.Steps.Single().ActiveClaimToken);
        var key = Guid.NewGuid();
        return new(Step(state), claim.OwnerId, claim.ClaimToken, new(key),
            new(new("process-result-fixture"), "1.0.0", key, outcome,
                state.Steps.Single().ProducedArtifactSlots.Select(slot => new ProducedArtifactRef(ArtifactInstanceId.New(), slot, Hash)).ToArray(),
                [], [], [], Hash) { ExecutionRunId = new(fixture.Source.Original.Id) });
    }

    private static string Hash => "sha256:" + new string('d', 64);
    private static RuntimeCommandContext ResultContext(ProjectTaskPlanProcessRuntimeFixture fixture)
        => new(RuntimeCommandId.New(), new(ProcessEventActorKind.System, new("process-result-fixture")), new("process-result-fixture"),
            fixture.Source.Clock.GetUtcNow().AddSeconds(2));

    private sealed class Factory(DbContextOptions<ProcessPersistenceDbContext> options) : IDbContextFactory<ProcessPersistenceDbContext> {
        public ProcessPersistenceDbContext CreateDbContext() => new(options);
    }

    private sealed class Probe : DbCommandInterceptor {
        internal ConcurrentQueue<string> Commands { get; } = new();
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            Commands.Enqueue(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }
}
