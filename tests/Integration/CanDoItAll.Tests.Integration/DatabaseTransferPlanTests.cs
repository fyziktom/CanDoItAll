using System.Data.Common;
using CanDoItAll.AgentFramework.ProviderHistory.Persistence;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace CanDoItAll.Tests.Integration;

public sealed class DatabaseTransferPlanTests {
    private const long FirstGate = 724091824021;
    private const long SecondGate = 724091824022;

    [Fact]
    public async Task Missing_target_plan_rejects_before_opening_a_database_or_invoking_the_callback() {
        var source = new ResolvedDatabaseProfile(new() { ProviderKind = DatabaseProviderKind.InMemory },
            DatabaseProfileResolutionSource.ExplicitOverride, $"transfer-plan-source-{Guid.NewGuid():N}");
        var target = new ResolvedDatabaseProfile(new() { ProviderKind = DatabaseProviderKind.InMemory },
            DatabaseProfileResolutionSource.ExplicitOverride, $"transfer-plan-target-{Guid.NewGuid():N}");
        var invoked = false;
        await Assert.ThrowsAsync<ArgumentNullException>(() => DatabaseTransferTestSupport.Create().RunTransferAsync(
            new(source, target, true), null!, (_, _) => {
                invoked = true;
                return Task.FromResult(Result());
            }, allowInMemoryTest: true));
        Assert.False(invoked);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Admitted_plan_rejects_an_unplanned_owner_or_later_table_addition_before_owner_SQL(bool extendPlan) {
        await using var source = await HistoryPersistenceTestDatabase.CreateAsync();
        await using var target = await HistoryPersistenceTestDatabase.CreateAsync();
        var activation = await target.ActivateDatasetAsync();
        var probe = new GateProbe();
        await using var sourceContext = source.Factory.CreateDbContext();
        await using var targetContext = target.Factory.WithInterceptor(probe).CreateDbContext();
        var owners = new DatabaseTransferOwnerSessionRunner();
        var transfer = new DatabaseTransferOperation(source.Profile, target.Profile, true);
        var operations = DatabaseTransferTestSupport.For(transfer, sourceContext, targetContext, owners, activation: activation);
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => operations.RunTransferAsync(
            transfer, new([typeof(Project)]), async (request, token) => {
                if (extendPlan) {
                    var phase = DatabaseTransferTable.From(targetContext.Model.FindEntityType(typeof(ProjectPhase))!);
                    await owners.AcquireTargetTableLocksAsync(request, [phase], token);
                } else {
                    await using var owner = await owners.CreateTargetAsync<ProjectsDbContext>(request, static options => new(options), token);
                    await owner.Set<Project>().CountAsync(token);
                }
                return Result();
            }));
        Assert.Contains(extendPlan ? "cannot extend" : "outside the complete admitted", failure.Message);
        Assert.Equal(0, probe.ProjectOwnerCommands);
        await using var verify = target.Factory.CreateDbContext();
        Assert.False(await verify.Set<Project>().AnyAsync());
    }

