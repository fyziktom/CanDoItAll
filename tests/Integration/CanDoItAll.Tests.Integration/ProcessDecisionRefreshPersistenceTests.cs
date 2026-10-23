using System.Data.Common;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;

namespace CanDoItAll.Tests.Integration;

[Trait("Category", "FileSystemPortability")]
public sealed class ProcessDecisionRefreshPersistenceTests(ITestOutputHelper output) {
    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public async Task Admission_is_atomic_immutable_discoverable_and_replayed_without_reading_runtime_history(ReadBasisMode mode) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(mode);
        var request = await RefreshAsync(fixture);
        var beforeAgent = JsonSerializer.Serialize(await fixture.Entry.Files.Store.GetExecutionRunDetailAsync(fixture.Waiting.Id));
        await using var database = Context(fixture.Services);
        var before = await BalanceAsync(database);
        var accepted = await fixture.Services.GetRequiredService<ProcessDecisionRefreshCoordinator>()
            .AdmitAsync(fixture.State, fixture.CommandContext, request);
        Assert.True(accepted.CommittedNow);
        Assert.Equal(ProcessDecisionPhase.Reconciling, accepted.Receipt.Decision.Current.Phase);
        Assert.Equal(request.CommandId, accepted.Receipt.Decision.Current.RefreshCommandId);
        Assert.Equal(mode == ReadBasisMode.RunSnapshot, accepted.Receipt.SharedScope is not null);
        if (accepted.Receipt.SharedScope is { } scope) {
            Assert.Equal(ProcessDecisionRunScopePhase.Reconciling, scope.State.Phase);
            Assert.Equal(request.SharedScope!.Revision + 1, scope.State.Revision);
        }
        var current = (await fixture.Services.GetRequiredService<IProcessRuntimeStateStore>().LoadAsync(fixture.State.RunId))!;
        Assert.Equal(JsonSerializer.Serialize(fixture.State), JsonSerializer.Serialize(current with { UpdatedAtUtc = fixture.State.UpdatedAtUtc }));
        Assert.Equal(before, await BalanceAsync(database));
        Assert.Equal(beforeAgent, JsonSerializer.Serialize(await fixture.Entry.Files.Store.GetExecutionRunDetailAsync(fixture.Waiting.Id)));
        await using var restarted = fixture.Services.CreateAsyncScope();
        var observer = new ReadProbe();
        var receipts = new EfProcessDecisionRefreshReceiptReader(new ContextFactory(Options(restarted.ServiceProvider, observer)));
        Assert.Equal(accepted.Receipt, await receipts.ReadAsync(current.RunId, request.CommandId));
        var exact = Assert.Single(observer.Reads);
        Assert.Contains("process_runtime_idempotency_keys", exact.Sql, StringComparison.Ordinal);
        Assert.Contains(current.RunId.Value, exact.Values);
        Assert.Contains(request.CommandId.Value, exact.Values);
        Assert.DoesNotContain("process_runtime_states", exact.Sql, StringComparison.Ordinal);
        observer.Reads.Clear();
        Assert.Equal(accepted.Receipt, await receipts.ReadCurrentAsync(current.RunId, request.Child.StepInstanceId));
        var discovery = Assert.Single(observer.Reads);
        Assert.DoesNotContain("process_runtime_results", discovery.Sql, StringComparison.Ordinal);
        Assert.DoesNotContain("PayloadJson", discovery.Sql, StringComparison.Ordinal);
        var coordinator = restarted.ServiceProvider.GetRequiredService<ProcessDecisionRefreshCoordinator>();
        var replay = await coordinator.AdmitAsync(current, fixture.CommandContext, request);
        Assert.False(replay.CommittedNow);
        Assert.Equal(accepted.Receipt, replay.Receipt);
        await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.AdmitAsync(current,
            fixture.CommandContext with { Actor = new(ProcessEventActorKind.Manager, new("different-manager")) }, request));
        await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.AdmitAsync(current, fixture.CommandContext,
            request with { Budget = request.Budget with { Revision = request.Budget.Revision + 1 } }));
        Assert.Single(fixture.Entry.Wire.Requests);
        await fixture.Entry.Files.Sql.VerifyAsync(0, 0);
        var json = (await database.IdempotencyKeys.AsNoTracking().SingleAsync(row => row.RunId == current.RunId.Value && row.CommandId == request.CommandId.Value)).DecisionReceiptJson!;
        Assert.Equal(7, JsonNode.Parse(json)!["version"]!.GetValue<int>());
        output.WriteLine("process-refresh-wire:" + json);
    }

    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot, false)]
    [InlineData(ReadBasisMode.RunSnapshot, true)]
    [InlineData(ReadBasisMode.StepSnapshot, false)]
    [InlineData(ReadBasisMode.StepSnapshot, true)]
    public async Task Failure_before_or_after_flush_rolls_back_local_scope_and_receipt_together(ReadBasisMode mode, bool after) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(mode);
        var request = await CommitAsync(fixture);
        await using var check = Context(fixture.Services);
        var before = await BalanceAsync(check);
        var fault = new FlushFault(after);
        await using var failed = Context(fixture.Services, fault);
        Assert.Same(fault.Failure, await Assert.ThrowsAsync<InvalidOperationException>(() => Owner(fixture, failed).CommitAsync(request)));
        Assert.Equal(after, fault.Flushed);
        var head = (await new EfProcessDecisionStore(check).ReadAsync(fixture.State.RunId, request.Decision!.StepInstanceId))!;
        Assert.Equal(ProcessDecisionPhase.WaitingForReview, head.Phase);
        Assert.Null(head.RefreshCommandId);
        Assert.False(await check.IdempotencyKeys.AnyAsync(row => row.RunId == fixture.State.RunId.Value && row.CommandId == request.CommandId.Value));
        if (request.DecisionRunScope is { } scope) {
            var state = (await new EfProcessDecisionRunScopeStore(check, fixture.Services.GetRequiredService<ICanonicalRuntimeDatabase>())
                .ReadAsync(scope.Expectation.ScopeId))!;
            Assert.Equal(scope.Expectation, state.Expectation);
            Assert.Equal(ProcessDecisionRunScopePhase.Active, state.Phase);
        }
        Assert.Equal(before, await BalanceAsync(check));
        Assert.True((await Owner(fixture, check).CommitAsync(request)).CommittedNow);
        await fixture.Entry.Files.Sql.VerifyAsync(0, 0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Concurrent_admissions_accept_one_action_and_replay_only_its_exact_identity(bool sameAction) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(ReadBasisMode.RunSnapshot);
        var first = await CommitAsync(fixture);
        var begin = Assert.IsType<ProcessDecisionBeginRefresh>(first.Decision);
        var second = sameAction ? first : await CommitAsync(fixture, begin.Intent.Request with { ActionId = new(Guid.NewGuid()) });
        await using var left = Context(fixture.Services);
        await using var right = Context(fixture.Services);
        var results = await Task.WhenAll(AttemptAsync(left, first), AttemptAsync(right, second));
        Assert.Single(results, item => item.Result?.CommittedNow == true);
        if (sameAction) {
            Assert.All(results, item => Assert.Null(item.Error));
            Assert.Equal(results[0].Result!.Decision, results[1].Result!.Decision);
        } else {
            Assert.Single(results, item => item.Error is ProcessRuntimeOptimisticConcurrencyException);
        }
        await using var check = Context(fixture.Services);
        Assert.Equal(1, await check.IdempotencyKeys.CountAsync(row => row.RunId == fixture.State.RunId.Value &&
            (row.CommandId == first.CommandId.Value || row.CommandId == second.CommandId.Value)));
        await fixture.Entry.Files.Sql.VerifyAsync(0, 0);

        async Task<(ProcessRuntimeCommitResult? Result, Exception? Error)> AttemptAsync(ProcessPersistenceDbContext context, ProcessRuntimeCommitRequest request) {
            ProcessRuntimeCommitResult? result = null;
            var error = await Record.ExceptionAsync(async () => result = await Owner(fixture, context).CommitAsync(request));
            return (result, error);
        }
    }

    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public async Task Lost_acknowledgement_replays_original_acceptance_after_root_cancellation(ReadBasisMode mode) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(mode);
        var request = await RefreshAsync(fixture);
        var lost = new IOException("Refresh admission acknowledgement lost.");
        var coordinator = ActivatorUtilities.CreateInstance<ProcessDecisionRefreshCoordinator>(fixture.Services,
            new LostAcknowledgement(fixture.Services.GetRequiredService<IProcessRuntimeUnitOfWork>(), lost));
        Assert.Same(lost, await Assert.ThrowsAsync<IOException>(() => coordinator.AdmitAsync(fixture.State, fixture.CommandContext, request)));
        var current = (await fixture.Services.GetRequiredService<IProcessRuntimeStateStore>().LoadAsync(fixture.State.RunId))!;
        var stopped = await new ProcessRuntimeEngine(fixture.Services.GetRequiredService<IProcessRuntimeUnitOfWork>())
            .BeginRootCancellationAsync(current, fixture.CommandContext);
        Assert.True(stopped.CommittedNow);
        await using var restarted = fixture.Services.CreateAsyncScope();
        var replay = await restarted.ServiceProvider.GetRequiredService<ProcessDecisionRefreshCoordinator>()
            .AdmitAsync(stopped.State, fixture.CommandContext, request);
        Assert.False(replay.CommittedNow);
        Assert.Equal(ProcessDecisionPhase.Reconciling, replay.Receipt.Decision.Current.Phase);
        Assert.Equal(request.CommandId, replay.Receipt.Decision.Current.RefreshCommandId);
        Assert.Single(fixture.Entry.Wire.Requests);
        await fixture.Entry.Files.Sql.VerifyAsync(0, 0);
    }

    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public async Task Cancellation_committing_first_refuses_an_already_prepared_refresh(ReadBasisMode mode) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(mode);
        var request = await CommitAsync(fixture);
        await new ProcessRuntimeEngine(fixture.Services.GetRequiredService<IProcessRuntimeUnitOfWork>())
            .BeginRootCancellationAsync(fixture.State, fixture.CommandContext);
        await using var check = Context(fixture.Services);
        await Assert.ThrowsAsync<ProcessRuntimeOptimisticConcurrencyException>(() => Owner(fixture, check).CommitAsync(request));
        Assert.False(await check.IdempotencyKeys.AnyAsync(row => row.RunId == fixture.State.RunId.Value && row.CommandId == request.CommandId.Value));
        Assert.Null((await new EfProcessDecisionStore(check).ReadAsync(fixture.State.RunId, request.Decision!.StepInstanceId))!.RefreshCommandId);
    }

    [Fact]
    public async Task Raw_admission_cannot_change_business_state_budget_or_shared_scope_or_invent_original_input() {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(ReadBasisMode.RunSnapshot);
        var request = await CommitAsync(fixture);
        var begin = Assert.IsType<ProcessDecisionBeginRefresh>(request.Decision);
        var invalid = new[] {
            request with { DecisionRunScope = null },
            request with { DecisionRunScope = request.DecisionRunScope! with { Expectation = request.DecisionRunScope!.Expectation with { Revision = request.DecisionRunScope.Expectation.Revision + 1 } } },
            request with { CommandId = RuntimeCommandId.New() },
            request with { Mutation = request.Mutation with { State = request.Mutation.State with { Status = ProcessRuntimeStatus.Completed } } },
            request with { DecisionBudget = new ProcessDecisionBudgetReservation(begin.Intent.Request.Budget, new(Guid.NewGuid()), new(1, 0)) },
            request with { Decision = begin with { Intent = begin.Intent with { InputFingerprint = "sha256:" + new string('a', 64) } } },
            request with { Decision = begin with { Intent = begin.Intent with { ChildFingerprint = "sha256:" + new string('b', 64) } } }
        };
        foreach (var changed in invalid) {
            await using var context = Context(fixture.Services);
            var error = await Record.ExceptionAsync(() => Owner(fixture, context).CommitAsync(changed));
            Assert.True(error is InvalidOperationException or InvalidDataException, error?.ToString());
            Assert.False(await context.IdempotencyKeys.AnyAsync(row => row.RunId == fixture.State.RunId.Value && row.CommandId == changed.CommandId.Value));
        }
        Assert.True((await fixture.Services.GetRequiredService<IProcessRuntimeUnitOfWork>().CommitAsync(request)).CommittedNow);
    }

    [Fact]
    public async Task An_actual_Project_publication_holds_refresh_until_its_effect_commits() {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(ReadBasisMode.RunSnapshot);
        var request = await CommitAsync(fixture);
        TaskCompletionSource flushed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Entry.Files.Sql.Faults.Save.AfterFlush = async token => {
            flushed.TrySetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(30), token);
        };
        var publication = Record.ExceptionAsync(fixture.PublishAsync);
        await flushed.Task.WaitAsync(TimeSpan.FromSeconds(20));
        var probe = new FenceProbe();
        await using var context = Context(fixture.Services, probe);
        var before = await BalanceAsync(context);
        var admission = Owner(fixture, context).CommitAsync(request);
        try {
            await probe.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
            Assert.False(admission.IsCompleted);
        } finally {
            release.TrySetResult();
            await Task.WhenAll(publication, admission).WaitAsync(TimeSpan.FromSeconds(25));
        }
        Assert.True((await admission.WaitAsync(TimeSpan.FromSeconds(20))).CommittedNow);
        Assert.True(await publication is null or AgentToolReceiptAccessDeniedException, (await publication)?.ToString());
        Assert.Equal(ProcessDecisionPhase.Reconciling, (await new EfProcessDecisionStore(context)
            .ReadAsync(fixture.State.RunId, request.Decision!.StepInstanceId))!.Phase);
        Assert.Equal(before, await BalanceAsync(context));
        await fixture.Entry.Files.Sql.VerifyAsync(1, 1);
        Assert.Single(fixture.Entry.Wire.Requests);
    }

    [Fact]
    public async Task Refresh_committing_first_revokes_the_real_saved_publication_without_an_effect() {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync();
        await fixture.Services.GetRequiredService<ProcessDecisionRefreshCoordinator>()
            .AdmitAsync(fixture.State, fixture.CommandContext, await RefreshAsync(fixture));
        await Assert.ThrowsAsync<AgentToolReceiptAccessDeniedException>(() => fixture.PublishAsync());
        await fixture.Entry.Files.Sql.VerifyAsync(0, 0);
        Assert.Single(fixture.Entry.Wire.Requests);
    }

    [Fact]
    public async Task Generic_advance_cannot_bypass_or_change_the_original_admitted_refresh() {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync();
        var action = await RefreshAsync(fixture);
        var accepted = await fixture.Services.GetRequiredService<ProcessDecisionRefreshCoordinator>()
            .AdmitAsync(fixture.State, fixture.CommandContext, action);
        var current = (await fixture.Services.GetRequiredService<IProcessRuntimeStateStore>().LoadAsync(fixture.State.RunId))!;
        var command = new ProcessDecisionRefresh(action.Child.StepInstanceId, accepted.Receipt.Decision.Current.Expectation,
            new(Guid.NewGuid()), ProcessDecisionRefreshKind.ManagerRefresh);
        foreach (var invalid in new[] {
            command,
            command with { AdmissionCommandId = RuntimeCommandId.New() },
            command with { AdmissionCommandId = action.CommandId, Kind = ProcessDecisionRefreshKind.AutomaticReplan }
        }) {
            await using var context = Context(fixture.Services);
            var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => Owner(fixture, context).CommitAsync(Request(current, invalid)));
            Assert.Contains("original", refused.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(accepted.Receipt.Decision.Current, await new EfProcessDecisionStore(context).ReadAsync(current.RunId, action.Child.StepInstanceId));
        }
        await fixture.Entry.Files.Sql.VerifyAsync(0, 0);
    }

    [Fact]
    public async Task New_receipts_fail_closed_for_downgraded_versions_missing_intent_or_wrong_generation_reference() {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync();
        var request = await CommitAsync(fixture);
        await fixture.Services.GetRequiredService<IProcessRuntimeUnitOfWork>().CommitAsync(request);
        await using var context = Context(fixture.Services);
        var json = (await context.IdempotencyKeys.AsNoTracking().SingleAsync(row => row.RunId == fixture.State.RunId.Value && row.CommandId == request.CommandId.Value)).DecisionReceiptJson!;
        var codec = typeof(EfProcessDecisionStore).Assembly.GetType("CanDoItAll.Processes.Persistence.ProcessDecisionReceiptCodec", throwOnError: true)!;
        var read = codec.GetMethod("Read", BindingFlags.Static | BindingFlags.NonPublic)!;
        foreach (var change in new Action<JsonNode>[] {
            node => node["version"] = 6,
            node => node["command"]!["refreshIntent"] = null,
            node => node["current"]!["refreshCommandId"] = Guid.NewGuid().ToString("D"),
            node => node["command"]!["refreshIntent"]!["kind"] = (int)ProcessDecisionRefreshKind.AutomaticReplan
        }) {
            var changed = JsonNode.Parse(json)!;
            change(changed);
            var error = Assert.Throws<TargetInvocationException>(() => read.Invoke(null, [changed.ToJsonString()]));
            Assert.IsType<InvalidDataException>(error.InnerException);
        }
    }

    private static async Task<ProcessDecisionRefreshRequest> RefreshAsync(ProjectTaskPlanReviewFixture fixture) {
        var review = fixture.Request;
        var selection = (await fixture.Services.GetRequiredService<IProcessDecisionResultQuery>()
            .ReadAsync(fixture.State.RunId, review.Child.StepInstanceId, new(review.Child.ChildId.Value)))!;
        return new(new(Guid.NewGuid()), review.Child, review.StrategyId, review.WaitingResultKey,
            review.Decision, review.Budget, selection.SharedScope, ProcessDecisionRefreshKind.ManagerRefresh);
    }

    private static async Task<ProcessRuntimeCommitRequest> CommitAsync(ProjectTaskPlanReviewFixture fixture, ProcessDecisionRefreshRequest? original = null) {
        var request = original ?? await RefreshAsync(fixture);
        var input = await fixture.Services.GetRequiredService<IProcessAcceptedExecutionInputReader>().ReadAsync(fixture.State.RunId, request.Child.StepInstanceId);
        var selection = (await fixture.Services.GetRequiredService<IProcessDecisionResultQuery>()
            .ReadAsync(fixture.State.RunId, request.Child.StepInstanceId, new(request.Child.ChildId.Value)))!;
        var command = ProcessDecisionRefreshPreparation.Create(fixture.State, input, selection, request, fixture.CommandContext.Actor);
        return new(request.CommandId, fixture.State, ProcessRuntimeMutation.Applied(
            fixture.State with { UpdatedAtUtc = fixture.State.UpdatedAtUtc.AddTicks(10) }, [], [])) {
            Decision = command, DecisionRunScope = ProcessDecisionRefreshReceipt.ScopeCommand(request)
        };
    }

    private static EfProcessRuntimeUnitOfWork Owner(ProjectTaskPlanReviewFixture fixture, ProcessPersistenceDbContext context)
        => new(context, fixture.Clock, runtimeDatabase: fixture.Services.GetRequiredService<ICanonicalRuntimeDatabase>());

    private static async Task<string> BalanceAsync(ProcessPersistenceDbContext context)
        => JsonSerializer.Serialize(await context.DecisionBudgets.AsNoTracking().OrderBy(row => row.BudgetId).ToArrayAsync());

    private sealed class ContextFactory(DbContextOptions<ProcessPersistenceDbContext> options) : IDbContextFactory<ProcessPersistenceDbContext> {
        public ProcessPersistenceDbContext CreateDbContext() => new(options);
    }

    private sealed class FlushFault(bool after) : SaveChangesInterceptor {
        internal Exception Failure { get; } = new InvalidOperationException("Refresh admission failed at the SQL flush boundary.");
        internal bool Flushed { get; private set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default) => after ? ValueTask.FromResult(result) : throw Failure;
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default) {
            Flushed = result > 0 && eventData.Context!.Database.CurrentTransaction is not null;
            throw Failure;
        }
    }

    private sealed class LostAcknowledgement(IProcessRuntimeUnitOfWork inner, Exception failure) : IProcessRuntimeUnitOfWork {
        public async Task<ProcessRuntimeCommitResult> CommitAsync(ProcessRuntimeCommitRequest request, CancellationToken cancellationToken) {
            var result = await inner.CommitAsync(request, cancellationToken);
            Assert.True(result.CommittedNow);
            throw failure;
        }
    }

    private sealed class ReadProbe : DbCommandInterceptor {
        internal List<(string Sql, object[] Values)> Reads { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            Reads.Add((command.CommandText, command.Parameters.Cast<DbParameter>().Select(item => item.Value).ToArray()));
            return ValueTask.FromResult(result);
        }
    }

    private sealed class FenceProbe : DbCommandInterceptor {
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            if (command.CommandText.Contains("pg_advisory_xact_lock", StringComparison.Ordinal)) {
                Entered.TrySetResult();
            }
            return ValueTask.FromResult(result);
        }
    }
}
