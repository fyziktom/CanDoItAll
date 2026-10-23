using System.Data.Common;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;

namespace CanDoItAll.Tests.Integration;

[Trait("Category", "FileSystemPortability")]
public sealed class ProcessDecisionRefreshAccountingTests {
    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public async Task Stopped_child_work_settles_once_and_remains_reconciling_before_and_after_replay(ReadBasisMode mode) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(mode);
        var id = await PrepareAsync(fixture, accepted: false);
        var state = await StateAsync(fixture.Services, fixture.State.RunId);
        var selected = await SelectedAsync(fixture);
        var actual = Assert.IsType<ProcessDecisionChildResult.Closed>(await Assert.Single(fixture.Services
            .GetServices<IProcessDecisionChildOutcomeReader>()).ReadAsync(fixture.Parent));
        var result = Assert.IsType<ProcessDecisionRefreshAccountingResult.Reconciled>(await Coordinator(fixture.Services)
            .ReconcileAsync(state, fixture.CommandContext, id)).Commit;
        Assert.True(result.CommittedNow);
        Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(result.State with { UpdatedAtUtc = state.UpdatedAtUtc }));
        Assert.Equal(ProcessDecisionPhase.Reconciling, result.Decision!.Current.Phase);
        Assert.Equal(0, result.Decision.Current.UnclosedChildCount);
        Assert.Equal(actual.Work, result.DecisionBudget!.Change.Allocation.Settlement);
        Assert.Equal(actual.Work.Consumed.WorkTicks + selected.Budget.ConsumedWorkTicks, result.DecisionBudget.Change.Balance.ConsumedWorkTicks);
        Assert.Equal(0, result.DecisionBudget.Change.Balance.ReservedWorkTicks);
        Assert.NotNull(result.DecisionBudget.Funding);
        Assert.Equal(actual.Work, result.DecisionBudget.Funding!.Receipt.Change.Allocation.Settlement);
        Assert.Equal(0, result.DecisionBudget.Funding.Receipt.Change.Balance.ReservedWorkTicks);
        Assert.Empty(result.Events);
        await VerifyScopeAsync(fixture.Services, id, mode, ProcessDecisionRunScopePhase.Reconciling);
        var cancelled = await new ProcessRuntimeEngine(fixture.Services.GetRequiredService<IProcessRuntimeUnitOfWork>())
            .BeginRootCancellationAsync(result.State, fixture.CommandContext);
        Assert.True(cancelled.Succeeded);
        var before = await RetainedAsync(fixture.Services, state.RunId);
        await using var scope = fixture.Services.CreateAsyncScope();
        var replay = Assert.IsType<ProcessDecisionRefreshAccountingResult.Reconciled>(await Coordinator(scope.ServiceProvider,
            readers: [new ReadForbidden()]).ReconcileAsync(cancelled.State, fixture.CommandContext, id)).Commit;
        Assert.False(replay.CommittedNow);
        Assert.Equal(ProcessRuntimeTransitionOutcome.Duplicate, replay.Outcome);
        Assert.Equal(actual.Work, replay.DecisionBudget!.Change.Allocation.Settlement);
        Assert.Equal(before, await RetainedAsync(fixture.Services, state.RunId));
        await fixture.Entry.Files.Sql.VerifyAsync(0, 0);
        Assert.Single(fixture.Entry.Wire.Requests);
    }

    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public async Task Accepted_before_refresh_completes_the_original_result_without_capturing_or_refunding(ReadBasisMode mode) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(mode);
        var id = await PrepareAsync(fixture, accepted: true);
        var state = await StateAsync(fixture.Services, fixture.State.RunId);
        var actual = Assert.IsType<ProcessDecisionChildResult.Closed>(await Assert.Single(fixture.Services
            .GetServices<IProcessDecisionChildOutcomeReader>()).ReadAsync(fixture.Parent));
        var result = Assert.IsType<ProcessDecisionRefreshAccountingResult.CompletedOriginal>(await Coordinator(fixture.Services)
            .ReconcileAsync(state, fixture.CommandContext, id)).Commit;
        Assert.True(result.CommittedNow);
        Assert.Equal(ProcessRuntimeStatus.Completed, result.State.Status);
        Assert.Equal(ProcessRuntimeStepStatus.Completed, result.State.Steps.Single().Status);
        Assert.Equal(2, result.State.AppliedResults.Count);
        Assert.Equal(JsonSerializer.Serialize(state.AppliedResults[0]), JsonSerializer.Serialize(result.State.AppliedResults[0]));
        Assert.Equal(state.Claims.Single(), result.State.Claims.Single());
        Assert.Equal(fixture.Parent.Child.MaterialId, result.Decision!.Current.MaterialId);
        Assert.Equal(id, result.Decision.Current.RefreshCommandId);
        Assert.Equal(ProcessDecisionChildOutcome.Accepted, result.Decision.Child!.Closure!.Outcome);
        Assert.Equal(actual.Work, result.DecisionBudget!.Change.Allocation.Settlement);
        Assert.Equal(result.DecisionBudget.Change.Allocation.Settlement, result.DecisionBudget.Funding!.Receipt.Change.Allocation.Settlement);
        Assert.Equal(0, result.DecisionBudget.Funding.Receipt.Change.Balance.ReservedWorkTicks);
        await VerifyScopeAsync(fixture.Services, id, mode, ProcessDecisionRunScopePhase.Completed);
        var before = await RetainedAsync(fixture.Services, state.RunId);
        await using var scope = fixture.Services.CreateAsyncScope();
        var replay = Assert.IsType<ProcessDecisionRefreshAccountingResult.CompletedOriginal>(await Coordinator(scope.ServiceProvider,
            readers: [new ReadForbidden()]).ReconcileAsync(result.State, fixture.CommandContext, id)).Commit;
        Assert.False(replay.CommittedNow);
        Assert.Equal(before, await RetainedAsync(fixture.Services, state.RunId));
        await fixture.Entry.Files.Sql.VerifyAsync(1, 1);
        Assert.Single(fixture.Entry.Wire.Requests);
    }

    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot, false)]
    [InlineData(ReadBasisMode.StepSnapshot, false)]
    [InlineData(ReadBasisMode.RunSnapshot, true)]
    [InlineData(ReadBasisMode.StepSnapshot, true)]
    public async Task Concurrent_continuations_share_one_accounting_commit(ReadBasisMode mode, bool accepted) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(mode);
        var id = await PrepareAsync(fixture, accepted);
        var state = await StateAsync(fixture.Services, fixture.State.RunId);
        var rendezvous = new Rendezvous();
        var results = await Task.WhenAll(AttemptAsync(), AttemptAsync());
        Assert.Single(results.Where(result => result.CommittedNow));
        Assert.Single(results.Where(result => !result.CommittedNow));
        Assert.Equal(results[0].Decision, results[1].Decision);
        Assert.Equal(results[0].DecisionBudget, results[1].DecisionBudget);
        await using var database = Context(fixture.Services);
        var command = new ProcessDecisionRefreshAccounting(id).CommandId;
        Assert.Equal(1, await database.IdempotencyKeys.CountAsync(row => row.RunId == state.RunId.Value && row.CommandId == command.Value));
        Assert.Single(fixture.Entry.Wire.Requests);

        async Task<ProcessRuntimeCommitResult> AttemptAsync() {
            await using var scope = fixture.Services.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var owner = new BeforeCommit(services.GetRequiredService<IProcessRuntimeUnitOfWork>(), rendezvous.WaitAsync);
            return Commit(await Coordinator(services, owner).ReconcileAsync(state, fixture.CommandContext, id));
        }
    }

    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot, false)]
    [InlineData(ReadBasisMode.StepSnapshot, false)]
    [InlineData(ReadBasisMode.RunSnapshot, true)]
    [InlineData(ReadBasisMode.StepSnapshot, true)]
    public async Task Failed_flush_rolls_back_local_root_result_and_scope_together(ReadBasisMode mode, bool accepted) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(mode);
        var id = await PrepareAsync(fixture, accepted);
        var state = await StateAsync(fixture.Services, fixture.State.RunId);
        var before = await RetainedAsync(fixture.Services, state.RunId);
        var failure = new IOException("Refresh accounting interrupted before commit.");
        await using (var database = Context(fixture.Services, new AfterFlush(failure))) {
            var owner = Owner(fixture, database);
            Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => Coordinator(fixture.Services, owner, owner)
                .ReconcileAsync(state, fixture.CommandContext, id)));
        }
        Assert.Equal(before, await RetainedAsync(fixture.Services, state.RunId));
        Assert.True(Commit(await Coordinator(fixture.Services).ReconcileAsync(state, fixture.CommandContext, id)).CommittedNow);
        Assert.Single(fixture.Entry.Wire.Requests);
    }

    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot, false)]
    [InlineData(ReadBasisMode.StepSnapshot, false)]
    [InlineData(ReadBasisMode.RunSnapshot, true)]
    [InlineData(ReadBasisMode.StepSnapshot, true)]
    public async Task Lost_commit_acknowledgement_replays_after_host_restart_without_owner_reads(ReadBasisMode mode, bool accepted) {
        await using var environment = CanDoItAllTestEnvironment.Create("process-refresh-accounting");
        var profile = environment.CreatePostgreSqlProfile("refresh-accounting");
        RuntimeCommandId id;
        ProcessRunId runId;
        RuntimeCommandContext commandContext;
        string before;
        var failure = new IOException("Refresh accounting acknowledgement lost.");
        await using (var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(mode, environment, profile)) {
            id = await PrepareAsync(fixture, accepted);
            runId = fixture.State.RunId;
            commandContext = fixture.CommandContext;
            var state = await StateAsync(fixture.Services, runId);
            await using var database = Context(fixture.Services, new AfterCommit(failure));
            var owner = Owner(fixture, database);
            Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => Coordinator(fixture.Services, owner, owner)
                .ReconcileAsync(state, commandContext, id)));
            before = await RetainedAsync(fixture.Services, runId);
            Assert.Single(fixture.Entry.Wire.Requests);
        }
        await using var restarted = await TestApplication.CreateAsync(new TestHarnessOptions {
            TestEnvironment = environment, ActiveProfile = profile, ConfigureServices = services => {
                foreach (var descriptor in services.Where(item => item.ImplementationType?.Name == "ProcessLaunchContinuationWorker").ToArray()) {
                    services.Remove(descriptor);
                }
                services.RemoveAll<IProcessDecisionChildOutcomeReader>();
                services.AddSingleton<IProcessDecisionChildOutcomeReader, ReadForbidden>();
            }
        });
        await using var scope = restarted.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var retained = await StateAsync(services, runId);
        var replay = await services.GetRequiredService<ProcessDecisionRefreshAccountingCoordinator>()
            .ReconcileAsync(retained, commandContext, id);
        Assert.Equal(accepted, replay is ProcessDecisionRefreshAccountingResult.CompletedOriginal);
        Assert.Equal(ProcessRuntimeTransitionOutcome.Duplicate, Commit(replay).Outcome);
        Assert.False(Commit(replay).CommittedNow);
        Assert.Equal(before, await RetainedAsync(services, runId));
    }

    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot, false)]
    [InlineData(ReadBasisMode.StepSnapshot, false)]
    [InlineData(ReadBasisMode.RunSnapshot, true)]
    [InlineData(ReadBasisMode.StepSnapshot, true)]
    public async Task Root_cancellation_after_owner_read_wins_without_releasing_or_completing_old_work(ReadBasisMode mode, bool accepted) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(mode);
        var id = await PrepareAsync(fixture, accepted);
        var state = await StateAsync(fixture.Services, fixture.State.RunId);
        var before = await SelectedAsync(fixture);
        var owner = new BeforeCommit(fixture.Services.GetRequiredService<IProcessRuntimeUnitOfWork>(), async token => {
            await using var database = Context(fixture.Services);
            var cancelled = await new ProcessRuntimeEngine(Owner(fixture, database))
                .BeginRootCancellationAsync(state, fixture.CommandContext, token);
            Assert.True(cancelled.CommittedNow);
        });
        await Assert.ThrowsAsync<ProcessRuntimeOptimisticConcurrencyException>(() => Coordinator(fixture.Services, owner)
            .ReconcileAsync(state, fixture.CommandContext, id));
        var after = await SelectedAsync(fixture);
        Assert.Equal(before.Budget, after.Budget);
        Assert.Equal(before.Head, after.Head);
        Assert.Null(after.Child!.Observation.Closure);
        Assert.Null(after.Child.Observation.Settlement);
        Assert.Single((await StateAsync(fixture.Services, state.RunId)).AppliedResults);
        await fixture.Entry.Files.Sql.VerifyAsync(accepted ? 1 : 0, accepted ? 1 : 0);
    }

    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public async Task Actual_unknown_publication_acknowledgement_cannot_settle_or_refund_work(ReadBasisMode mode) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(mode);
        fixture.Entry.Files.Sql.Faults.Commit.AfterCommit = () => {
            fixture.Entry.Files.Sql.Probe.BeforeCommand = (command, _) => command.CommandText.Contains("LEFT JOIN \"Workbench_TaskPlanReceipts\"", StringComparison.Ordinal)
                ? Task.FromException(new IOException("Original receipt temporarily unavailable.")) : Task.CompletedTask;
            throw new IOException("Original publication acknowledgement lost.");
        };
        try {
            await Assert.ThrowsAnyAsync<Exception>(fixture.PublishAsync);
        } finally {
            fixture.Entry.Files.Sql.Probe.BeforeCommand = null;
        }
        var id = await AdmitAsync(fixture);
        var state = await StateAsync(fixture.Services, fixture.State.RunId);
        var before = await RetainedAsync(fixture.Services, state.RunId);
        var pending = Assert.IsType<ProcessDecisionRefreshAccountingResult.Pending>(await Coordinator(fixture.Services)
            .ReconcileAsync(state, fixture.CommandContext, id));
        Assert.Equal(ProcessDecisionChildPendingReason.ActiveWork, pending.Reason);
        Assert.Equal(before, await RetainedAsync(fixture.Services, state.RunId));
        Assert.NotNull((await fixture.Entry.Files.Store.GetExecutionRunAsync(fixture.Waiting.Id))!.ToolAdmission!.PlanningDecision!.Work.Open);
        await fixture.Entry.Files.Sql.VerifyAsync(1, 1);
    }

    public enum ChangedCommand { MissingAuthority, Allocation, SharedScope, Result }

    [Theory]
    [InlineData(ChangedCommand.MissingAuthority)]
    [InlineData(ChangedCommand.Allocation)]
    [InlineData(ChangedCommand.SharedScope)]
    [InlineData(ChangedCommand.Result)]
    public async Task A_forged_commit_cannot_borrow_the_frozen_shared_scope(ChangedCommand change) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(ReadBasisMode.RunSnapshot);
        var id = await PrepareAsync(fixture, accepted: true);
        var state = await StateAsync(fixture.Services, fixture.State.RunId);
        var selected = await SelectedAsync(fixture);
        var admission = (await fixture.Services.GetRequiredService<IProcessDecisionRefreshReceiptReader>().ReadAsync(state.RunId, id))!;
        var closed = Assert.IsType<ProcessDecisionChildResult.Closed>(await Assert.Single(fixture.Services
            .GetServices<IProcessDecisionChildOutcomeReader>()).ReadAsync(fixture.Parent));
        var request = ProcessDecisionRefreshAccountingPreparation.Create(state, fixture.CommandContext, admission, selected.Child!, closed);
        request = change switch {
            ChangedCommand.MissingAuthority => request with { RefreshAccounting = null },
            ChangedCommand.Allocation => request with { DecisionBudget = ((ProcessDecisionBudgetSettlement)request.DecisionBudget!) with { AllocationId = new(Guid.NewGuid()) } },
            ChangedCommand.SharedScope => request with { DecisionRunScope = request.DecisionRunScope! with { Expectation = admission.SharedScope!.Command.Expectation } },
            ChangedCommand.Result => request with { Mutation = request.Mutation with { State = request.Mutation.State with {
                AppliedResults = [request.Mutation.State.AppliedResults[0], request.Mutation.State.AppliedResults[1] with { ResultHash = "sha256:" + new string('f', 64) }]
            } } },
            _ => throw new ArgumentOutOfRangeException(nameof(change))
        };
        var before = await RetainedAsync(fixture.Services, state.RunId);
        await using var database = Context(fixture.Services);
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => Owner(fixture, database).CommitAsync(request));
        Assert.Equal(before, await RetainedAsync(fixture.Services, state.RunId));
        await fixture.Entry.Files.Sql.VerifyAsync(1, 1);
    }

    private static ProcessRuntimeCommitResult Commit(ProcessDecisionRefreshAccountingResult result) => result switch {
        ProcessDecisionRefreshAccountingResult.Reconciled reconciled => reconciled.Commit,
        ProcessDecisionRefreshAccountingResult.CompletedOriginal completed => completed.Commit,
        _ => throw new InvalidOperationException("The original child was not accounted for.")
    };

    private static async Task<RuntimeCommandId> PrepareAsync(ProjectTaskPlanReviewFixture fixture, bool accepted) {
        if (accepted) {
            await fixture.CloseWithoutAccountingAsync(AgentPlanningReviewChoice.Approve);
        }
        var id = await AdmitAsync(fixture);
        Assert.Equal(ProcessDecisionChildStopResult.ReadyForAccounting,
            await Assert.Single(fixture.Services.GetServices<IProcessDecisionRefreshChildStopper>()).StopAsync(fixture.Parent, id));
        return id;
    }

    private static async Task<RuntimeCommandId> AdmitAsync(ProjectTaskPlanReviewFixture fixture) {
        var selection = await SelectedAsync(fixture);
        var review = fixture.Request;
        ProcessDecisionRefreshRequest request = new(new(Guid.NewGuid()), review.Child, review.StrategyId, review.WaitingResultKey,
            review.Decision, review.Budget, selection.SharedScope, ProcessDecisionRefreshKind.ManagerRefresh);
        Assert.True((await fixture.Services.GetRequiredService<ProcessDecisionRefreshCoordinator>()
            .AdmitAsync(fixture.State, fixture.CommandContext, request)).CommittedNow);
        return request.CommandId;
    }

    private static ProcessDecisionRefreshAccountingCoordinator Coordinator(IServiceProvider services,
        IProcessRuntimeUnitOfWork? owner = null, IProcessIdempotencyStore? receipts = null, IEnumerable<IProcessDecisionChildOutcomeReader>? readers = null)
        => new(services.GetRequiredService<IProcessAcceptedExecutionInputReader>(), services.GetRequiredService<IProcessDecisionResultQuery>(),
            services.GetRequiredService<IProcessDecisionRefreshReceiptReader>(), readers ?? services.GetServices<IProcessDecisionChildOutcomeReader>(),
            receipts ?? services.GetRequiredService<IProcessIdempotencyStore>(), owner ?? services.GetRequiredService<IProcessRuntimeUnitOfWork>(),
            NullLogger<ProcessDecisionRefreshAccountingCoordinator>.Instance);

    private static EfProcessRuntimeUnitOfWork Owner(ProjectTaskPlanReviewFixture fixture, ProcessPersistenceDbContext context)
        => new(context, fixture.Clock, runtimeDatabase: fixture.Services.GetRequiredService<ICanonicalRuntimeDatabase>());

    private static async Task<ProcessRuntimeStateSnapshot> StateAsync(IServiceProvider services, ProcessRunId runId)
        => (await services.GetRequiredService<IProcessRuntimeStateStore>().LoadAsync(runId))!;

    private static async Task<ProcessDecisionResultSelection> SelectedAsync(ProjectTaskPlanReviewFixture fixture)
        => (await fixture.Services.GetRequiredService<IProcessDecisionResultQuery>()
            .ReadAsync(fixture.State.RunId, fixture.Request.Child.StepInstanceId, new(fixture.Waiting.Id)))!;

    private static async Task VerifyScopeAsync(IServiceProvider services, RuntimeCommandId id, ReadBasisMode mode, ProcessDecisionRunScopePhase phase) {
        await using var context = Context(services);
        var scopes = await context.DecisionRunScopes.AsNoTracking().ToArrayAsync();
        if (mode == ReadBasisMode.StepSnapshot) {
            Assert.Empty(scopes);
            return;
        }
        Assert.Equal(phase, Assert.Single(scopes).Phase);
        var generation = Assert.Single(await context.DecisionGenerations.AsNoTracking().ToArrayAsync());
        Assert.Equal(id.Value, generation.RefreshCommandId);
        Assert.Equal(0, generation.UnclosedChildCount);
        Assert.Equal(generation.MaterialId, scopes[0].CurrentMaterialId);
    }

    private static async Task<string> RetainedAsync(IServiceProvider services, ProcessRunId runId) {
        await using var context = Context(services);
        return JsonSerializer.Serialize(new {
            Runtime = await StateAsync(services, runId),
            Budgets = await context.DecisionBudgets.AsNoTracking().OrderBy(row => row.BudgetId).ToArrayAsync(),
            Allocations = await context.DecisionBudgetAllocations.AsNoTracking().OrderBy(row => row.BudgetId).ThenBy(row => row.AllocationId).ToArrayAsync(),
            Generations = await context.DecisionGenerations.AsNoTracking().OrderBy(row => row.Generation).ToArrayAsync(),
            Children = await context.DecisionChildren.AsNoTracking().OrderBy(row => row.ChildId).ToArrayAsync(),
            Scopes = await context.DecisionRunScopes.AsNoTracking().OrderBy(row => row.ScopeId).ToArrayAsync(),
            Events = await context.RuntimeEvents.CountAsync(), Outbox = await context.OutboxMessages.CountAsync(),
            Commands = await context.IdempotencyKeys.CountAsync(), Materials = await context.DecisionMaterials.CountAsync()
        });
    }

    private sealed class BeforeCommit(IProcessRuntimeUnitOfWork inner, Func<CancellationToken, Task> before) : IProcessRuntimeUnitOfWork {
        public async Task<ProcessRuntimeCommitResult> CommitAsync(ProcessRuntimeCommitRequest request, CancellationToken cancellationToken = default) {
            await before(cancellationToken);
            return await inner.CommitAsync(request, cancellationToken);
        }
    }

    private sealed class Rendezvous {
        private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int arrived;
        internal async Task WaitAsync(CancellationToken token) {
            if (Interlocked.Increment(ref arrived) == 2) {
                ready.TrySetResult();
            }
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(30), token);
        }
    }

    private sealed class AfterFlush(Exception failure) : SaveChangesInterceptor {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default) => ValueTask.FromException<int>(failure);
    }

    private sealed class AfterCommit(Exception failure) : DbTransactionInterceptor {
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default) => throw failure;
    }

    private sealed class ReadForbidden : IProcessDecisionChildOutcomeReader {
        public ProcessDecisionExecutionContract Contract => ProjectTaskPlanProcessContract.Execution;
        public Task<ProcessDecisionChildResult> ReadAsync(ProcessDecisionChildMaterialBinding original, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("An accounted refresh must replay without reading the child owner.");
    }
}
