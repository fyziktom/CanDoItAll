using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace CanDoItAll.Tests.Integration;

public sealed partial class ProjectTransferHistoryPersistenceTests {
    [Fact]
    public async Task Project_import_waits_before_its_snapshot_and_refuses_newly_committed_CRM_residue() {
        await using var fixture = await TransferFixture.CreateAsync();
        var projectId = await SeedImportProjectAsync(fixture);
        await using var writer = await fixture.TargetContextAsync();
        await using var transaction = await writer.Database.BeginTransactionAsync();
        await SerializableMutationScope.AcquireRelationalScopeLocksAsync(writer,
            [ProjectStructureSerializableMutationScope.ManagedStorageBindingScopeKey], default);
        var residue = ImportCapacityBlock(projectId);
        writer.Add(residue);
        await writer.SaveChangesAsync();
        var probe = new ImportOrderProbe();
        var transfer = fixture.TransferAsync(probe);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try {
            var processId = await probe.AdvisoryStarted.Task.WaitAsync(deadline.Token);
            await using var observer = await fixture.TargetContextAsync();
            await WaitForAdvisoryWaiterAsync(observer, processId, deadline.Token);
            Assert.DoesNotContain(probe.Commands, command => command.Transaction is not null);
            await transaction.CommitAsync(deadline.Token);
            var result = await transfer.WaitAsync(deadline.Token);
            Assert.False(result.Success);
            Assert.Equal(0, result.RecordsCopied);
            Assert.Contains("CRM capacity blocks linked to projects", result.Message, StringComparison.Ordinal);
            AssertImportCommandOrder(probe);
            Assert.False(await observer.Set<Project>().AnyAsync(project => project.Id == projectId, deadline.Token));
            var retained = await observer.Set<CapacityBlock>().AsNoTracking().SingleAsync(row => row.Id == residue.Id, deadline.Token);
            Assert.Equal(residue.RelatedProjectId, retained.RelatedProjectId);
            Assert.Equal(residue.Notes, retained.Notes);
            await RequireGateAvailableAsync(observer, ProjectStructureSerializableMutationScope.ManagedStorageBindingScopeKey, deadline.Token);
        } finally {
            await transaction.DisposeAsync();
            await transfer.WaitAsync(deadline.Token);
        }
    }

