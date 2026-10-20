using System.Data;
using System.Data.Common;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed class ProcessDecisionPublicationFenceTests {
    [Fact]
    public async Task Composition_reopens_the_exact_enrollment_and_original_grant_after_host_restart() {
        await using var environment = CanDoItAllTestEnvironment.Create("process-decision-publication");
        var profile = environment.CreatePostgreSqlProfile("child-publication-restart");
        var harness = new ProcessDecisionPublicationTestClock().Harness(environment, profile);
        ProcessDecisionChildAdmission original;
        await using (var app = await TestApplication.CreateAsync(harness)) {
            await using var scope = app.Services.CreateAsyncScope();
            await using var context = Context(scope.ServiceProvider);
            var enrolled = await RunningChildAsync(context);
            original = (await scope.ServiceProvider.GetRequiredService<EfProcessDecisionChildQuery>().ReadAsync(Address(enrolled)))!.Admission;
            Assert.Equal(20, original.Grant.WorkTicks);
            await RequireAsync(scope.ServiceProvider, original);
        }
        await using var restarted = await TestApplication.CreateAsync(harness);
        await using var reopened = restarted.Services.CreateAsyncScope();
        var retained = await reopened.ServiceProvider.GetRequiredService<EfProcessDecisionChildQuery>().ReadAsync(original.Address);
        Assert.Equal(original, retained!.Admission);
        Assert.True(retained.IsCurrent);
        Assert.Null(retained.Closure);
        Assert.Null(retained.Settlement);
        await RequireAsync(reopened.ServiceProvider, original);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    public async Task A_changed_scope_generation_occurrence_material_owner_or_grant_cannot_publish(int change) {
        await using var app = await TestApplication.CreateAsync(new ProcessDecisionPublicationTestClock().Harness());
        await using var scope = app.Services.CreateAsyncScope();
        await using var context = Context(scope.ServiceProvider);
        var enrolled = await RunningChildAsync(context);
        var original = await AdmissionAsync(scope.ServiceProvider, enrolled);
        var changed = change switch {
            0 => original with { Address = original.Address with { RootRunId = new(Guid.NewGuid()) } },
            1 => original with { Address = original.Address with { RunId = new(Guid.NewGuid()) } },
            2 => original with { Address = original.Address with { StepInstanceId = new(Guid.NewGuid()) } },
            3 => original with { Address = original.Address with { Reference = new(new(Guid.NewGuid()), 1) } },
            4 => original with { Address = original.Address with { Reference = original.Address.Reference with { Generation = 2 } } },
            5 => original with { Address = original.Address with { ChildId = new(Guid.NewGuid()) } },
            6 => original with { MaterialId = new(Guid.NewGuid()) },
            7 => original with { Owner = ProcessDecisionChildOwner.Workflow },
            8 => original with { BudgetId = new(Guid.NewGuid()) },
            9 => original with { AllocationId = new(Guid.NewGuid()) },
            _ => original with { Grant = original.Grant with { WorkTicks = original.Grant.WorkTicks + 1 } }
        };
        await Assert.ThrowsAsync<ProcessExecutionAuthorityMismatchException>(() => RequireAsync(scope.ServiceProvider, changed));
        await RequireAsync(scope.ServiceProvider, original);
    }

    [Fact]
    public async Task Claim_rotation_and_sibling_enrollment_preserve_the_original_occurrence_and_allocation() {
        await using var app = await TestApplication.CreateAsync(new ProcessDecisionPublicationTestClock().Harness());
        await using var scope = app.Services.CreateAsyncScope();
        await using var context = Context(scope.ServiceProvider);
        var first = await RunningChildAsync(context);
        var original = await AdmissionAsync(scope.ServiceProvider, first);
        var sibling = await EnrollAsync(context, first.State, ProcessDecisionChildOwner.Workflow);
        await ClaimAsync(context, sibling.State);
        var read = await scope.ServiceProvider.GetRequiredService<EfProcessDecisionChildQuery>().ReadAsync(original.Address);
        Assert.Equal(original, read!.Admission);
        Assert.True(read.Generation.Revision > first.Decision!.Current.Revision);
        await RequireAsync(scope.ServiceProvider, original);
    }

    [Theory]
    [InlineData(ProcessRuntimeStatus.Active)]
    [InlineData(ProcessRuntimeStatus.Waiting)]
    [InlineData(ProcessRuntimeStatus.Blocked)]
    [InlineData(ProcessRuntimeStatus.WaitingForUser)]
    public async Task Manager_review_requires_the_waiting_generation_and_released_claim(ProcessRuntimeStatus runStatus) {
        await using var app = await TestApplication.CreateAsync(new ProcessDecisionPublicationTestClock().Harness());
        await using var scope = app.Services.CreateAsyncScope();
        await using var context = Context(scope.ServiceProvider);
        var first = await RunningChildAsync(context);
        var original = await AdmissionAsync(scope.ServiceProvider, first);
        await Assert.ThrowsAsync<ProcessExecutionAuthorityMismatchException>(() => RequireAsync(scope.ServiceProvider, original, ProcessDecisionPublicationKind.ManagerReview));
        var waiting = await QuiesceAsync(context, first.State, ProcessDecisionPhaseChange.WaitForReview);
        var request = Request(waiting.State, null);
        await new EfProcessRuntimeUnitOfWork(context).CommitAsync(request with { Mutation = request.Mutation with { State = request.Mutation.State with { Status = runStatus } } });
        await RequireAsync(scope.ServiceProvider, original, ProcessDecisionPublicationKind.ManagerReview);
        await Assert.ThrowsAsync<ProcessExecutionAuthorityMismatchException>(() => RequireAsync(scope.ServiceProvider, original));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task Runtime_stop_terminal_step_or_missing_claim_revokes_dispatch(int change) {
        await using var app = await TestApplication.CreateAsync(new ProcessDecisionPublicationTestClock().Harness());
        await using var scope = app.Services.CreateAsyncScope();
        await using var context = Context(scope.ServiceProvider);
        var first = await RunningChildAsync(context);
        var original = await AdmissionAsync(scope.ServiceProvider, first);
        var state = first.State;
        var changed = change switch {
            0 => state with { Status = ProcessRuntimeStatus.CancelRequested },
            1 => state with { Status = ProcessRuntimeStatus.Cancelled },
            2 => state with { Status = ProcessRuntimeStatus.Completed },
            3 => state with { Steps = [state.Steps.Single() with { Status = ProcessRuntimeStepStatus.Failed }] },
            4 => state with { Steps = [state.Steps.Single() with { ActiveClaimToken = null }] },
            _ => state with { Claims = state.Claims.Select(claim => claim with { Status = DispatchClaimStatus.Expired }).ToArray() }
        };
        var request = Request(state, null);
        await new EfProcessRuntimeUnitOfWork(context).CommitAsync(request with { Mutation = request.Mutation with { State = changed with { UpdatedAtUtc = request.Mutation.State.UpdatedAtUtc } } });
        await Assert.ThrowsAsync<ProcessExecutionAuthorityMismatchException>(() => RequireAsync(scope.ServiceProvider, original));
    }

    [Theory]
    [InlineData(ProcessDecisionChildOutcome.Accepted)]
    [InlineData(ProcessDecisionChildOutcome.NoEffect)]
    public async Task Closed_and_superseded_children_remain_readable_but_cannot_publish(ProcessDecisionChildOutcome outcome) {
        await using var app = await TestApplication.CreateAsync(new ProcessDecisionPublicationTestClock().Harness());
        await using var scope = app.Services.CreateAsyncScope();
        await using var context = Context(scope.ServiceProvider);
        var first = await RunningChildAsync(context);
        var original = await AdmissionAsync(scope.ServiceProvider, first);
        var closure = new ProcessDecisionChildClosure(9, outcome, outcome == ProcessDecisionChildOutcome.Accepted ? new(Guid.NewGuid()) : null);
        var closed = await CloseAsync(context, first.State, first.Decision!.Child!, closure, 7);
        await Assert.ThrowsAsync<ProcessExecutionAuthorityMismatchException>(() => RequireAsync(scope.ServiceProvider, original));
        var reconciling = await QuiesceAsync(context, closed.State, ProcessDecisionPhaseChange.BeginReconciliation);
        var next = await RefreshAsync(context, reconciling.State, ProcessDecisionRefreshKind.ManagerRefresh);
        var historical = await scope.ServiceProvider.GetRequiredService<EfProcessDecisionChildQuery>().ReadAsync(original.Address);
        Assert.False(historical!.IsCurrent);
        Assert.Equal(closure, historical.Closure);
        Assert.Equal(7, historical.Settlement!.Consumed.WorkTicks);
        Assert.Equal(original, historical.Admission);
        Assert.Equal(ProcessDecisionPhase.Superseded, historical.Generation.Phase);
        Assert.NotEqual(ProcessDecisionChildIdentity.Create(original.Address.Reference, original.Owner, 1),
            ProcessDecisionChildIdentity.Create(next.Decision!.Current.Reference, original.Owner, 1));
        await Assert.ThrowsAsync<ProcessExecutionAuthorityMismatchException>(() => RequireAsync(scope.ServiceProvider, original));
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("boundary")]
    [InlineData("token")]
    [InlineData("attempt")]
    [InlineData("released")]
    [InlineData("expired-status")]
    public async Task Dispatch_requires_the_exact_active_attempt_and_unexpired_claim(string change) {
        var clock = new ProcessDecisionPublicationTestClock();
        await using var app = await TestApplication.CreateAsync(clock.Harness());
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await using var context = Context(services);
        var first = await RunningChildAsync(context);
        var original = await AdmissionAsync(services, first);
        await RequireAsync(services, original);
        var step = await context.RuntimeSteps.SingleAsync(item => item.RunId == first.State.RunId.Value);
        var claim = await context.DispatchClaims.SingleAsync(item => item.ClaimToken == step.ActiveClaimToken);
        switch (change) {
            case "expired":
                claim.ExpiresAtUtc = clock.GetUtcNow().AddMilliseconds(-1);
                break;
            case "boundary":
                claim.ExpiresAtUtc = clock.GetUtcNow();
                break;
            case "token":
                step.ActiveClaimToken = Guid.NewGuid();
                break;
            case "attempt":
                step.AttemptNumber++;
                break;
            case "released":
                claim.Status = DispatchClaimStatus.Released;
                break;
            case "expired-status":
                claim.Status = DispatchClaimStatus.Expired;
                break;
        }
        await context.SaveChangesAsync();
        await Assert.ThrowsAsync<ProcessExecutionAuthorityMismatchException>(() => RequireAsync(services, original));
        var retained = await services.GetRequiredService<IProcessDecisionChildReader>().ReadAsync(original.Address);
        Assert.Equal(original, retained!.Admission);
        Assert.Null(retained.Closure);
        Assert.Null(retained.Settlement);
    }

    [Theory]
    [InlineData(DispatchClaimStatus.Claimed)]
    [InlineData(DispatchClaimStatus.LeaseRenewed)]
    [InlineData(DispatchClaimStatus.Reclaimed)]
    public async Task Matching_dispatchable_claim_remains_valid_until_its_expiry(DispatchClaimStatus status) {
        var clock = new ProcessDecisionPublicationTestClock();
        await using var app = await TestApplication.CreateAsync(clock.Harness());
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await using var context = Context(services);
        var first = await RunningChildAsync(context);
        var original = await AdmissionAsync(services, first);
        var claim = await context.DispatchClaims.SingleAsync(item => item.RunId == first.State.RunId.Value);
        claim.Status = status;
        claim.ExpiresAtUtc = clock.GetUtcNow().AddMilliseconds(1);
        await context.SaveChangesAsync();
        await RequireAsync(services, original);
        clock.SetUtcNow(claim.ExpiresAtUtc);
        await Assert.ThrowsAsync<ProcessExecutionAuthorityMismatchException>(() => RequireAsync(services, original));
    }

    [Fact]
    public async Task Manager_review_refuses_a_retained_active_token_after_its_claim_is_released() {
        await using var app = await TestApplication.CreateAsync(new ProcessDecisionPublicationTestClock().Harness());
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await using var context = Context(services);
        var first = await RunningChildAsync(context);
        var original = await AdmissionAsync(services, first);
        await QuiesceAsync(context, first.State, ProcessDecisionPhaseChange.WaitForReview);
        var step = await context.RuntimeSteps.SingleAsync(item => item.RunId == first.State.RunId.Value);
        step.ActiveClaimToken = first.State.Steps.Single().ActiveClaimToken!.Value.Value;
        await context.SaveChangesAsync();
        await Assert.ThrowsAsync<ProcessExecutionAuthorityMismatchException>(() => RequireAsync(services, original, ProcessDecisionPublicationKind.ManagerReview));
    }

    [Fact]
    public async Task Waiting_for_the_root_fence_cannot_preserve_an_expired_claim_observation() {
        var clock = new ProcessDecisionPublicationTestClock();
        await using var app = await TestApplication.CreateAsync(clock.Harness());
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await using var setup = Context(services);
        var first = await RunningChildAsync(setup);
        var original = await AdmissionAsync(services, first);
        var expiry = first.State.Claims.Single().ExpiresAtUtc;
        await using var held = Context(services);
        await using var transaction = await held.Database.BeginTransactionAsync();
        Task<Exception> pending;
        var probe = new FenceEntryProbe();
        using (services.GetRequiredService<CoordinatedDatabaseTransaction>().Enter(held)) {
            await services.GetRequiredService<IProcessDecisionPublicationFence>().RequireAsync(original, ProcessDecisionPublicationKind.Dispatch);
        }
        pending = AttemptAsync();
        try {
            await probe.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
            Assert.False(pending.IsCompleted);
            clock.SetUtcNow(expiry);
        } finally {
            await transaction.RollbackAsync();
        }
        Assert.IsType<ProcessExecutionAuthorityMismatchException>(await pending.WaitAsync(TimeSpan.FromSeconds(15)));
        Assert.Equal(original, (await services.GetRequiredService<IProcessDecisionChildReader>().ReadAsync(original.Address))!.Admission);

        async Task<Exception> AttemptAsync() {
            await using var effect = Context(services, probe);
            await using var effectTransaction = await effect.Database.BeginTransactionAsync();
            using var participation = services.GetRequiredService<CoordinatedDatabaseTransaction>().Enter(effect);
            var fence = new EfProcessDecisionPublicationFence(Options(services, probe), services.GetRequiredService<CoordinatedDatabaseTransaction>(), clock);
            return await Assert.ThrowsAsync<ProcessExecutionAuthorityMismatchException>(() => fence.RequireAsync(original, ProcessDecisionPublicationKind.Dispatch));
        }
    }

    [Fact]
    public async Task A_siblings_retained_overrun_revokes_publication_without_settling_the_open_child() {
        await using var app = await TestApplication.CreateAsync(new ProcessDecisionPublicationTestClock().Harness());
        await using var scope = app.Services.CreateAsyncScope();
        await using var context = Context(scope.ServiceProvider);
        var first = await RunningChildAsync(context);
        var original = await AdmissionAsync(scope.ServiceProvider, first);
        var sibling = await EnrollAsync(context, first.State, ProcessDecisionChildOwner.Workflow);
        var budget = await BudgetAsync(context, sibling.State);
        await CloseAsync(context, sibling.State, sibling.Decision!.Child!, NoEffect(), budget.Ceiling.MaximumWorkDuration.Ticks + 1);
        Assert.Null((await scope.ServiceProvider.GetRequiredService<EfProcessDecisionChildQuery>().ReadAsync(original.Address))!.Settlement);
        await Assert.ThrowsAsync<ProcessExecutionAuthorityMismatchException>(() => RequireAsync(scope.ServiceProvider, original));
    }

    [Fact]
    public async Task Publication_requires_the_callers_existing_effect_transaction() {
        await using var app = await TestApplication.CreateAsync(new ProcessDecisionPublicationTestClock().Harness());
        await using var scope = app.Services.CreateAsyncScope();
        await using var context = Context(scope.ServiceProvider);
        var first = await RunningChildAsync(context);
        var original = await AdmissionAsync(scope.ServiceProvider, first);
        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<IProcessDecisionPublicationFence>()
            .RequireAsync(original, ProcessDecisionPublicationKind.Dispatch));
    }

    [Theory]
    [InlineData(IsolationLevel.RepeatableRead)]
    [InlineData(IsolationLevel.Serializable)]
    public async Task A_transaction_snapshot_cannot_authorize_a_generation_after_waiting_for_its_fence(IsolationLevel isolation) {
        await using var app = await TestApplication.CreateAsync(new ProcessDecisionPublicationTestClock().Harness());
        await using var scope = app.Services.CreateAsyncScope();
        await using var context = Context(scope.ServiceProvider);
        var first = await RunningChildAsync(context);
        var original = await AdmissionAsync(scope.ServiceProvider, first);
        await Assert.ThrowsAsync<NotSupportedException>(() => RequireAsync(scope.ServiceProvider, original, isolation: isolation));
    }

    [Fact]
    public async Task The_exact_lookup_is_one_selected_query_without_event_history_reads() {
        await using var app = await TestApplication.CreateAsync(new ProcessDecisionPublicationTestClock().Harness());
        await using var scope = app.Services.CreateAsyncScope();
        await using var context = Context(scope.ServiceProvider);
        var first = await RunningChildAsync(context);
        var probe = new ReadProbe();
        var query = new EfProcessDecisionChildQuery(new ContextFactory(Options(scope.ServiceProvider, probe)));
        var read = await query.ReadAsync(Address(first));
        Assert.NotNull(read);
        var sql = Assert.Single(probe.Commands);
        Assert.Contains("process_decision_children", sql);
        Assert.Contains("WHERE", sql);
        Assert.Contains("ChildId", sql);
        Assert.DoesNotContain("process_runtime_events", sql);
        Assert.DoesNotContain("process_strategy_result_receipts", sql);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task An_admitted_effect_holds_the_root_until_its_commit_or_rollback_while_an_unrelated_root_progresses(bool commit) {
        await using var app = await TestApplication.CreateAsync(new ProcessDecisionPublicationTestClock().Harness());
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await using var setup = Context(services);
        var first = await RunningChildAsync(setup);
        var other = await RunningChildAsync(setup);
        var original = await AdmissionAsync(services, first);
        await setup.Database.ExecuteSqlRawAsync("CREATE TABLE decision_fence_effects (\"RunId\" uuid PRIMARY KEY)");
        await using var effect = Context(services);
        await using var transaction = await effect.Database.BeginTransactionAsync();
        using var participation = services.GetRequiredService<CoordinatedDatabaseTransaction>().Enter(effect);
        await services.GetRequiredService<IProcessDecisionPublicationFence>().RequireAsync(original, ProcessDecisionPublicationKind.Dispatch);
        var probe = new FenceEntryProbe();
        await using var quiescence = Context(services, probe);
        var pending = QuiesceAsync(quiescence, first.State, ProcessDecisionPhaseChange.BeginReconciliation);
        try {
            await probe.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
            Assert.False(pending.IsCompleted);
            await using var unrelated = Context(services);
            await QuiesceAsync(unrelated, other.State, ProcessDecisionPhaseChange.BeginReconciliation).WaitAsync(TimeSpan.FromSeconds(15));
            await effect.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO decision_fence_effects (\"RunId\") VALUES ({first.State.RunId.Value})");
            if (commit) {
                await transaction.CommitAsync();
            } else {
                await transaction.RollbackAsync();
            }
            Assert.Equal(ProcessDecisionPhase.Reconciling, (await pending.WaitAsync(TimeSpan.FromSeconds(15))).Decision!.Current.Phase);
        } finally {
            if (!pending.IsCompleted) {
                await transaction.RollbackAsync();
                await pending.WaitAsync(TimeSpan.FromSeconds(15));
            }
        }
        Assert.Equal(commit ? 1 : 0, await setup.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM decision_fence_effects").SingleAsync());
    }

    [Fact]
    public async Task Quiescence_committing_first_refuses_the_waiting_effect_without_a_write() {
        await using var app = await TestApplication.CreateAsync(new ProcessDecisionPublicationTestClock().Harness());
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await using var setup = Context(services);
        var first = await RunningChildAsync(setup);
        var original = await AdmissionAsync(services, first);
        var pause = new CommitPause();
        await using var quiescence = Context(services, pause);
        var changing = QuiesceAsync(quiescence, first.State, ProcessDecisionPhaseChange.BeginReconciliation);
        await pause.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
        var probe = new FenceEntryProbe();
        var effect = AttemptPublicationAsync();
        try {
            await probe.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
            Assert.False(effect.IsCompleted);
        } finally {
            pause.Release.TrySetResult();
        }
        await changing.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.IsType<ProcessExecutionAuthorityMismatchException>(await effect.WaitAsync(TimeSpan.FromSeconds(15)));

        async Task<Exception> AttemptPublicationAsync() {
            await using var context = Context(services, probe);
            await using var transaction = await context.Database.BeginTransactionAsync();
            using var participation = services.GetRequiredService<CoordinatedDatabaseTransaction>().Enter(context);
            var fence = new EfProcessDecisionPublicationFence(Options(services, probe), services.GetRequiredService<CoordinatedDatabaseTransaction>(), services.GetRequiredService<TimeProvider>());
            return await Assert.ThrowsAsync<ProcessExecutionAuthorityMismatchException>(() => fence.RequireAsync(original, ProcessDecisionPublicationKind.Dispatch));
        }
    }

    private static async Task<ProcessRuntimeCommitResult> RunningChildAsync(ProcessPersistenceDbContext context) {
        var started = await StartAsync(context);
        var current = started.Decision!.Current;
        var request = await EnrollmentRequestAsync(context, started.State);
        var enrollment = (ProcessDecisionEnrollment)request.Decision!;
        request = request with { Decision = enrollment with { Child = enrollment.Child with {
            ChildId = ProcessDecisionChildIdentity.Create(current.Reference, ProcessDecisionChildOwner.Agent, 1)
        } } };
        var enrolled = await new EfProcessRuntimeUnitOfWork(context).CommitAsync(request);
        var running = await ClaimAsync(context, enrolled.State);
        return enrolled with { State = running.State };
    }

    private static Task<ProcessRuntimeCommitResult> ClaimAsync(ProcessPersistenceDbContext context, ProcessRuntimeStateSnapshot state) {
        var request = Request(state, null);
        var claim = new DispatchClaimState(new(Guid.NewGuid()), Step(state), new("decision-publication-fixture"), DispatchClaimStatus.Claimed,
            state.Steps.Single().AttemptNumber + 1, state.UpdatedAtUtc, state.UpdatedAtUtc.AddHours(1), null, null);
        var target = request.Mutation.State with { Status = ProcessRuntimeStatus.Active,
            Steps = [state.Steps.Single() with { Status = ProcessRuntimeStepStatus.Running, AttemptNumber = claim.AttemptNumber, ActiveClaimToken = claim.ClaimToken }],
            Claims = state.Claims.Select(item => item with { Status = DispatchClaimStatus.Released }).Append(claim).ToArray() };
        return new EfProcessRuntimeUnitOfWork(context).CommitAsync(request with { Mutation = request.Mutation with { State = target } });
    }

    private static async Task<ProcessRuntimeCommitResult> QuiesceAsync(ProcessPersistenceDbContext context, ProcessRuntimeStateSnapshot state, ProcessDecisionPhaseChange phase) {
        var current = await DecisionAsync(context, state);
        var request = Request(state, new ProcessDecisionChangePhase(Step(state), current.Expectation, phase));
        var target = request.Mutation.State with { Steps = [request.Mutation.State.Steps.Single() with { ActiveClaimToken = null }],
            Claims = state.Claims.Select(item => item with { Status = DispatchClaimStatus.Released }).ToArray() };
        return await new EfProcessRuntimeUnitOfWork(context).CommitAsync(request with { Mutation = request.Mutation with { State = target } });
    }

    private static ProcessDecisionChildAddress Address(ProcessRuntimeCommitResult enrolled) => new(enrolled.State.RootRunId, enrolled.State.RunId,
        Step(enrolled.State), enrolled.Decision!.Current.Reference, enrolled.Decision.Child!.ChildId);

    private static async Task<ProcessDecisionChildAdmission> AdmissionAsync(IServiceProvider services, ProcessRuntimeCommitResult enrolled)
        => (await services.GetRequiredService<EfProcessDecisionChildQuery>().ReadAsync(Address(enrolled)))!.Admission;

    private static async Task RequireAsync(IServiceProvider services, ProcessDecisionChildAdmission original,
        ProcessDecisionPublicationKind kind = ProcessDecisionPublicationKind.Dispatch, IsolationLevel isolation = IsolationLevel.ReadCommitted) {
        await using var context = Context(services);
        await using var transaction = await context.Database.BeginTransactionAsync(isolation);
        using var participation = services.GetRequiredService<CoordinatedDatabaseTransaction>().Enter(context);
        await services.GetRequiredService<IProcessDecisionPublicationFence>().RequireAsync(original, kind);
        await transaction.CommitAsync();
    }

    private sealed class ReadProbe : DbCommandInterceptor {
        public List<string> Commands { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            Commands.Add(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }

    private sealed class ContextFactory(DbContextOptions<ProcessPersistenceDbContext> options) : IDbContextFactory<ProcessPersistenceDbContext> {
        public ProcessPersistenceDbContext CreateDbContext() => new(options);
    }

    private sealed class FenceEntryProbe : DbCommandInterceptor {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            if (command.CommandText.Contains("pg_advisory_xact_lock", StringComparison.Ordinal)) {
                Entered.TrySetResult();
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class CommitPause : DbTransactionInterceptor {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData,
            InterceptionResult result, CancellationToken cancellationToken = default) {
            Entered.TrySetResult();
            await Release.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            return result;
        }
    }
}
