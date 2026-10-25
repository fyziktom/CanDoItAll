using System.Data.Common;
using System.Text.Json;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;
using static CanDoItAll.Tests.Integration.ProcessDecisionRefreshJournalFixture;

namespace CanDoItAll.Tests.Integration;

[Trait("Category", "FileSystemPortability")]
public sealed class ProcessDecisionRefreshCaptureJournalPersistenceTests {
    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public async Task Reserved_work_is_claimed_once_after_interruption_before_begin(ReadBasisMode mode) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(mode, freezeClock: false);
        var (admission, state) = await ProcessDecisionRefreshJournalFixture.StartAsync(fixture);
        CaptureProbe capturer = new(fixture.Services);
        InterruptedStore interrupted = new(fixture.Services.GetRequiredService<IProcessDecisionRefreshCaptureStore>(), Interruption.BeforeBegin);
        await Assert.ThrowsAsync<IOException>(() => Coordinator(fixture.Services, capturer, interrupted).PrepareAsync(state, fixture.CommandContext, admission));
        var saved = await SavedAsync(fixture.Services, state, admission, mode);
        Assert.Equal(ProcessDecisionRefreshCapturePhase.Reserved, saved.Phase);
        Assert.Null(saved.Invocation);
        Assert.Equal(0, capturer.Calls);
        var result = await Coordinator(fixture.Services, capturer).PrepareAsync(state, fixture.CommandContext, admission);
        Assert.Equal(ProcessDecisionRefreshCaptureStatus.Advanced, result.Status);
        Assert.Equal(1, capturer.Calls);
        saved = await SavedAsync(fixture.Services, state, admission, mode);
        Assert.Equal(ProcessDecisionRefreshCapturePhase.Settled, saved.Phase);
        Assert.Equal(ProcessDecisionRefreshCaptureDisposition.Advanced, saved.Disposition);
        Assert.NotNull(saved.Invocation);
        await using var context = Context(fixture.Services);
        Assert.Equal(0, await context.RefreshCapturePayloads.CountAsync());
        Assert.Equal(mode == ReadBasisMode.RunSnapshot ? 2 : 1, await context.RefreshCaptures.CountAsync());
    }

    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public async Task Lost_begin_acknowledgement_keeps_unknown_work_and_refuses_refund_or_reinvocation(ReadBasisMode mode) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(mode, freezeClock: false);
        var (admission, state) = await ProcessDecisionRefreshJournalFixture.StartAsync(fixture);
        var store = fixture.Services.GetRequiredService<IProcessDecisionRefreshCaptureStore>();
        CaptureProbe capturer = new(fixture.Services);
        await Assert.ThrowsAsync<IOException>(() => Coordinator(fixture.Services, capturer, new InterruptedStore(store, Interruption.AfterBegin))
            .PrepareAsync(state, fixture.CommandContext, admission));
        var saved = await SavedAsync(fixture.Services, state, admission, mode);
        Assert.Equal(ProcessDecisionRefreshCapturePhase.Invoking, saved.Phase);
        Assert.NotNull(saved.Invocation);
        Assert.Null(saved.Outcome);
        var retained = await SnapshotAsync(fixture.Services, state);
        Assert.Equal(ProcessDecisionRefreshCaptureStatus.Pending, (await Coordinator(fixture.Services, capturer)
            .PrepareAsync(state, fixture.CommandContext, admission)).Status);
        Assert.Equal(retained, await SnapshotAsync(fixture.Services, state));
        Assert.Equal(0, capturer.Calls);
        Assert.Equal(ProcessDecisionRefreshCaptureRefusal.InvalidPhase, (await Assert.ThrowsAsync<ProcessDecisionRefreshCaptureException>(() =>
            store.CloseNeverInvokedAsync(saved.Reservation))).Reason);
        var competing = await store.BeginAsync(saved.Reservation, new(Guid.NewGuid()));
        Assert.Equal(ProcessDecisionRefreshCaptureInvocationStatus.PreviouslyAdmitted, competing.Status);
        Assert.Equal(saved, competing.State);
        await using var context = Context(fixture.Services);
        var allocation = await context.DecisionBudgetAllocations.AsNoTracking().SingleAsync(row =>
            row.BudgetId == saved.Reservation.Budget.Expectation.BudgetId.Value && row.AllocationId == saved.Reservation.Budget.AllocationId.Value);
        Assert.Null(allocation.OwnerWorkRevision);
        Assert.Equal(saved.Reservation.Budget.Grant.WorkTicks, allocation.GrantedWorkTicks);
    }

    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public async Task Returned_capture_survives_host_restart_and_settles_without_recapture(ReadBasisMode mode) {
        await using var environment = CanDoItAllTestEnvironment.Create("process-refresh-journal-restart");
        var profile = environment.CreatePostgreSqlProfile("refresh-journal-restart");
        RuntimeCommandId admission;
        RuntimeCommandContext command;
        ProcessRuntimeStateSnapshot state;
        ProcessDecisionRefreshCaptureState returned;
        await using (var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(mode, environment, profile, freezeClock: false)) {
            (admission, state) = await ProcessDecisionRefreshJournalFixture.StartAsync(fixture);
            command = fixture.CommandContext;
            CaptureProbe capturer = new(fixture.Services);
            InterruptedStore interrupted = new(fixture.Services.GetRequiredService<IProcessDecisionRefreshCaptureStore>(), Interruption.AfterReturn);
            await Assert.ThrowsAsync<IOException>(() => Coordinator(fixture.Services, capturer, interrupted).PrepareAsync(state, command, admission));
            Assert.Equal(1, capturer.Calls);
            returned = await SavedAsync(fixture.Services, state, admission, mode);
            Assert.Equal(ProcessDecisionRefreshCapturePhase.Returned, returned.Phase);
            Assert.Equal(ProcessDecisionRefreshCaptureOutcomeKind.Captured, returned.Outcome!.Kind);
            Assert.True(returned.Outcome.Work.Consumed.WorkTicks > 0);
            await using var context = Context(fixture.Services);
            Assert.Equal(1, await context.RefreshCapturePayloads.CountAsync());
        }
        await using var restarted = await TestApplication.CreateAsync(new TestHarnessOptions {
            TestEnvironment = environment, ActiveProfile = profile, ConfigureServices = services => {
                foreach (var descriptor in services.Where(item => item.ImplementationType?.Name == "ProcessLaunchContinuationWorker").ToArray()) {
                    services.Remove(descriptor);
                }
            }
        });
        await using var scope = restarted.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        CaptureProbe forbidden = new(services, (_, _) => throw new InvalidOperationException("Completed capture was invoked again."));
        Assert.Equal(ProcessDecisionRefreshCaptureStatus.Advanced, (await Coordinator(services, forbidden).PrepareAsync(state, command, admission)).Status);
        Assert.Equal(0, forbidden.Calls);
        var settled = await SavedAsync(services, state, admission, mode);
        Assert.Equal(returned.Outcome, settled.Outcome);
        Assert.Equal(returned.Invocation, settled.Invocation);
        Assert.Equal(ProcessDecisionRefreshCaptureDisposition.Advanced, settled.Disposition);
        await using var persisted = Context(services);
        Assert.Equal(0, await persisted.RefreshCapturePayloads.CountAsync());
        Assert.Equal(2, await persisted.DecisionMaterials.CountAsync());
        var retained = await SnapshotAsync(services, state);
        Assert.Equal(ProcessDecisionRefreshCaptureStatus.Advanced, (await Coordinator(services, forbidden).PrepareAsync(state, command, admission)).Status);
        Assert.Equal(retained, await SnapshotAsync(services, state));
    }

    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public async Task Cancellation_before_invocation_closes_never_invoked_work_without_capture(ReadBasisMode mode) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(mode, freezeClock: false);
        var (admission, state) = await ProcessDecisionRefreshJournalFixture.StartAsync(fixture);
        var store = fixture.Services.GetRequiredService<IProcessDecisionRefreshCaptureStore>();
        CaptureProbe capturer = new(fixture.Services);
        InterruptedStore interrupted = new(store, Interruption.BeforeBegin, () => CancelAsync(fixture.Services, state, fixture.CommandContext));
        await Assert.ThrowsAsync<IOException>(() => Coordinator(fixture.Services, capturer, interrupted).PrepareAsync(state, fixture.CommandContext, admission));
        var result = await Coordinator(fixture.Services, capturer).PrepareAsync(state, fixture.CommandContext, admission);
        Assert.Equal(ProcessDecisionRefreshCaptureStatus.Stopped, result.Status);
        Assert.Equal(0, capturer.Calls);
        var saved = await SavedAsync(fixture.Services, state, admission, mode);
        Assert.Equal(ProcessDecisionRefreshCapturePhase.Settled, saved.Phase);
        Assert.Equal(ProcessDecisionRefreshCaptureOutcomeKind.NeverInvoked, saved.Outcome!.Kind);
        Assert.Equal(0, saved.Outcome.Work.Consumed.WorkTicks);
        Assert.Equal(ProcessDecisionRefreshCaptureDisposition.Aborted, saved.Disposition);
        Assert.Null(saved.Invocation);
        Assert.Equal(ProcessDecisionRefreshCaptureInvocationStatus.PreviouslyAdmitted, (await store.BeginAsync(saved.Reservation, new(Guid.NewGuid()))).Status);
    }

    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public async Task Cancellation_after_begin_waits_for_the_original_return_then_drains_exact_work(ReadBasisMode mode) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(mode);
        var (admission, state) = await ProcessDecisionRefreshJournalFixture.StartAsync(fixture);
        var store = fixture.Services.GetRequiredService<IProcessDecisionRefreshCaptureStore>();
        CaptureProbe capturer = new(fixture.Services, async (_, _) => {
            var invoking = await SavedAsync(fixture.Services, state, admission, mode);
            Assert.Equal(ProcessDecisionRefreshCapturePhase.Invoking, invoking.Phase);
            await CancelAsync(fixture.Services, state, fixture.CommandContext);
            await Assert.ThrowsAsync<ProcessDecisionRefreshCaptureException>(() => store.CloseNeverInvokedAsync(invoking.Reservation));
            fixture.Clock.Advance(TimeSpan.FromSeconds(2));
            return new ProcessDecisionMaterialCaptureResult.Refused(ProcessDecisionMaterialCaptureRefusal.SourceChanged);
        });
        Assert.Equal(ProcessDecisionRefreshCaptureStatus.Stopped, (await Coordinator(fixture.Services, capturer)
            .PrepareAsync(state, fixture.CommandContext, admission)).Status);
        var settled = await SavedAsync(fixture.Services, state, admission, mode);
        Assert.Equal(ProcessDecisionRefreshCapturePhase.Settled, settled.Phase);
        Assert.Equal(ProcessDecisionRefreshCaptureOutcomeKind.Refused, settled.Outcome!.Kind);
        Assert.Equal(TimeSpan.FromSeconds(2).Ticks, settled.Outcome.Work.Consumed.WorkTicks);
        Assert.Equal(ProcessDecisionRefreshCaptureDisposition.Aborted, settled.Disposition);
        Assert.Equal(1, capturer.Calls);
        await using var context = Context(fixture.Services);
        Assert.Equal(TimeSpan.FromSeconds(2).Ticks, (await context.DecisionBudgetAllocations.AsNoTracking().SingleAsync(row =>
            row.BudgetId == settled.Reservation.Budget.Expectation.BudgetId.Value && row.AllocationId == settled.Reservation.Budget.AllocationId.Value)).ConsumedWorkTicks);
    }

    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public async Task Metadata_writes_share_the_root_fence_and_leave_runtime_history_and_receipts_unchanged(ReadBasisMode mode) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(mode, freezeClock: false);
        var (admission, state) = await ProcessDecisionRefreshJournalFixture.StartAsync(fixture);
        var registered = fixture.Services.GetRequiredService<IProcessDecisionRefreshCaptureStore>();
        await Assert.ThrowsAsync<IOException>(() => Coordinator(fixture.Services, new(fixture.Services), new InterruptedStore(registered, Interruption.BeforeBegin))
            .PrepareAsync(state, fixture.CommandContext, admission));
        var saved = await SavedAsync(fixture.Services, state, admission, mode);
        SqlProbe probe = new();
        EfProcessDecisionRefreshCaptureStore store = new(new Factory(fixture.Services, probe), fixture.Services.GetRequiredService<ICanonicalRuntimeDatabase>());
        var before = JsonSerializer.Serialize(await fixture.Services.GetRequiredService<IProcessRuntimeStateStore>().LoadAsync(state.RunId));
        await using var context = Context(fixture.Services);
        var commands = await context.IdempotencyKeys.CountAsync();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var key = ProcessRootAdvisoryKey.From(state.RootRunId.Value);
        await context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})");
        ProcessDecisionCaptureInvocationId invocation = new(Guid.NewGuid());
        var pending = store.BeginAsync(saved.Reservation, invocation);
        try {
            await probe.FenceEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(pending.IsCompleted);
        } finally {
            await transaction.RollbackAsync();
        }
        Assert.Equal(ProcessDecisionRefreshCaptureInvocationStatus.Started, (await pending.WaitAsync(TimeSpan.FromSeconds(5))).Status);
        ProcessDecisionRefreshCaptureReturned returned = new(new(ProcessDecisionRefreshCaptureOutcomeKind.Refused, new(1, new(7, 0))));
        var recorded = await store.ReturnAsync(saved.Reservation, invocation, returned);
        Assert.Equal(recorded, await store.ReturnAsync(saved.Reservation, invocation, returned));
        Assert.Equal(ProcessDecisionRefreshCaptureRefusal.OriginalInvocationChanged, (await Assert.ThrowsAsync<ProcessDecisionRefreshCaptureException>(() =>
            store.ReturnAsync(saved.Reservation, new(Guid.NewGuid()), returned))).Reason);
        var changed = returned with { Outcome = returned.Outcome with { Work = new(1, new(8, 0)) } };
        Assert.Equal(ProcessDecisionRefreshCaptureRefusal.OriginalOutcomeChanged, (await Assert.ThrowsAsync<ProcessDecisionRefreshCaptureException>(() =>
            store.ReturnAsync(saved.Reservation, invocation, changed))).Reason);
        Assert.Equal(before, JsonSerializer.Serialize(await fixture.Services.GetRequiredService<IProcessRuntimeStateStore>().LoadAsync(state.RunId)));
        Assert.Equal(commands, await context.IdempotencyKeys.CountAsync());
        Assert.Equal("read committed|5s|2s|1s", probe.Settings);
        Assert.InRange(probe.Commands.Count, 1, 28);
        foreach (var sql in probe.Commands) {
            Assert.DoesNotContain("process_dispatch_claims", sql, StringComparison.Ordinal);
            Assert.DoesNotContain("process_strategy_result_receipts", sql, StringComparison.Ordinal);
            Assert.DoesNotContain("process_runtime_events", sql, StringComparison.Ordinal);
            Assert.DoesNotContain("process_decision_refresh_capture_payloads", sql, StringComparison.Ordinal);
            Assert.DoesNotContain("UPDATE process_runtime_states", sql, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public async Task Reservation_rolls_back_atomically_and_known_return_cannot_be_settled_without_its_journal(ReadBasisMode mode) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(mode, freezeClock: false);
        var (admission, state) = await ProcessDecisionRefreshJournalFixture.StartAsync(fixture);
        var before = await SnapshotAsync(fixture.Services, state);
        CaptureProbe capturer = new(fixture.Services);
        await using var context = Context(fixture.Services);
        await context.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION reject_refresh_capture_fixture() RETURNS trigger LANGUAGE plpgsql AS $body$
            BEGIN RAISE EXCEPTION 'Refresh capture fixture interrupted reservation'; END $body$;
            CREATE TRIGGER reject_refresh_capture_fixture BEFORE INSERT ON process_decision_refresh_captures
                FOR EACH ROW EXECUTE FUNCTION reject_refresh_capture_fixture();
            """);
        try {
            await Assert.ThrowsAsync<DbUpdateException>(() => Coordinator(fixture.Services, capturer).PrepareAsync(state, fixture.CommandContext, admission));
            Assert.Equal(before, await SnapshotAsync(fixture.Services, state));
            Assert.Equal(0, capturer.Calls);
        } finally {
            await context.Database.ExecuteSqlRawAsync("DROP TRIGGER reject_refresh_capture_fixture ON process_decision_refresh_captures; DROP FUNCTION reject_refresh_capture_fixture()");
        }
        InterruptedStore interrupted = new(fixture.Services.GetRequiredService<IProcessDecisionRefreshCaptureStore>(), Interruption.AfterReturn);
        await Assert.ThrowsAsync<IOException>(() => Coordinator(fixture.Services, capturer, interrupted).PrepareAsync(state, fixture.CommandContext, admission));
        var returned = await SavedAsync(fixture.Services, state, admission, mode);
        var current = (await fixture.Services.GetRequiredService<IProcessRuntimeStateStore>().LoadAsync(state.RunId))!;
        var balance = await context.DecisionBudgets.AsNoTracking().SingleAsync(row => row.BudgetId == returned.Reservation.Budget.Expectation.BudgetId.Value);
        ProcessRuntimeCommitRequest bypass = new(RuntimeCommandId.New(), current,
            ProcessRuntimeMutation.Applied(current with { UpdatedAtUtc = new(current.UpdatedAtUtc.UtcTicks + 10, TimeSpan.Zero) }, [], [])) {
            DecisionBudget = new ProcessDecisionBudgetSettlement(new(returned.Reservation.Budget.Expectation.StepInstanceId,
                new(balance.BudgetId), balance.Revision), returned.Reservation.Budget.AllocationId, returned.Outcome!.Work),
            DecisionRunScope = mode == ReadBasisMode.RunSnapshot
                ? new((await fixture.Services.GetRequiredService<IProcessDecisionRootQuery>().ReadAsync(current))!.Scope.Expectation,
                    ProcessDecisionRunScopeCommandKind.AbortCapture) : null
        };
        var retained = await SnapshotAsync(fixture.Services, state);
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Services.GetRequiredService<IProcessRuntimeUnitOfWork>().CommitAsync(bypass));
        Assert.Equal(retained, await SnapshotAsync(fixture.Services, state));
        Assert.Equal(ProcessDecisionRefreshCaptureStatus.Advanced, (await Coordinator(fixture.Services, capturer)
            .PrepareAsync(state, fixture.CommandContext, admission)).Status);
        Assert.Equal(1, capturer.Calls);
    }

    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public async Task Legacy_reservation_without_protocol_evidence_remains_unknown(ReadBasisMode mode) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(mode, freezeClock: false);
        var (admission, state) = await ProcessDecisionRefreshJournalFixture.StartAsync(fixture);
        CaptureProbe capturer = new(fixture.Services);
        LegacyReservation owner = new(fixture.Services.GetRequiredService<IProcessRuntimeUnitOfWork>());
        Assert.Equal(ProcessDecisionRefreshCaptureStatus.Pending, (await Coordinator(fixture.Services, capturer, owner: owner)
            .PrepareAsync(state, fixture.CommandContext, admission)).Status);
        Assert.Null(await SavedAsync(fixture.Services, state, admission, mode));
        var before = await SnapshotAsync(fixture.Services, state);
        Assert.Equal(ProcessDecisionRefreshCaptureStatus.Pending, (await Coordinator(fixture.Services, capturer)
            .PrepareAsync(state, fixture.CommandContext, admission)).Status);
        Assert.Equal(before, await SnapshotAsync(fixture.Services, state));
        Assert.Equal(0, capturer.Calls);
    }

    private sealed class LegacyReservation(IProcessRuntimeUnitOfWork inner) : IProcessRuntimeUnitOfWork {
        public async Task<ProcessRuntimeCommitResult> CommitAsync(ProcessRuntimeCommitRequest request, CancellationToken cancellationToken = default) {
            var result = await inner.CommitAsync(request with { RefreshCapture = null }, cancellationToken);
            if (request.RefreshCapture is not null) {
                throw new IOException("Legacy reservation acknowledgement lost.");
            }
            return result;
        }
    }

    private sealed class Factory(IServiceProvider services, SqlProbe probe) : IDbContextFactory<ProcessPersistenceDbContext> {
        public ProcessPersistenceDbContext CreateDbContext() => Context(services, probe);
    }

    private sealed class SqlProbe : DbCommandInterceptor {
        internal List<string> Commands { get; } = [];
        internal TaskCompletionSource FenceEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal string? Settings { get; private set; }
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            Commands.Add(command.CommandText);
            if (command.CommandText.Contains("pg_advisory_xact_lock", StringComparison.Ordinal)) {
                FenceEntered.TrySetResult();
            }
            return ValueTask.FromResult(result);
        }
        public override async ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            int result, CancellationToken cancellationToken = default) {
            await using var inspect = command.Connection!.CreateCommand();
            inspect.Transaction = command.Transaction;
            inspect.CommandText = "SELECT current_setting('transaction_isolation') || '|' || current_setting('transaction_timeout') || '|' || current_setting('statement_timeout') || '|' || current_setting('lock_timeout')";
            Settings = (string)(await inspect.ExecuteScalarAsync(cancellationToken))!;
            return result;
        }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            Commands.Add(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }
}