    [Fact]
    public async Task Project_import_table_contention_fails_without_mutation_and_releases_its_advisory_gate() {
        await using var fixture = await TransferFixture.CreateAsync();
        var projectId = await SeedImportProjectAsync(fixture);
        await using var writer = await fixture.TargetContextAsync();
        await using var transaction = await writer.Database.BeginTransactionAsync();
        var residue = ImportCapacityBlock(projectId);
        writer.Add(residue);
        await writer.SaveChangesAsync();
        var probe = new ImportOrderProbe();
        var failure = await Assert.ThrowsAsync<PostgresException>(() => fixture.TransferAsync(probe).WaitAsync(TimeSpan.FromSeconds(20)));
        Assert.Equal(PostgresErrorCodes.LockNotAvailable, failure.SqlState);
        var exclusion = Assert.Single(probe.Commands, command => command.Sql.StartsWith("LOCK TABLE", StringComparison.Ordinal));
        Assert.EndsWith("IN ACCESS EXCLUSIVE MODE NOWAIT", exclusion.Sql, StringComparison.Ordinal);
        Assert.DoesNotContain(probe.Commands, command => command.Sql.Contains("pg_advisory_xact_lock", StringComparison.Ordinal));
        await using var observer = await fixture.TargetContextAsync();
        Assert.False(await observer.Set<Project>().AnyAsync(project => project.Id == projectId));
        Assert.False(await observer.Set<CapacityBlock>().AnyAsync(row => row.Id == residue.Id));
        await RequireGateAvailableAsync(observer, ProjectStructureSerializableMutationScope.ManagedStorageBindingScopeKey);
        await transaction.RollbackAsync();
        Assert.True((await fixture.TransferAsync()).Success);
        Assert.True(await observer.Set<Project>().AnyAsync(project => project.Id == projectId));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Import_preserves_one_owner_transaction_and_releases_the_gate_after_commit_or_rollback(bool fail) {
        await using var database = await HistoryPersistenceTestDatabase.CreateAsync();
        var probe = new ImportOrderProbe();
        await using var maintenance = database.Factory.WithInterceptor(probe).CreateDbContext();
        var operations = DatabaseTransferTestSupport.ForProfile(database.Profile, maintenance);
        var projects = new ProjectsProfileTransferStore(operations);
        var projectId = Guid.NewGuid();
        var objectId = Guid.NewGuid();
        var original = new ImportBodyFailure();
        await using var independent = database.Factory.CreateDbContext();
        await independent.Database.OpenConnectionAsync();
        var readerProcessId = ((NpgsqlConnection)independent.Database.GetDbConnection()).ProcessID;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        Task<bool>? visibility = null;
        var operation = operations.RunExclusiveImportAsync(database.Profile,
            [ProjectStructureSerializableMutationScope.ManagedStorageBindingScopeKey], [typeof(Project), typeof(ProjectObjectRecord)],
            async (session, token) => {
                await projects.SaveAsync(session, new([new ProjectTransferProject { Id = projectId, Name = "Locked import" }], [], [], [], [], []), token);
                await using var owner = await operations.CreateOwnerAsync<WorkbenchDbContext>(session, static options => new WorkbenchDbContext(options), token);
                var exclusion = Assert.Single(probe.Commands, command => command.Sql.StartsWith("LOCK TABLE", StringComparison.Ordinal));
                Assert.Same(exclusion.Connection, owner.Database.GetDbConnection());
                var transaction = Assert.IsAssignableFrom<IDbContextTransaction>(owner.Database.CurrentTransaction);
                Assert.Same(exclusion.Transaction, transaction.GetDbTransaction());
                Assert.Equal(IsolationLevel.Serializable, transaction.GetDbTransaction().IsolationLevel);
                owner.Add(new ProjectObjectRecord { Id = objectId, ProjectId = projectId, NodeKey = "import:transaction", Title = "Exact owner transaction" });
                await owner.SaveChangesAsync(token);
                visibility = independent.Set<ProjectObjectRecord>().AnyAsync(row => row.Id == objectId, token);
                await using var observer = database.Factory.CreateDbContext();
                await WaitForRelationWaiterAsync(observer, readerProcessId, token);
                Assert.False(visibility.IsCompleted);
                if (fail) {
                    throw original;
                }
                return true;
            }, deadline.Token);
        if (fail) {
            Assert.Same(original, await Assert.ThrowsAsync<ImportBodyFailure>(() => operation));
        } else {
            Assert.True(await operation);
        }
        Assert.NotNull(visibility);
        Assert.Equal(!fail, await visibility.WaitAsync(deadline.Token));
        AssertImportCommandOrder(probe);
        await using var restarted = database.Factory.CreateDbContext();
        Assert.Equal(!fail, await restarted.Set<Project>().AnyAsync(row => row.Id == projectId));
        Assert.Equal(!fail, await restarted.Set<ProjectObjectRecord>().AnyAsync(row => row.Id == objectId));
        await RequireGateAvailableAsync(restarted, ProjectStructureSerializableMutationScope.ManagedStorageBindingScopeKey);
    }

    [Fact]
    public async Task Cancelled_import_advisory_wait_releases_its_already_acquired_session_gate() {
        const string firstGate = "transfer-import-test:first-gate";
        await using var database = await HistoryPersistenceTestDatabase.CreateAsync();
        await using var blocker = database.Factory.CreateDbContext();
        await using var blockedTransaction = await blocker.Database.BeginTransactionAsync();
        await SerializableMutationScope.AcquireRelationalScopeLocksAsync(blocker,
            [ProjectStructureSerializableMutationScope.ManagedStorageBindingScopeKey], default);
        var probe = new ImportOrderProbe();
        await using var maintenance = database.Factory.WithInterceptor(probe).CreateDbContext();
        var operations = DatabaseTransferTestSupport.ForProfile(database.Profile, maintenance);
        using var cancelled = new CancellationTokenSource();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var called = false;
        var operation = operations.RunExclusiveImportAsync(database.Profile,
            [firstGate, ProjectStructureSerializableMutationScope.ManagedStorageBindingScopeKey], [typeof(Project)], (session, token) => {
                called = true;
                return Task.FromResult(true);
            }, cancelled.Token);
        try {
            var processId = await probe.AdvisoryStarted.Task.WaitAsync(deadline.Token);
            await using var observer = database.Factory.CreateDbContext();
            await WaitForAdvisoryWaiterAsync(observer, processId, deadline.Token);
            Assert.Equal(2, probe.Commands.Count(command => command.Sql.Contains("pg_advisory_lock(", StringComparison.Ordinal)));
            cancelled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation.WaitAsync(deadline.Token));
            Assert.False(called);
            Assert.DoesNotContain(probe.Commands, command => command.Transaction is not null);
            await RequireGateAvailableAsync(observer, firstGate, deadline.Token);
        } finally {
            cancelled.Cancel();
            await blockedTransaction.RollbackAsync();
            try {
                await operation.WaitAsync(TimeSpan.FromSeconds(10));
            } catch (OperationCanceledException) {
            }
        }
    }

    [Fact]
    public async Task Import_preserves_its_original_failure_when_session_gate_cleanup_also_fails() {
        await using var database = await HistoryPersistenceTestDatabase.CreateAsync();
        var original = new ImportBodyFailure();
        var cleanup = new ImportCleanupFailure();
        var probe = new ImportOrderProbe(original, cleanup);
        await using var maintenance = database.Factory.WithInterceptor(probe).CreateDbContext();
        var operations = DatabaseTransferTestSupport.ForProfile(database.Profile, maintenance);
        var called = false;
        var failure = await Assert.ThrowsAsync<AggregateException>(() => operations.RunExclusiveImportAsync(database.Profile,
            [ProjectStructureSerializableMutationScope.ManagedStorageBindingScopeKey], [typeof(Project)], (session, token) => {
                called = true;
                return Task.FromResult(true);
            }));
        Assert.Same(original, failure.InnerExceptions[0]);
        Assert.Contains(failure.InnerExceptions, exception => ReferenceEquals(exception, cleanup));
        Assert.False(called);
        await using var observer = database.Factory.CreateDbContext();
        await RequireGateAvailableAsync(observer, ProjectStructureSerializableMutationScope.ManagedStorageBindingScopeKey);
        Assert.Empty(await observer.Set<Project>().ToArrayAsync());
    }

