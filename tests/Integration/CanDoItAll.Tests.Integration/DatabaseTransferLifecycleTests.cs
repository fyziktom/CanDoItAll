using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace CanDoItAll.Tests.Integration;

public sealed class DatabaseTransferLifecycleTests {
    private const string ImportGate = "transfer-lifecycle-test:import";
    private const string LifecycleLock = "LOCK TABLE \"Infrastructure_DatasetLifecycle\" IN EXCLUSIVE MODE";
    private const string ImportLifecycleLock = LifecycleLock + " NOWAIT";

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
        Task<DatabaseTransferItemResult> StartAsync() => import
            ? operations.RunExclusiveImportAsync(target.Profile, [ImportGate], [typeof(Project), typeof(ProjectDefinitionReceiptRecord)],
                async (session, token) => {
                    await using var owner = await operations.CreateOwnerAsync<ProjectsDbContext>(session, static options => new(options), token);
                    return await InspectAsync(owner, token);
                }, deadline.Token)
            : operations.RunTransferAsync(transfer, new(new ProjectTransferReferenceQuery(owners).TargetEntityTypes), async (request, token) => {
                await using var owner = await owners.CreateTargetAsync<ProjectsDbContext>(request, static options => new(options), token);
                return await InspectAsync(owner, token);
            }, cancellationToken: deadline.Token);
        var run = StartAsync();
        try {
            var processId = await probe.Started.Task.WaitAsync(deadline.Token);
            await using var observer = target.Factory.CreateDbContext();
            if (import) {
                var failure = await Assert.ThrowsAsync<PostgresException>(() => run.WaitAsync(deadline.Token));
                Assert.Equal(PostgresErrorCodes.LockNotAvailable, failure.SqlState);
                await RequireFreeGateAsync(observer, ImportGate, deadline.Token);
                Assert.False(await observer.Set<Project>().AnyAsync(row => row.Id == project.Id, deadline.Token));
                Assert.False(await observer.Set<ProjectDefinitionReceiptRecord>().AnyAsync(row => row.OperationId == receipt.OperationId, deadline.Token));
            } else {
                await WaitForLifecycleWaiterAsync(observer, processId, deadline.Token);
            }
            Assert.False(entered);
            var lockCommand = import ? ImportLifecycleLock : LifecycleLock;
            var beforeLock = probe.Commands.TakeWhile(command => command.Sql != lockCommand).ToArray();
            Assert.All(beforeLock.Where(command => command.HasTransaction),
                command => Assert.Equal("SET LOCAL lock_timeout = '5s'", command.Sql));
            await transaction.CommitAsync(deadline.Token);
            if (import) {
                run = StartAsync();
            }
            Assert.False((await run.WaitAsync(deadline.Token)).Success);
            Assert.True(entered);
            Assert.True(await observer.Set<Project>().AnyAsync(row => row.Id == project.Id, deadline.Token));
            var retained = await observer.Set<ProjectDefinitionReceiptRecord>().AsNoTracking()
                .SingleAsync(row => row.OperationId == receipt.OperationId, deadline.Token);
            Assert.Equal(receipt.AcceptedPayload, retained.AcceptedPayload);
            Assert.Equal(receipt.AcceptedRevision, retained.AcceptedRevision);
            Assert.Equal(receipt.LifetimeId, retained.LifetimeId);
        } finally {
            await deadline.CancelAsync();
            try {
                await transaction.DisposeAsync();
            } finally {
                await DrainAsync(run);
            }
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
        Assert.Same(transaction, blocker.Database.CurrentTransaction);
        await using var verify = target.Factory.CreateDbContext();
        await RequireFreeGateAsync(verify, ImportGate, default);
        Assert.False(await verify.Set<Project>().AnyAsync());
        await transaction.RollbackAsync();
    }