    [Fact]
    public async Task Target_begin_failure_releases_deduplicated_entry_gates_on_the_original_connection() {
        await using var source = await HistoryPersistenceTestDatabase.CreateAsync();
        await using var target = await HistoryPersistenceTestDatabase.CreateAsync();
        var activation = await target.ActivateDatasetAsync();
        var failure = new BeginFailure();
        await using var sourceContext = source.Factory.CreateDbContext();
        await using var targetContext = target.Factory.WithInterceptor(failure).CreateDbContext();
        var transfer = new DatabaseTransferOperation(source.Profile, target.Profile, true);
        var operations = DatabaseTransferTestSupport.For(transfer, sourceContext, targetContext, activation: activation);
        var plan = new DatabaseTransferTargetPlan([typeof(Project)], [SecondGate, FirstGate, SecondGate]);
        Assert.Equal(new[] { FirstGate, SecondGate }, plan.SessionAdvisoryKeys);
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => operations.RunTransferAsync(
            transfer, plan, (_, _) => Task.FromResult(Result())));
        Assert.Same(failure.Exception, actual);
        await AssertGatesAvailableAsync(target, [FirstGate, SecondGate]);
    }

    [Fact]
    public async Task Cancellation_waiting_for_second_entry_gate_releases_the_acquired_prefix_without_beginning() {
        await using var source = await HistoryPersistenceTestDatabase.CreateAsync();
        await using var target = await HistoryPersistenceTestDatabase.CreateAsync();
        var activation = await target.ActivateDatasetAsync();
        await using var blocker = target.Factory.CreateDbContext();
        await using var transaction = await blocker.Database.BeginTransactionAsync();
        await blocker.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({SecondGate})");
        var probe = new GateProbe();
        await using var sourceContext = source.Factory.CreateDbContext();
        await using var targetContext = target.Factory.WithInterceptor(probe).CreateDbContext();
        var transfer = new DatabaseTransferOperation(source.Profile, target.Profile, true);
        var operations = DatabaseTransferTestSupport.For(transfer, sourceContext, targetContext, activation: activation);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
        var invoked = false;
        var run = operations.RunTransferAsync(transfer, new([typeof(Project)], [FirstGate, SecondGate]), (_, _) => {
            invoked = true;
            return Task.FromResult(Result());
        }, cancellationToken: cancel.Token);
        try {
            var processId = await probe.Requested.Task.WaitAsync(deadline.Token);
            await WaitForAdvisoryWaiterAsync(target, processId, deadline.Token);
            cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
            Assert.False(invoked);
            Assert.False(probe.SessionGateHadTransaction);
            await AssertGatesAvailableAsync(target, [FirstGate]);
        } finally {
            cancel.Cancel();
            await transaction.RollbackAsync();
        }
        await AssertGatesAvailableAsync(target, [FirstGate, SecondGate]);
    }

    [Fact]
    public async Task Existing_initializer_commits_before_transfer_snapshot_and_transfer_preserves_its_partition() {
        await using var source = await HistoryPersistenceTestDatabase.CreateAsync();
        await using var target = await HistoryPersistenceTestDatabase.CreateAsync();
        var activation = await target.ActivateDatasetAsync();
        await ClearEmptyHistoryAsync(target);
        var initializeProbe = new InitializationProbe(pauseAfterAcquisition: true);
        var factory = target.HistoryFactory.WithInterceptor(initializeProbe);
        var partitions = new HistoryPartitionStore(factory, factory.Options, target.Transactions);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var initialization = partitions.GetAsync(deadline.Token);
        await initializeProbe.Acquired.Task.WaitAsync(deadline.Token);
        var transferProbe = new GateProbe();
        await using var sourceContext = source.Factory.CreateDbContext();
        await using var targetContext = target.Factory.WithInterceptor(transferProbe).CreateDbContext();
        var owners = new DatabaseTransferOwnerSessionRunner();
        var transfer = new DatabaseTransferOperation(source.Profile, target.Profile, false);
        var operations = DatabaseTransferTestSupport.For(transfer, sourceContext, targetContext, owners, activation: activation);
        var run = new HistoryDatabaseTransferHandler([], owners, operations).TransferAsync(transfer, deadline.Token);
        try {
            var processId = await transferProbe.Requested.Task.WaitAsync(deadline.Token);
            await WaitForAdvisoryWaiterAsync(target, processId, deadline.Token);
            Assert.False(transferProbe.SessionGateHadTransaction);
            initializeProbe.Release.TrySetResult();
            var initialized = await initialization.WaitAsync(deadline.Token);
            var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => run);
            Assert.Contains("cannot merge or replace retained target history", failure.Message);
            Assert.Equal(initialized, await target.Partitions.GetAsync(deadline.Token));
            Assert.NotEqual(source.Partition, initialized);
        } finally {
            initializeProbe.Release.TrySetResult();
        }
        await AssertGatesAvailableAsync(target, [HistoryPartitionStore.InitializationLockKey]);
    }

    [Fact]
    public async Task Initializer_waiting_behind_transfer_rereads_and_returns_the_transferred_partition() {
        await using var source = await HistoryPersistenceTestDatabase.CreateAsync();
        await using var target = await HistoryPersistenceTestDatabase.CreateAsync();
        var activation = await target.ActivateDatasetAsync();
        await ClearEmptyHistoryAsync(target);
        var transferProbe = new TargetReadProbe();
        await using var sourceContext = source.Factory.CreateDbContext();
        await using var targetContext = target.Factory.WithInterceptor(transferProbe).CreateDbContext();
        var owners = new DatabaseTransferOwnerSessionRunner();
        var transfer = new DatabaseTransferOperation(source.Profile, target.Profile, false);
        var operations = DatabaseTransferTestSupport.For(transfer, sourceContext, targetContext, owners, activation: activation);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var run = new HistoryDatabaseTransferHandler([], owners, operations).TransferAsync(transfer, deadline.Token);
        await transferProbe.Reached.Task.WaitAsync(deadline.Token);
        var initializeProbe = new InitializationProbe(pauseAfterAcquisition: false);
        var factory = target.HistoryFactory.WithInterceptor(initializeProbe);
        var partitions = new HistoryPartitionStore(factory, factory.Options, target.Transactions);
        var initialization = partitions.GetAsync(deadline.Token);
        try {
            var processId = await initializeProbe.Requested.Task.WaitAsync(deadline.Token);
            await WaitForAdvisoryWaiterAsync(target, processId, deadline.Token);
            transferProbe.Release.TrySetResult();
            Assert.True((await run.WaitAsync(deadline.Token)).Success);
            Assert.Equal(source.Partition, await initialization.WaitAsync(deadline.Token));
            Assert.Equal(source.Partition, await target.Partitions.GetAsync(deadline.Token));
        } finally {
            transferProbe.Release.TrySetResult();
        }
        await AssertGatesAvailableAsync(target, [HistoryPartitionStore.InitializationLockKey]);
    }

    private static DatabaseTransferItemResult Result() => new("fixture", "Fixture", true, "Completed.", 0);

    private static async Task ClearEmptyHistoryAsync(HistoryPersistenceTestDatabase database) {
        await using var db = database.HistoryFactory.CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync();
        Assert.False(await db.Set<HistoryEntryRow>().AnyAsync());
        await db.Set<HistoryStorageIdentity>().ExecuteDeleteAsync();
        await db.Set<HistoryCheckpointRow>().ExecuteDeleteAsync();
        await db.Set<HistoryPolicyRow>().ExecuteDeleteAsync();
        await db.Set<HistoryPartitionRow>().ExecuteDeleteAsync();
        await transaction.CommitAsync();
    }

    private static async Task AssertGatesAvailableAsync(HistoryPersistenceTestDatabase database, IReadOnlyCollection<long> keys) {
        await using var db = database.Factory.CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync();
        foreach (var key in keys) {
            Assert.True(await db.Database.SqlQuery<bool>($"SELECT pg_try_advisory_xact_lock({key}) AS \"Value\"").SingleAsync());
        }
    }

    private static async Task WaitForAdvisoryWaiterAsync(HistoryPersistenceTestDatabase database, int processId, CancellationToken token) {
        await using var observer = database.Factory.CreateDbContext();
        while (!await observer.Database.SqlQuery<bool>($"SELECT EXISTS (SELECT 1 FROM pg_locks WHERE pid = {processId} AND locktype = 'advisory' AND NOT granted) AS \"Value\"")
                .SingleAsync(token)) {
            await Task.Delay(TimeSpan.FromMilliseconds(20), token);
        }
    }

    private sealed class BeginFailure : DbTransactionInterceptor {
        public InvalidOperationException Exception { get; } = new("Injected transfer BEGIN failure.");

        public override ValueTask<InterceptionResult<DbTransaction>> TransactionStartingAsync(DbConnection connection,
            TransactionStartingEventData eventData, InterceptionResult<DbTransaction> result, CancellationToken cancellationToken = default)
            => throw Exception;
    }

    private sealed class GateProbe : DbCommandInterceptor {
        public TaskCompletionSource<int> Requested { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool SessionGateHadTransaction { get; private set; }
        public int ProjectOwnerCommands { get; private set; }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            Observe(command, eventData);
            if (command.CommandText.Contains("pg_advisory_lock(", StringComparison.Ordinal)) {
                SessionGateHadTransaction |= command.Transaction is not null;
                Requested.TrySetResult(((NpgsqlConnection)command.Connection!).ProcessID);
            }
            return ValueTask.FromResult(result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            Observe(command, eventData);
            return ValueTask.FromResult(result);
        }

        private void Observe(DbCommand command, CommandEventData eventData) {
            if (eventData.Context is ProjectsDbContext) {
                ProjectOwnerCommands++;
            }
        }
    }

    private sealed class InitializationProbe(bool pauseAfterAcquisition) : DbCommandInterceptor {
        public TaskCompletionSource<int> Requested { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Acquired { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            if (command.CommandText.Contains("pg_advisory_xact_lock(", StringComparison.Ordinal)) {
                Requested.TrySetResult(((NpgsqlConnection)command.Connection!).ProcessID);
            }
            return ValueTask.FromResult(result);
        }

        public override async ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            int result, CancellationToken cancellationToken = default) {
            if (command.CommandText.Contains("pg_advisory_xact_lock(", StringComparison.Ordinal)) {
                Acquired.TrySetResult();
                if (pauseAfterAcquisition) {
                    await Release.Task.WaitAsync(cancellationToken);
                }
            }
            return result;
        }
    }

    private sealed class TargetReadProbe : DbCommandInterceptor {
        public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            if (eventData.Context is ProviderHistoryDbContext && !Reached.Task.IsCompleted) {
                Reached.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
            }
            return result;
        }
    }
}
