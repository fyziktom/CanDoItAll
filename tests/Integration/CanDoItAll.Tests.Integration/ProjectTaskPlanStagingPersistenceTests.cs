using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectTaskPlanStagingPersistenceTests {
    [Fact]
    public async Task Batches_of_1_10_100_persist_matching_bindings_without_per_task_reads_or_locks() {
        await using var fixture = await Fixture.CreateAsync();
        await using (var seed = await fixture.ContextAsync()) {
            seed.Add(new ProjectObjectRecord {
                ProjectId = fixture.Project.ProjectId, NodeKey = "existing:issue", ObjectType = ProjectObjectType.WorkItem,
                ObjectSubtype = "issue", Title = "Existing issue", NodeReferences = new(),
                CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow
            });
            await seed.SaveChangesAsync();
        }
        int? readCommands = null;
        foreach (var count in new[] { 1, 10, 100 }) {
            await using var context = await fixture.ContextAsync();
            await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            await fixture.GuardAsync(context);
            fixture.Probe.Reset();
            var current = await fixture.Capture.ReadEnlistedAsync(context, fixture.Binding, fixture.Project);
            var reads = fixture.Probe.Commands.Count;
            Assert.Equal(readCommands ?? reads, reads);
            readCommands = reads;
            Assert.DoesNotContain(fixture.Probe.Commands, command => command.Sql.Contains("pg_advisory", StringComparison.Ordinal));
            var staged = Stage(context, fixture.Project, current, count);
            Assert.Equal(reads, fixture.Probe.Commands.Count);
            Assert.Equal(count, context.ChangeTracker.Entries<ProjectObjectRecord>().Count());
            Assert.Equal(count, context.ChangeTracker.Entries<ProjectNodeBindingRecord>().Count());
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
            await using var verify = await fixture.ContextAsync();
            var ids = staged.Tasks.Select(task => task.Fact.RecordId).ToArray();
            Assert.Equal(count, await verify.Set<ProjectObjectRecord>().CountAsync(row => ids.Contains(row.Id)));
            var bindings = await verify.Set<ProjectNodeBindingRecord>().Where(row => ids.Contains(row.ProjectObjectId)).ToArrayAsync();
            Assert.Equal(count, bindings.Length);
            Assert.All(bindings, binding => {
                Assert.Equal($"/projects/{fixture.Project.ProjectId}/structure", binding.Route);
                Assert.Equal(nameof(ProjectObjectType.WorkItem), binding.ExternalArtifactKind);
                Assert.Null(binding.ExternalArtifactId);
                Assert.Equal("", binding.StorageObjectReferenceJson);
            });
            Assert.Equal(0, await verify.Set<ProjectNodeReferenceRecord>().CountAsync(row => ids.Contains(row.ProjectObjectId)));
        }
    }

    [Fact]
    public async Task Failure_after_real_save_rolls_back_the_complete_task_and_binding_batch() {
        await using var fixture = await Fixture.CreateAsync();
        Guid[] ids;
        await using (var context = await fixture.ContextAsync()) {
            await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            await fixture.GuardAsync(context);
            var current = await fixture.Capture.ReadEnlistedAsync(context, fixture.Binding, fixture.Project);
            var staged = Stage(context, fixture.Project, current, 10);
            ids = staged.Tasks.Select(task => task.Fact.RecordId).ToArray();
            fixture.Fault.Armed = true;
            await Assert.ThrowsAsync<InjectedSaveFailure>(() => context.SaveChangesAsync());
            Assert.Equal(10, await context.Set<ProjectObjectRecord>().CountAsync(row => ids.Contains(row.Id)));
            Assert.Equal(10, await context.Set<ProjectNodeBindingRecord>().CountAsync(row => ids.Contains(row.ProjectObjectId)));
            await transaction.RollbackAsync();
        }
        await using var verify = await fixture.ContextAsync();
        Assert.Equal(0, await verify.Set<ProjectObjectRecord>().CountAsync(row => row.ProjectId == fixture.Project.ProjectId));
        Assert.Equal(0, await verify.Set<ProjectNodeBindingRecord>().CountAsync(row => ids.Contains(row.ProjectObjectId)));
    }

    [Fact]
    public async Task Held_global_binding_gate_does_not_block_the_closed_plain_task_path() {
        await using var fixture = await Fixture.CreateAsync();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var bindingOwner = await fixture.ContextAsync();
        await using var bindingTransaction = await bindingOwner.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, deadline.Token);
        await SerializableMutationScope.AcquireRelationalScopeLocksAsync(bindingOwner,
            [ProjectStructureSerializableMutationScope.ManagedStorageBindingScopeKey], deadline.Token);
        await using var context = await fixture.ContextAsync();
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, deadline.Token);
        await fixture.GuardAsync(context, deadline.Token);
        var current = await fixture.Capture.ReadEnlistedAsync(context, fixture.Binding, fixture.Project, deadline.Token);
        fixture.Probe.Reset();
        Stage(context, fixture.Project, current, 10);
        await context.SaveChangesAsync(deadline.Token);
        await transaction.CommitAsync(deadline.Token);
        Assert.Same(bindingTransaction, bindingOwner.Database.CurrentTransaction);
        Assert.DoesNotContain(fixture.Probe.Commands, command => command.Sql.Contains("pg_advisory", StringComparison.Ordinal));
        await bindingTransaction.RollbackAsync(deadline.Token);
    }

    [Fact]
    public async Task Enlisted_read_uses_exact_owner_connection_transaction_without_owning_its_lifetime() {
        await using var fixture = await Fixture.CreateAsync();
        await using var context = await fixture.ContextAsync();
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await fixture.GuardAsync(context);
        fixture.Probe.Reset();
        var current = await fixture.Capture.ReadEnlistedAsync(context, fixture.Binding, fixture.Project);
        Assert.Equal(fixture.Project, current.Basis.Project.Expectation.Project);
        Assert.Equal(0, current.CandidateCount);
        Assert.Equal(ProjectTaskPlanBasisLimits.ScalarBytes(current.Basis.Project.Scalars), current.SourceBytes);
        Assert.True(fixture.Probe.Commands.Count >= 4);
        Assert.All(fixture.Probe.Commands, command => {
            Assert.Same(context.Database.GetDbConnection(), command.Connection);
            Assert.Same(transaction.GetDbTransaction(), command.Transaction);
            Assert.DoesNotContain("pg_advisory", command.Sql, StringComparison.Ordinal);
            Assert.DoesNotContain("FOR SHARE", command.Sql, StringComparison.Ordinal);
        });
        Assert.Equal(0, fixture.Probe.TransactionStarts);
        Assert.Equal(0, fixture.Probe.TransactionCommits);
        Assert.Same(transaction, context.Database.CurrentTransaction);
        await transaction.CommitAsync();
        Assert.Equal(1, fixture.Probe.TransactionCommits);
        var standalone = await fixture.Capture.CaptureAsync(fixture.Project, (_, _) => Task.CompletedTask);
        Assert.IsType<ProjectTaskPlanBasisReadResult.Captured>(standalone);
    }

    [Fact]
    public async Task Foreign_ended_wrong_isolation_and_nested_different_transactions_refuse_before_read_sql() {
        await using var fixture = await Fixture.CreateAsync();
        await using var owner = await fixture.ContextAsync();
        fixture.Probe.Reset();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Read(owner));
        Assert.Empty(fixture.Probe.Commands);
        await using (var wrongIsolation = await owner.Database.BeginTransactionAsync(IsolationLevel.Serializable)) {
            fixture.Probe.Reset();
            await Assert.ThrowsAsync<InvalidOperationException>(() => Read(owner));
            Assert.Empty(fixture.Probe.Commands);
        }
        var otherConnection = new Npgsql.NpgsqlConnectionStringBuilder(owner.Database.GetConnectionString()) {
            Database = "unopened-foreign-task-plan-database"
        };
        await using var foreign = new WorkbenchDbContext(new DbContextOptionsBuilder<WorkbenchDbContext>()
            .UseNpgsql(otherConnection.ConnectionString).Options);
        Assert.Throws<InvalidOperationException>(() => fixture.Transactions.RequireOwnerProfile(foreign));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Read(foreign));
        await using var transaction = await owner.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        using (fixture.Transactions.Enter(owner)) {
            await using var other = await fixture.ContextAsync();
            await using var otherTransaction = await other.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            fixture.Probe.Reset();
            await Assert.ThrowsAsync<InvalidOperationException>(() => Read(other));
            Assert.Empty(fixture.Probe.Commands);
        }
        await transaction.CommitAsync();
        fixture.Probe.Reset();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Read(owner));
        Assert.Empty(fixture.Probe.Commands);

        Task<ProjectTaskPlanCurrentMaterial> Read(WorkbenchDbContext context)
            => fixture.Capture.ReadEnlistedAsync(context, fixture.Binding, fixture.Project);
    }

    [Fact]
    public async Task Non_task_work_items_count_toward_post_append_capacity_without_becoming_plan_facts() {
        await using var fixture = await Fixture.CreateAsync();
        await using (var seed = await fixture.ContextAsync()) {
            seed.AddRange(Enumerable.Range(0, 1_000).Select(index => new ProjectObjectRecord {
                ProjectId = fixture.Project.ProjectId, NodeKey = $"issue:{index}", ObjectType = ProjectObjectType.WorkItem,
                ObjectSubtype = "issue", Title = "Issue", MetadataJson = "{}", NodeReferences = new(),
                CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow
            }));
            await seed.SaveChangesAsync();
        }
        await using var context = await fixture.ContextAsync();
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await fixture.GuardAsync(context);
        var current = await fixture.Capture.ReadEnlistedAsync(context, fixture.Binding, fixture.Project);
        Assert.Empty(current.Basis.Tasks);
        Assert.Equal(1_000, current.CandidateCount);
        Assert.True(current.SourceBytes > ProjectTaskPlanBasisLimits.ScalarBytes(current.Basis.Project.Scalars));
        fixture.Probe.Reset();
        var failure = Assert.Throws<ProjectTaskPlanBasisException>(() => Stage(context, fixture.Project, current, 1));
        Assert.Equal(ProjectTaskPlanBasisRefusal.LimitExceeded, failure.Reason);
        Assert.Empty(context.ChangeTracker.Entries());
        Assert.Empty(fixture.Probe.Commands);
        await transaction.RollbackAsync();
        Assert.Equal(1_000, await context.Set<ProjectObjectRecord>().CountAsync(row => row.ProjectId == fixture.Project.ProjectId));
        Assert.Equal(0, await context.Set<ProjectNodeBindingRecord>().CountAsync(binding =>
            context.Set<ProjectObjectRecord>().Any(row => row.Id == binding.ProjectObjectId && row.ProjectId == fixture.Project.ProjectId)));
    }

    private static ProjectTaskPlanStagedBatch Stage(WorkbenchDbContext context, ProjectWriteAdmission project,
        ProjectTaskPlanCurrentMaterial current, int count) => ProjectTaskPlanTaskStaging.StageNewPlainTasks(context, project,
            ProjectTaskPlanProposal.Normalize(new(1, Enumerable.Range(0, count)
                .Select(index => new ProjectTaskPlanDraftItem($"Task {index}", "Description", "Notes", index % 7)).ToArray())),
            current, current.Basis.CapturedAtUtc);

    private sealed class Fixture(TestApplication application, AsyncServiceScope scope, Probe probe, SaveFault fault,
        ProjectWriteAdmission project, DatasetActivationBinding binding) : IAsyncDisposable {
        internal ProjectWriteAdmission Project => project;
        internal DatasetActivationBinding Binding => binding;
        internal Probe Probe => probe;
        internal SaveFault Fault => fault;
        internal CoordinatedDatabaseTransaction Transactions => scope.ServiceProvider.GetRequiredService<CoordinatedDatabaseTransaction>();
        internal ProjectTaskPlanBasisCapture Capture => new(scope.ServiceProvider.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>(),
            scope.ServiceProvider.GetRequiredService<ProjectDefinitionReader>(),
            scope.ServiceProvider.GetRequiredService<ProjectWriteAdmissionService>(), Transactions,
            scope.ServiceProvider.GetRequiredService<DatasetActivationService>());

        internal static async Task<Fixture> CreateAsync() {
            var probe = new Probe();
            var fault = new SaveFault();
            var application = await TestApplication.CreateAsync(new TestHarnessOptions {
                ConfigureServices = services => {
                    services.ConfigureDbContext<ProjectsDbContext>(options => options.AddInterceptors(probe, probe.Transactions), ServiceLifetime.Singleton);
                    services.ConfigureDbContext<WorkbenchDbContext>(options => options.AddInterceptors(probe, probe.Transactions, fault), ServiceLifetime.Singleton);
                }
            });
            var scope = application.Services.CreateAsyncScope();
            try {
                var services = scope.ServiceProvider;
                await using var context = await services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync();
                var project = new Project { Name = "Staging project", Slug = Guid.NewGuid().ToString("N") };
                context.Add(project);
                await context.SaveChangesAsync();
                var admissions = services.GetRequiredService<ProjectWriteAdmissionService>();
                var target = await admissions.CaptureAsync(project.Id);
                var binding = await services.GetRequiredService<DatasetActivationService>().ReadActiveBindingAsync(admissions.DatabaseProfileId);
                return new(application, scope, probe, fault, target!, binding);
            } catch {
                await scope.DisposeAsync();
                await application.DisposeAsync();
                throw;
            }
        }

        internal Task<WorkbenchDbContext> ContextAsync()
            => scope.ServiceProvider.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync();

        internal async Task GuardAsync(WorkbenchDbContext context, CancellationToken cancellationToken = default) {
            await context.Database.ExecuteSqlRawAsync("SET LOCAL transaction_timeout = '15s'; SET LOCAL lock_timeout = '2s'", cancellationToken);
            await DatasetLifecycleStore.RequireActiveForMutationAsync(context, Binding, cancellationToken);
            await SerializableMutationScope.AcquireRelationalScopeLocksAsync(context,
                [ProjectMutationScopeKeys.ForProject(Project.ProjectId)], cancellationToken);
        }

        public async ValueTask DisposeAsync() {
            await scope.DisposeAsync();
            await application.DisposeAsync();
        }
    }

    private sealed class InjectedSaveFailure : Exception {
    }

    private sealed class SaveFault : SaveChangesInterceptor {
        internal bool Armed { get; set; }

        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default) {
            if (Armed) {
                Armed = false;
                throw new InjectedSaveFailure();
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class Probe : DbCommandInterceptor {
        internal ConcurrentQueue<(string Sql, DbConnection? Connection, DbTransaction? Transaction)> Commands { get; } = new();
        internal TransactionProbe Transactions { get; } = new();
        internal int TransactionStarts => Transactions.Starts;
        internal int TransactionCommits => Transactions.Commits;

        internal void Reset() {
            Commands.Clear();
            Transactions.Starts = 0;
            Transactions.Commits = 0;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            Commands.Enqueue((command.CommandText, command.Connection, command.Transaction));
            return ValueTask.FromResult(result);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            Commands.Enqueue((command.CommandText, command.Connection, command.Transaction));
            return ValueTask.FromResult(result);
        }

        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default) {
            Commands.Enqueue((command.CommandText, command.Connection, command.Transaction));
            return ValueTask.FromResult(result);
        }
    }

    private sealed class TransactionProbe : DbTransactionInterceptor {
        internal int Starts { get; set; }
        internal int Commits { get; set; }

        public override ValueTask<DbTransaction> TransactionStartedAsync(DbConnection connection, TransactionEndEventData eventData,
            DbTransaction result, CancellationToken cancellationToken = default) {
            Starts++;
            return ValueTask.FromResult(result);
        }

        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default) {
            Commits++;
            return Task.CompletedTask;
        }
    }
}