    [Fact]
    public async Task Import_refusal_breaks_the_three_owner_lifecycle_project_binding_wait_chain() {
        await using var target = await HistoryPersistenceTestDatabase.CreateAsync();
        var activation = await target.ActivateDatasetAsync();
        var binding = await activation.ReadActiveBindingAsync(target.Profile.Profile.Id);
        var projectKey = ProjectMutationScopeKeys.ForProject(Guid.NewGuid());
        const string bindingKey = ProjectStructureSerializableMutationScope.ManagedStorageBindingScopeKey;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var proceed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var probe = new EntryProbe(proceed.Task);
        await using var importContext = target.Factory.WithInterceptor(probe).CreateDbContext();
        var operations = DatabaseTransferTestSupport.ForProfile(target.Profile, importContext, activation: activation);
        await using var structuralOwner = target.Factory.CreateDbContext();
        await using var preludeOwner = target.Factory.CreateDbContext();
        await using var observer = target.Factory.CreateDbContext();
        await structuralOwner.Database.OpenConnectionAsync(deadline.Token);
        await preludeOwner.Database.OpenConnectionAsync(deadline.Token);
        var structuralPid = ((NpgsqlConnection)structuralOwner.Database.GetDbConnection()).ProcessID;
        var preludePid = ((NpgsqlConnection)preludeOwner.Database.GetDbConnection()).ProcessID;
        var invoked = false;
        Task? structural = null;
        Task? prelude = null;
        var import = operations.RunExclusiveImportAsync(target.Profile, [bindingKey], [typeof(Project)], (session, token) => {
            invoked = true;
            return Task.FromResult(new DatabaseTransferItemResult("projects", "Projects", true, "Unexpected admission.", 0));
        }, deadline.Token);
        try {
            var importPid = await probe.Started.Task.WaitAsync(deadline.Token);
            structural = RunStructuralOwnerAsync();
            await WaitForBlockerAsync(observer, structuralPid, importPid, deadline.Token);
            prelude = RunDeletionPreludeAsync();
            await WaitForBlockerAsync(observer, preludePid, structuralPid, deadline.Token);
            Assert.False(import.IsCompleted);
            Assert.False(structural.IsCompleted);
            Assert.False(prelude.IsCompleted);
            proceed.TrySetResult();
            var failure = await Assert.ThrowsAsync<PostgresException>(() => import.WaitAsync(deadline.Token));
            Assert.Equal(PostgresErrorCodes.LockNotAvailable, failure.SqlState);
            Assert.False(invoked);
            await Task.WhenAll(structural, prelude).WaitAsync(deadline.Token);
            await RequireFreeGateAsync(observer, bindingKey, deadline.Token);
            await RequireFreeGateAsync(observer, projectKey, deadline.Token);
            Assert.False(await observer.Set<Project>().AnyAsync(deadline.Token));
            Assert.False(await observer.Set<ProjectDefinitionReceiptRecord>().AnyAsync(deadline.Token));
            var lifecycle = await observer.Set<DatasetLifecycleRecord>().AsNoTracking().SingleAsync(deadline.Token);
            Assert.Equal(binding.Namespace.Value, lifecycle.NamespaceId);
            Assert.Equal(binding.MaintenanceId, lifecycle.MaintenanceId);
            Assert.Equal(binding.SchemaVersion, lifecycle.SchemaVersion);
            Assert.Equal(binding.GuardProtocol, lifecycle.GuardProtocol);
            Assert.Equal(DatasetLifecycleState.Active, lifecycle.State);
        } finally {
            proceed.TrySetResult();
            await deadline.CancelAsync();
            await DrainAsync(import, structural, prelude);
        }

        async Task RunStructuralOwnerAsync() {
            await using var scope = await SerializableMutationScope.BeginAsync(structuralOwner, [projectKey, bindingKey], deadline.Token);
            await scope.CommitAsync(deadline.Token);
        }

        async Task RunDeletionPreludeAsync() {
            await using var transaction = await preludeOwner.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, deadline.Token);
            await DatasetLifecycleStore.RequireActiveForMutationAsync(preludeOwner, binding, deadline.Token);
            await SerializableMutationScope.AcquireRelationalScopeLocksAsync(preludeOwner, [projectKey], deadline.Token);
            await transaction.CommitAsync(deadline.Token);
        }
    }

    [Fact]
    public async Task Empty_epoch_metadata_allows_target_inspection_but_receipts_survive_and_block_replacement() {
        await using var target = await HistoryPersistenceTestDatabase.CreateAsync();
        var activation = await target.ActivateDatasetAsync();
        var binding = await activation.ReadActiveBindingAsync(target.Profile.Profile.Id);
        await using var context = target.Factory.CreateDbContext();
        Assert.Single(await context.Set<FiniteOperationStateRecord>().ToArrayAsync());
        Assert.Single(await context.Set<FiniteOperationEpochRecord>().ToArrayAsync());
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
        Assert.DoesNotContain(typeof(FiniteOperationEpochRecord), participant.EntityTypesToLock);
        Assert.DoesNotContain(typeof(FiniteOperationStateRecord), participant.EntityTypesToLock);
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
        Assert.Single(await restarted.Set<FiniteOperationEpochRecord>().ToArrayAsync());
        Assert.Single(await restarted.Set<FiniteOperationStateRecord>().ToArrayAsync());
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

    private static async Task WaitForBlockerAsync(DbContext observer, int waiter, int blocker, CancellationToken cancellationToken) {
        while (!await observer.Database.SqlQuery<bool>(
                $"SELECT {blocker} = ANY(pg_blocking_pids({waiter})) AS \"Value\"").SingleAsync(cancellationToken)) {
            await Task.Delay(TimeSpan.FromMilliseconds(20), cancellationToken);
        }
    }

    private static async Task RequireFreeGateAsync(DbContext observer, string key, CancellationToken cancellationToken) {
        await using var transaction = await observer.Database.BeginTransactionAsync(cancellationToken);
        Assert.True(await observer.Database.SqlQuery<bool>(
            $"SELECT pg_try_advisory_xact_lock(hashtextextended({key}, 0)) AS \"Value\"").SingleAsync(cancellationToken));
        await transaction.RollbackAsync(cancellationToken);
    }

    private static async Task DrainAsync(params Task?[] operations) {
        var tasks = operations.OfType<Task>().ToArray();
        try {
            await Task.WhenAll(tasks);
        } catch when (tasks.All(task => task.IsCompleted)) {
        }
    }

    private sealed class EntryProbe(Task? proceed = null) : DbCommandInterceptor {
        public ConcurrentQueue<(string Sql, bool HasTransaction)> Commands { get; } = new();
        public TaskCompletionSource<int> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            Observe(command);
            if (command.CommandText == ImportLifecycleLock && proceed is not null) {
                await proceed.WaitAsync(cancellationToken);
            }
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            Observe(command);
            return ValueTask.FromResult(result);
        }

        private void Observe(DbCommand command) {
            Commands.Enqueue((command.CommandText, command.Transaction is not null));
            if (command.CommandText is LifecycleLock or ImportLifecycleLock) {
                Started.TrySetResult(((NpgsqlConnection)command.Connection!).ProcessID);
            }
        }
    }
}
