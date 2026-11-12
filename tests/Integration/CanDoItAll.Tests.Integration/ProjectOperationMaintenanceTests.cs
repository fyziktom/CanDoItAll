using System.Data.Common;
using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Fixture = CanDoItAll.Tests.Integration.ProjectOperationEpochTests.Fixture;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectOperationMaintenanceTests {
    private static readonly ProjectMutationCallerScope Caller = ProjectMutationCallerScope.LocalWorkspace;

    [Fact]
    public async Task Open_epoch_retirement_is_bounded_and_does_not_wait_for_a_live_writer_or_delete_retained_bytes() {
        await using var fixture = await Fixture.CreateAsync();
        var now = await fixture.NowAsync();
        await fixture.SeedHistoryAsync(Enumerable.Range(1, 129).Select(epoch => Closed(fixture, epoch, now)).ToArray(), 0);
        await fixture.SetDeadlineAsync(TimeSpan.FromHours(1));
        await fixture.SeedReceiptsAsync(1, 1, 3);
        await using var owner = fixture.Context();
        await using var transaction = await owner.Database.BeginTransactionAsync();
        await owner.Database.ExecuteSqlInterpolatedAsync($"""
            SELECT "Epoch" FROM "Infrastructure_FiniteOperationEpochs" WHERE "NamespaceId" = {fixture.Binding.Namespace.Value}
                AND "Epoch" = 130 FOR SHARE
            """);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var result = await fixture.Service.AdvanceRetirementAsync(fixture.Binding, fixture.Pool, deadline.Token);

        Assert.Equal(FiniteOperationRetirementStatus.Advanced, result.Status);
        Assert.Equal(128, result.RetiredThroughEpoch);
        await using var read = fixture.Context();
        var receipt = await read.Set<ProjectDefinitionReceiptRecord>().SingleAsync(deadline.Token);
        var operation = new ProjectDefinitionOperation(fixture.Binding.Namespace, new(1), receipt.OperationId, Caller.Bind(fixture.Binding.Namespace));
        await using var receiptRead = fixture.CreateDbContext();
        var observed = await ProjectDefinitionOperationStore.ObserveAsync(receiptRead, operation, Caller, deadline.Token);
        Assert.NotNull(observed);
        Assert.True(operation.Epoch.Value <= observed.RetiredThroughEpoch);
        Assert.Equal(receipt.AcceptedPayload, observed.AcceptedPayload);
        await using var environment = CanDoItAllTestEnvironment.Create("project-operation-retirement-reader");
        var reader = fixture.CreateReceiptReader(DatabaseTransferTestSupport.CreateActivationService(environment));
        var expired = await reader.ObserveAsync(fixture.Binding, operation,
            new ProjectDefinitionReceiptInvocation(Caller, new UnexpectedReceiptAccess()), deadline.Token);
        Assert.IsType<ProjectDefinitionLookup.Expired>(expired.Lookup);
        Assert.Equal(FiniteOperationEpochState.Open, (await read.Set<FiniteOperationEpochRecord>()
            .SingleAsync(epoch => epoch.Epoch == 130, deadline.Token)).State);
        await using var rejectedOwner = fixture.Context();
        await using var rejectedTransaction = await rejectedOwner.Database.BeginTransactionAsync(deadline.Token);
        await Assert.ThrowsAsync<FiniteOperationAdmissionException>(() => fixture.Service.EnterAsync(rejectedOwner,
            fixture.Binding, fixture.Pool, operation.Epoch.Value, deadline.Token));
    }

    [Fact]
    public async Task A_locked_state_returns_busy_without_changing_evidence_and_advances_after_release() {
        await using var fixture = await RetainedFixtureAsync();
        await using var owner = fixture.Context();
        await using var transaction = await owner.Database.BeginTransactionAsync();
        await owner.Database.ExecuteSqlInterpolatedAsync($"""
            SELECT "CurrentEpoch" FROM "Infrastructure_FiniteOperationState" WHERE "NamespaceId" = {fixture.Binding.Namespace.Value} FOR UPDATE
            """);

        var busy = await fixture.Service.AdvanceRetirementAsync(fixture.Binding, fixture.Pool, CancellationToken.None);

        Assert.Equal(FiniteOperationRetirementStatus.Busy, busy.Status);
        Assert.Null(busy.RetiredThroughEpoch);
        Assert.Equal(0, await fixture.RetiredThroughAsync());
        await transaction.RollbackAsync();
        Assert.Equal(1, (await fixture.Service.AdvanceRetirementAsync(fixture.Binding, fixture.Pool, CancellationToken.None)).RetiredThroughEpoch);
        await using var read = fixture.Context();
        Assert.Single(await read.Set<ProjectDefinitionReceiptRecord>().ToArrayAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Rotation_and_retirement_share_state_before_epoch_order_and_never_overwrite_a_higher_watermark(bool retirementFirst) {
        await using var fixture = await RetainedFixtureAsync();
        await fixture.SetDeadlineAsync(TimeSpan.FromSeconds(-1));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var gate = new CommandGate(IsStateLock, after: true, async token => {
            entered.SetResult();
            await release.Task.WaitAsync(token);
        });
        var firstService = fixture.CreateService(interceptor: gate);
        Task first = retirementFirst ? firstService.AdvanceRetirementAsync(fixture.Binding, fixture.Pool, deadline.Token)
            : firstService.RotateExpiredAsync(fixture.Binding, fixture.Pool, deadline.Token);
        try {
            await entered.Task.WaitAsync(deadline.Token);
            if (retirementFirst) {
                Assert.Equal(FiniteOperationRotationResult.Busy, await fixture.Service.RotateExpiredAsync(fixture.Binding, fixture.Pool, deadline.Token));
            } else {
                Assert.Equal(FiniteOperationRetirementStatus.Busy,
                    (await fixture.Service.AdvanceRetirementAsync(fixture.Binding, fixture.Pool, deadline.Token)).Status);
            }
        } finally {
            release.TrySetResult();
            await first;
        }
        await fixture.Service.AdvanceRetirementAsync(fixture.Binding, fixture.Pool, deadline.Token);
        await fixture.Service.RotateExpiredAsync(fixture.Binding, fixture.Pool, deadline.Token);
        await using var read = fixture.Context();
        var state = await read.Set<FiniteOperationStateRecord>().SingleAsync(deadline.Token);
        Assert.Equal(1, state.RetiredThroughEpoch);
        Assert.Equal(3, state.CurrentEpoch);
        Assert.Single(await read.Set<FiniteOperationEpochRecord>().Where(epoch => epoch.State == FiniteOperationEpochState.Open)
            .ToArrayAsync(deadline.Token));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Binding_switch_after_precheck_fences_retirement_and_purge_statements(bool purge) {
        await using var fixture = await RetainedFixtureAsync();
        if (purge) {
            await fixture.Service.AdvanceRetirementAsync(fixture.Binding, fixture.Pool, CancellationToken.None);
        }
        var replacement = new DatasetActivationBinding(new DatasetNamespaceId(Guid.NewGuid()), Guid.NewGuid(),
            DatasetLifecycleRecord.CurrentSchemaVersion, DatasetGuardProtocol.SharedRows);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gate = new CommandGate(purge ? IsReceiptDelete : IsRetirementUpdate, after: false,
            async token => {
                entered.TrySetResult();
                await release.Task.WaitAsync(token);
            });
        var service = fixture.CreateService(interceptor: gate);
        var reactivation = ReactivateAfterPrecheckAsync();
        try {
            await Assert.ThrowsAsync<DatasetActivationMismatchException>(async () => {
                if (purge) {
                    await fixture.PurgeAsync(cancellationToken: deadline.Token, interceptor: gate);
                } else {
                    await service.AdvanceRetirementAsync(fixture.Binding, fixture.Pool, deadline.Token);
                }
            });
        } finally {
            entered.TrySetCanceled(deadline.Token);
            release.TrySetResult();
            await reactivation;
        }

        Assert.True(gate.Triggered);
        Assert.Equal(replacement, (await fixture.LifecycleStore.ReadAsync())!.Binding);
        await using var read = fixture.Context();
        Assert.Single(await read.Set<ProjectDefinitionReceiptRecord>().ToArrayAsync());
        var current = await read.Set<FiniteOperationStateRecord>().SingleAsync(state => state.NamespaceId == replacement.Namespace.Value);
        Assert.Equal(1, current.CurrentEpoch);
        Assert.Equal(0, current.RetiredThroughEpoch);
        Assert.Equal(purge ? 1 : 0, (await read.Set<FiniteOperationStateRecord>()
            .SingleAsync(state => state.NamespaceId == fixture.Binding.Namespace.Value)).RetiredThroughEpoch);

        async Task ReactivateAfterPrecheckAsync() {
            await entered.Task.WaitAsync(deadline.Token);
            try {
                await fixture.ReactivateAsync(replacement, commit: true, deadline.Token);
            } finally {
                release.TrySetResult();
            }
        }
    }

    [Fact]
    public async Task Failure_after_receipt_delete_rolls_back_the_batch_but_preserves_prior_retirement_commit() {
        await using var fixture = await RetainedFixtureAsync();
        await fixture.Service.AdvanceRetirementAsync(fixture.Binding, fixture.Pool, CancellationToken.None);
        var gate = new CommandGate(sql => sql.Contains("LEFT JOIN LATERAL", StringComparison.Ordinal), after: false,
            _ => throw new InvalidOperationException("Synthetic post-delete observation failure"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.PurgeAsync(interceptor: gate));

        Assert.True(gate.Triggered);
        Assert.Equal(1, await fixture.RetiredThroughAsync());
        await using var read = fixture.Context();
        Assert.Single(await read.Set<ProjectDefinitionReceiptRecord>().ToArrayAsync());
        Assert.True(await read.Set<FiniteOperationEpochRecord>().AnyAsync(epoch => epoch.Epoch == 1));
    }

    [Fact]
    public async Task Two_purge_workers_report_locked_eligible_rows_as_contention_then_delete_once_after_release() {
        await using var fixture = await RetainedFixtureAsync();
        await fixture.Service.AdvanceRetirementAsync(fixture.Binding, fixture.Pool, CancellationToken.None);
        await using var owner = fixture.Context();
        await using var transaction = await owner.Database.BeginTransactionAsync();
        await owner.Database.ExecuteSqlInterpolatedAsync($"""
            SELECT "OperationId" FROM "Projects_DefinitionReceipts" WHERE "NamespaceId" = {fixture.Binding.Namespace.Value} FOR UPDATE
            """);
        var first = fixture.CreateCycle();
        var second = fixture.CreateCycle();
        var results = await Task.WhenAll(first.RunAsync(fixture.Service, fixture.CreateMaintenance(), fixture.Binding, CancellationToken.None),
            second.RunAsync(fixture.Service, fixture.CreateMaintenance(), fixture.Binding, CancellationToken.None));
        Assert.All(results, result => {
            Assert.Equal(0, result.DeletedReceipts);
            Assert.Equal(FiniteOperationEligibleWork.Present, result.EligibleWork);
            Assert.Equal(FiniteOperationMaintenanceStop.Contention, result.Stop);
            Assert.NotNull(result.EligibleAtUtc);
        });
        await using (var read = fixture.Context()) {
            Assert.True(await read.Set<FiniteOperationEpochRecord>().AnyAsync(epoch => epoch.Epoch == 1));
        }
        await transaction.RollbackAsync();

        var resumed = await Task.WhenAll(fixture.PurgeAsync(fixture.Binding),
            fixture.PurgeAsync(fixture.Binding));

        Assert.Equal(1, resumed.Sum(result => result.DeletedReceipts));
        Assert.Equal(3, resumed.Sum(result => result.DeletedBytes));
        await using var after = fixture.Context();
        Assert.Empty(await after.Set<ProjectDefinitionReceiptRecord>().ToArrayAsync());
    }

    [Fact]
    public async Task Shared_budget_cancellation_after_a_confirmed_batch_keeps_counts_and_starts_no_next_batch() {
        await using var fixture = await RetainedFixtureAsync(receipts: 129);
        using var budget = new CancellationTokenSource();
        var cancellation = new CancelAfterPurgeDisposal(budget);
        var cycle = fixture.CreateCycle();

        var result = await cycle.RunAsync(fixture.CreateService(interceptor: cancellation), fixture.CreateMaintenance(cancellation),
            fixture.Binding, budget.Token);

        Assert.Equal(FiniteOperationMaintenanceStop.Budget, result.Stop);
        Assert.Equal(1, result.CompletedBatches);
        Assert.Equal(128, result.DeletedReceipts);
        Assert.Equal(384, result.DeletedBytes);
        Assert.Equal(3, cancellation.Disposals);
        Assert.Equal(FiniteOperationEligibleWork.Unknown, result.EligibleWork);
        var definitions = Assert.Single(result.Families, family => family.Family == ProjectFiniteOperationPool.DefinitionFamily);
        Assert.Equal(FiniteOperationEligibleWork.Present, definitions.EligibleWork);
        var lifecycle = Assert.Single(result.Families, family => family.Family == ProjectFiniteOperationPool.LifecycleFamily);
        Assert.Equal(0, lifecycle.CompletedBatches);
        Assert.Equal(FiniteOperationEligibleWork.Unknown, lifecycle.EligibleWork);
        await using var read = fixture.Context();
        Assert.Single(await read.Set<ProjectDefinitionReceiptRecord>().ToArrayAsync());
    }

    [Fact]
    public async Task Query_failure_preserves_confirmed_counts_and_logs_only_sanitized_failure_metadata() {
        await using var fixture = await RetainedFixtureAsync(receipts: 129);
        var failure = new FailSecondDelete();
        var cycle = fixture.CreateCycle();

        var result = await cycle.RunAsync(fixture.Service, fixture.CreateMaintenance(failure), fixture.Binding, CancellationToken.None);

        Assert.Equal(FiniteOperationMaintenanceStop.Failed, result.Stop);
        Assert.Equal(2, result.CompletedBatches);
        Assert.Equal(1, Assert.Single(result.Families, family => family.Family == ProjectFiniteOperationPool.DefinitionFamily).CompletedBatches);
        Assert.Equal(FiniteOperationEligibleWork.NoneObserved,
            Assert.Single(result.Families, family => family.Family == ProjectFiniteOperationPool.LifecycleFamily).EligibleWork);
        Assert.Equal(128, result.DeletedReceipts);
        Assert.Equal(384, result.DeletedBytes);
        Assert.Equal(nameof(InvalidOperationException), result.Failure!.Type);
        var logger = new RecordingLogger();
        FiniteOperationMaintenance.Report(logger, Guid.NewGuid(), fixture.Pool.Pool, result, null);
        var warning = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, warning.Level);
        Assert.Null(warning.Exception);
        Assert.DoesNotContain(FailSecondDelete.PrivateDiagnostic, warning.Message, StringComparison.Ordinal);
        await using (var read = fixture.Context()) {
            Assert.Single(await read.Set<ProjectDefinitionReceiptRecord>().ToArrayAsync());
        }
        var cleanupResult = await fixture.CreateCycle().RunAsync(fixture.Service,
            fixture.CreateMaintenance(new FailConnectionDispose()), fixture.Binding, CancellationToken.None);
        Assert.Equal(1, cleanupResult.DeletedReceipts);
        Assert.Equal(3, cleanupResult.DeletedBytes);
        Assert.Equal(129, result.DeletedReceipts + cleanupResult.DeletedReceipts);
        Assert.Equal(387, result.DeletedBytes + cleanupResult.DeletedBytes);
        Assert.Equal(FiniteOperationMaintenanceStop.Failed, cleanupResult.Stop);
        await using var afterCleanup = fixture.Context();
        Assert.Empty(await afterCleanup.Set<ProjectDefinitionReceiptRecord>().ToArrayAsync());
    }

    private static async Task<Fixture> RetainedFixtureAsync(int receipts = 1) {
        var fixture = await Fixture.CreateAsync();
        try {
            await fixture.SeedHistoryAsync([Closed(fixture, 1, await fixture.NowAsync())], 0);
            await fixture.SetDeadlineAsync(TimeSpan.FromHours(1));
            await fixture.SeedReceiptsAsync(1, receipts, 3);
            return fixture;
        } catch {
            await fixture.DisposeAsync();
            throw;
        }
    }

    private static FiniteOperationEpochRecord Closed(Fixture fixture, long epoch, DateTimeOffset now) => new() {
        NamespaceId = fixture.Binding.Namespace.Value,
        PoolId = fixture.Pool.Pool.Value,
        Epoch = epoch,
        State = FiniteOperationEpochState.Closed,
        OpenedAtUtc = now.AddDays(-4),
        AdmissionClosesAtUtc = now.AddDays(-3),
        ClosedAtUtc = now.AddDays(-3).AddSeconds(1),
        MutationDeadlineMilliseconds = 1000,
        ReplayRetentionSeconds = 60
    };

    private static bool IsStateLock(string sql) => sql.Contains("Infrastructure_FiniteOperationState", StringComparison.Ordinal)
        && sql.Contains("FOR UPDATE SKIP LOCKED", StringComparison.Ordinal);

    private static bool IsRetirementUpdate(string sql) => sql.Contains("UPDATE \"Infrastructure_FiniteOperationState\" AS s", StringComparison.Ordinal);

    private static bool IsReceiptDelete(string sql) => sql.Contains("WITH cleanup_epoch AS", StringComparison.Ordinal);

    private sealed class CommandGate(Func<string, bool> matches, bool after, Func<CancellationToken, Task> action) : DbCommandInterceptor {
        private int triggered;
        public bool Triggered => triggered != 0;

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            if (!after) {
                await VisitAsync(command, cancellationToken);
            }
            return result;
        }

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default) {
            if (after) {
                await VisitAsync(command, cancellationToken);
            }
            return result;
        }

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            if (!after) {
                await VisitAsync(command, cancellationToken);
            }
            return result;
        }

        private Task VisitAsync(DbCommand command, CancellationToken token)
            => matches(command.CommandText) && Interlocked.Exchange(ref triggered, 1) == 0 ? action(token) : Task.CompletedTask;
    }

    private sealed class CancelAfterPurgeDisposal(CancellationTokenSource cancellation) : DbConnectionInterceptor {
        public int Disposals { get; private set; }

        public override Task ConnectionDisposedAsync(DbConnection connection, ConnectionEndEventData eventData) {
            Disposals++;
            if (Disposals == 3) {
                cancellation.Cancel();
            }
            return Task.CompletedTask;
        }
    }

    private sealed class FailSecondDelete : DbCommandInterceptor {
        public const string PrivateDiagnostic = "SyntheticSecret=must-never-appear-in-maintenance-log";
        private int deletes;

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            if (IsReceiptDelete(command.CommandText) &&
                    command.CommandText.Contains(ProjectDefinitionReceiptRecord.TableName, StringComparison.Ordinal) && ++deletes == 2) {
                throw new InvalidOperationException(PrivateDiagnostic);
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class FailConnectionDispose : DbConnectionInterceptor {
        public override Task ConnectionDisposedAsync(DbConnection connection, ConnectionEndEventData eventData)
            => throw new InvalidOperationException("Synthetic connection cleanup failure after acknowledged purge.");
    }

    private sealed class RecordingLogger : ILogger {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Entries.Add((logLevel, formatter(state, exception), exception));
    }

    private sealed class UnexpectedReceiptAccess : IProjectDefinitionReceiptAccess {
        public Task<bool> CanReadAsync(ProjectWriteAdmission target, CancellationToken cancellationToken)
            => throw new InvalidOperationException("An expired address must not read or authorize its retained payload.");
    }
}
