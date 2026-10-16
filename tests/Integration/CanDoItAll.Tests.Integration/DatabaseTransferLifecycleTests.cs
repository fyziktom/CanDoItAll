using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace CanDoItAll.Tests.Integration;

public sealed class DatabaseTransferLifecycleTests {
    private const string ImportGate = "transfer-lifecycle-test:import";
    private const string LifecycleLock = "LOCK TABLE \"Infrastructure_DatasetLifecycle\" IN EXCLUSIVE MODE";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Target_snapshot_observes_project_and_receipt_committed_while_lifecycle_lock_waits(bool import) {
        await using var source = await HistoryPersistenceTestDatabase.CreateAsync();
        await using var target = await HistoryPersistenceTestDatabase.CreateAsync();
        var activation = await target.ActivateDatasetAsync();
        var binding = await activation.ReadActiveBindingAsync(target.Profile.Profile.Id);
        await using var writer = target.Factory.CreateDbContext();
        await using var transaction = await writer.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await DatasetLifecycleStore.RequireActiveForMutationAsync(writer, binding);
        var project = new Project { Name = "Committed during transfer admission" };
        var receipt = Receipt(binding, project);
        writer.AddRange(project, receipt);
        await writer.SaveChangesAsync();
        var probe = new EntryProbe();
        await using var sourceContext = source.Factory.CreateDbContext();
        await using var targetContext = target.Factory.WithInterceptor(probe).CreateDbContext();
        var owners = new DatabaseTransferOwnerSessionRunner();
        var transfer = new DatabaseTransferOperation(source.Profile, target.Profile, true);
        var operations = DatabaseTransferTestSupport.For(transfer, sourceContext, targetContext, owners, activation: activation);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var entered = false;
        async Task<DatabaseTransferItemResult> InspectAsync(ProjectsDbContext owner, CancellationToken token) {
            entered = true;
            Assert.Equal(IsolationLevel.Serializable, owner.Database.CurrentTransaction!.GetDbTransaction().IsolationLevel);
            Assert.True(await owner.Set<Project>().AnyAsync(row => row.Id == project.Id, token));
            Assert.True(await owner.Set<ProjectDefinitionReceiptRecord>().AnyAsync(row => row.OperationId == receipt.OperationId, token));
            return new("projects", "Projects", false, "Retained project and receipt prevent replacement.", 0);
        }
        Task<DatabaseTransferItemResult> run = import
            ? operations.RunExclusiveImportAsync(target.Profile, [ImportGate], [typeof(Project), typeof(ProjectDefinitionReceiptRecord)],
                async (session, token) => {
                    await using var owner = await operations.CreateOwnerAsync<ProjectsDbContext>(session, static options => new(options), token);
                    return await InspectAsync(owner, token);
                }, deadline.Token)
            : operations.RunTransferAsync(transfer, new(new ProjectTransferReferenceQuery(owners).TargetEntityTypes), async (request, token) => {
                await using var owner = await owners.CreateTargetAsync<ProjectsDbContext>(request, static options => new(options), token);
                return await InspectAsync(owner, token);
            }, cancellationToken: deadline.Token);
        try {
            var processId = await probe.Started.Task.WaitAsync(deadline.Token);
            await using var observer = target.Factory.CreateDbContext();
            await WaitForLifecycleWaiterAsync(observer, processId, deadline.Token);
            Assert.False(entered);
            var beforeLock = probe.Commands.TakeWhile(command => command.Sql != LifecycleLock).ToArray();
            Assert.All(beforeLock.Where(command => command.HasTransaction),
                command => Assert.Equal("SET LOCAL lock_timeout = '5s'", command.Sql));
            await transaction.CommitAsync(deadline.Token);
            Assert.False((await run.WaitAsync(deadline.Token)).Success);
            Assert.True(entered);
            Assert.True(await observer.Set<Project>().AnyAsync(row => row.Id == project.Id, deadline.Token));
            Assert.True(await observer.Set<ProjectDefinitionReceiptRecord>().AnyAsync(row => row.OperationId == receipt.OperationId, deadline.Token));
        } finally {
            await transaction.DisposeAsync();
            await run.WaitAsync(deadline.Token);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Captured_activation_mismatch_rejects_before_target_participants(bool import) {
        await using var source = await HistoryPersistenceTestDatabase.CreateAsync();
        await using var target = await HistoryPersistenceTestDatabase.CreateAsync();
        var activation = await target.ActivateDatasetAsync();
        var captured = await activation.ReadActiveBindingAsync(target.Profile.Profile.Id);
        await using (var change = target.Factory.CreateDbContext()) {
            await change.Set<DatasetLifecycleRecord>().ExecuteUpdateAsync(setters => setters
                .SetProperty(row => row.MaintenanceId, Guid.NewGuid()));
        }
        var operations = DatabaseTransferTestSupport.Create(activation: activation);
        var invoked = false;
        Task<DatabaseTransferItemResult> run = import
            ? operations.RunExclusiveImportAsync(target.Profile, [ImportGate], [typeof(Project)], (session, token) => {
                invoked = true;
                return Task.FromResult(new DatabaseTransferItemResult("projects", "Projects", true, "Unexpected admission.", 0));
            })
            : operations.RunTransferAsync(new(source.Profile, target.Profile, true), new([typeof(Project)]), (request, token) => {
                invoked = true;
                return Task.FromResult(new DatabaseTransferItemResult("projects", "Projects", true, "Unexpected admission.", 0));
            });
        var failure = await Assert.ThrowsAsync<DatasetActivationMismatchException>(() => run);
        Assert.Equal(captured, failure.CapturedBinding);
        Assert.False(invoked);
        await using var verify = target.Factory.CreateDbContext();
        Assert.False(await verify.Set<Project>().AnyAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Lifecycle_contention_refuses_boundedly_and_releases_the_import_session_gate(bool import) {
        await using var source = await HistoryPersistenceTestDatabase.CreateAsync();
        await using var target = await HistoryPersistenceTestDatabase.CreateAsync();
        var activation = await target.ActivateDatasetAsync();
        await using var blocker = target.Factory.CreateDbContext();
        await using var transaction = await blocker.Database.BeginTransactionAsync();
        await DatasetLifecycleStore.RequireActiveForMutationAsync(blocker, await activation.ReadActiveBindingAsync(target.Profile.Profile.Id));
        var operations = DatabaseTransferTestSupport.Create(activation: activation);
        var invoked = false;
        Task<DatabaseTransferItemResult> run = import
            ? operations.RunExclusiveImportAsync(target.Profile, [ImportGate], [typeof(Project)], (session, token) => {
                invoked = true;
                return Task.FromResult(new DatabaseTransferItemResult("projects", "Projects", true, "Unexpected admission.", 0));
            })
            : operations.RunTransferAsync(new(source.Profile, target.Profile, true), new([typeof(Project)]), (request, token) => {
                invoked = true;
                return Task.FromResult(new DatabaseTransferItemResult("projects", "Projects", true, "Unexpected admission.", 0));
            });
        var failure = await Assert.ThrowsAsync<PostgresException>(() => run.WaitAsync(TimeSpan.FromSeconds(15)));
        Assert.Equal(PostgresErrorCodes.LockNotAvailable, failure.SqlState);
        Assert.False(invoked);
        await using var verify = target.Factory.CreateDbContext();
        await using var verification = await verify.Database.BeginTransactionAsync();
        Assert.True(await verify.Database.SqlQuery<bool>(
            $"SELECT pg_try_advisory_xact_lock(hashtextextended({ImportGate}, 0)) AS \"Value\"").SingleAsync());
        Assert.False(await verify.Set<Project>().AnyAsync());
        await transaction.RollbackAsync();
    }

    [Fact]
    public async Task Empty_epoch_metadata_allows_target_inspection_but_receipts_survive_and_block_replacement() {
        await using var target = await HistoryPersistenceTestDatabase.CreateAsync();
        var activation = await target.ActivateDatasetAsync();
        var binding = await activation.ReadActiveBindingAsync(target.Profile.Profile.Id);
        await using var context = target.Factory.CreateDbContext();
        Assert.Single(await context.Set<ProjectOperationStateRecord>().ToArrayAsync());
        Assert.Single(await context.Set<ProjectOperationEpochRecord>().ToArrayAsync());
        var inspections = new ProjectTransferTargetInspectionRunner();
        var participant = new ProjectsProjectTransferTargetStateParticipant(inspections);
        using (inspections.Begin(target.Profile, context, ProjectTransferTargetInspectionMode.Independent, out var request)) {
            Assert.Empty(await participant.FindResiduesAsync(request, default));
        }
        var receipt = Receipt(binding, new Project());
        context.Add(receipt);
        await context.SaveChangesAsync();
        using (inspections.Begin(target.Profile, context, ProjectTransferTargetInspectionMode.Independent, out var request)) {
            var residue = Assert.Single(await participant.FindResiduesAsync(request, default));
            Assert.Equal("retained project definition receipts", residue.Description);
            Assert.False(residue.BlocksSourceTransfer);
        }
        Assert.Contains(typeof(ProjectDefinitionReceiptRecord), participant.EntityTypesToLock);
        Assert.DoesNotContain(typeof(ProjectOperationEpochRecord), participant.EntityTypesToLock);
        Assert.DoesNotContain(typeof(ProjectOperationStateRecord), participant.EntityTypesToLock);
        var operations = DatabaseTransferTestSupport.ForProfile(target.Profile, context, activation: activation);
        var data = new ProjectsProfileTransferStore(operations);
        var exported = await operations.RunIndependentAsync(target.Profile, data.LoadAsync);
        Assert.Empty(exported.Projects);
        await Assert.ThrowsAsync<InvalidOperationException>(() => operations.RunExclusiveImportAsync(target.Profile, [ImportGate],
            participant.EntityTypesToLock, async (session, token) => {
                await data.ClearAsync(session, token);
                return true;
            }));
        await using var restarted = target.Factory.CreateDbContext();
        Assert.Single(await restarted.Set<ProjectDefinitionReceiptRecord>().ToArrayAsync());
        Assert.Single(await restarted.Set<ProjectOperationEpochRecord>().ToArrayAsync());
        Assert.Single(await restarted.Set<ProjectOperationStateRecord>().ToArrayAsync());
    }

    internal static ProjectDefinitionReceiptRecord Receipt(DatasetActivationBinding binding, Project project) => new() {
        NamespaceId = binding.Namespace.Value,
        Epoch = 1,
        CallerKind = ProjectMutationCallerKind.LocalWorkspace,
        CallerKey = new byte[32],
        OperationId = Guid.NewGuid(),
        SchemaVersion = 1,
        NormalizationVersion = 1,
        ResultVersion = 1,
        CommandKind = ProjectDefinitionCommandKind.Replace,
        SemanticDigest = new byte[32],
        ProjectId = project.Id,
        LifetimeId = project.LifetimeId,
        AcceptedRevision = project.ConcurrencyToken,
        AcceptedAtUtc = DateTimeOffset.UtcNow,
        AcceptedPayload = [1]
    };

    private static async Task WaitForLifecycleWaiterAsync(DbContext observer, int processId, CancellationToken cancellationToken) {
        while (!await observer.Database.SqlQuery<bool>(
                $"SELECT EXISTS (SELECT 1 FROM pg_locks WHERE pid = {processId} AND relation = '\"Infrastructure_DatasetLifecycle\"'::regclass AND mode = 'ExclusiveLock' AND NOT granted) AS \"Value\"")
                .SingleAsync(cancellationToken)) {
            await Task.Delay(TimeSpan.FromMilliseconds(20), cancellationToken);
        }
    }

    private sealed class EntryProbe : DbCommandInterceptor {
        public ConcurrentQueue<(string Sql, bool HasTransaction)> Commands { get; } = new();
        public TaskCompletionSource<int> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            Observe(command);
            return ValueTask.FromResult(result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            Observe(command);
            return ValueTask.FromResult(result);
        }

        private void Observe(DbCommand command) {
            Commands.Enqueue((command.CommandText, command.Transaction is not null));
            if (command.CommandText == LifecycleLock) {
                Started.TrySetResult(((NpgsqlConnection)command.Connection!).ProcessID);
            }
        }
    }
}