    private static async Task<Guid> SeedImportProjectAsync(TransferFixture fixture) {
        await using var source = await fixture.SourceContextAsync();
        var project = new Project { Name = "Import race source", CurrentPhase = "Execution" };
        source.Add(project);
        await source.SaveChangesAsync();
        return project.Id;
    }

    private static CapacityBlock ImportCapacityBlock(Guid projectId) => new() {
        PartyId = Guid.NewGuid(), RelatedProjectId = projectId, StartDateUtc = Now, EndDateUtc = Now.AddDays(1),
        Percentage = 25, Notes = "Concurrent retained project reference"
    };

    private static async Task WaitForAdvisoryWaiterAsync(DbContext observer, int processId, CancellationToken cancellationToken) {
        while (!await observer.Database.SqlQuery<bool>(
                $"SELECT EXISTS (SELECT 1 FROM pg_locks WHERE pid = {processId} AND locktype = 'advisory' AND NOT granted) AS \"Value\"")
                .SingleAsync(cancellationToken)) {
            await Task.Delay(TimeSpan.FromMilliseconds(20), cancellationToken);
        }
    }

    private static async Task RequireGateAvailableAsync(DbContext observer, string key, CancellationToken cancellationToken = default) {
        await using var transaction = await observer.Database.BeginTransactionAsync(cancellationToken);
        Assert.True(await observer.Database.SqlQuery<bool>(
            $"SELECT pg_try_advisory_xact_lock(hashtextextended({key}, 0)) AS \"Value\"").SingleAsync(cancellationToken));
        await transaction.RollbackAsync(cancellationToken);
    }

    private static async Task WaitForRelationWaiterAsync(DbContext observer, int processId, CancellationToken cancellationToken) {
        while (!await observer.Database.SqlQuery<bool>(
                $"SELECT EXISTS (SELECT 1 FROM pg_locks WHERE pid = {processId} AND locktype = 'relation' AND NOT granted) AS \"Value\"")
                .SingleAsync(cancellationToken)) {
            await Task.Delay(TimeSpan.FromMilliseconds(20), cancellationToken);
        }
    }

    private static void AssertImportCommandOrder(ImportOrderProbe probe) {
        var commands = probe.Commands.ToArray();
        var exclusionIndex = Array.FindIndex(commands, command => command.Sql.StartsWith("LOCK TABLE", StringComparison.Ordinal));
        Assert.True(exclusionIndex > 0);
        Assert.All(commands.Take(exclusionIndex), command => Assert.Null(command.Transaction));
        var exclusion = commands[exclusionIndex];
        Assert.NotNull(exclusion.Transaction);
        Assert.EndsWith("IN ACCESS EXCLUSIVE MODE NOWAIT", exclusion.Sql, StringComparison.Ordinal);
        var transactionGateIndex = Array.FindIndex(commands, command => command.Sql.Contains("pg_advisory_xact_lock", StringComparison.Ordinal));
        Assert.True(transactionGateIndex > exclusionIndex);
        Assert.Contains(commands.Take(exclusionIndex), command => command.Sql.Contains("pg_advisory_lock(", StringComparison.Ordinal));
        Assert.All(commands.Where(command => command.Transaction is not null), command => {
            Assert.Same(exclusion.Connection, command.Connection);
            Assert.Same(exclusion.Transaction, command.Transaction);
        });
    }

    private sealed record ImportCommand(string Sql, DbConnection Connection, DbTransaction? Transaction);

    private sealed class ImportOrderProbe(Exception? exclusionFailure = null, Exception? cleanupFailure = null) : DbCommandInterceptor {
        public ConcurrentQueue<ImportCommand> Commands { get; } = new();
        public TaskCompletionSource<int> AdvisoryStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            Observe(command);
            if (command.CommandText.StartsWith("LOCK TABLE", StringComparison.Ordinal) && exclusionFailure is not null) {
                throw exclusionFailure;
            }
            return ValueTask.FromResult(result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            Observe(command);
            if (command.CommandText.Contains("pg_advisory_unlock(", StringComparison.Ordinal) && cleanupFailure is not null) {
                throw cleanupFailure;
            }
            return ValueTask.FromResult(result);
        }

        private void Observe(DbCommand command) {
            Commands.Enqueue(new(command.CommandText, command.Connection!, command.Transaction));
            if (command.CommandText.Contains("pg_advisory_lock(", StringComparison.Ordinal)) {
                AdvisoryStarted.TrySetResult(((NpgsqlConnection)command.Connection!).ProcessID);
            }
        }
    }

    private sealed class ImportBodyFailure : Exception;
    private sealed class ImportCleanupFailure : Exception;
}
