using System.Collections.Concurrent;
using System.Data.Common;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectTaskPlanBasisCaptureTests {
    [Fact]
    public async Task Scalar_and_task_capture_share_one_transaction_and_leave_standalone_reads_usable() {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.SeedTasksAsync(1);
        fixture.Probe.Arm();
        var basis = Captured(await fixture.CaptureAsync());
        Assert.Equal(fixture.Project, basis.Project.Expectation.Project);
        Assert.Equal("Original objective", basis.Project.Scalars.Objective);
        Assert.Equal("Task 0", Assert.Single(basis.Tasks).Title);
        Assert.True(fixture.Probe.Reads.Count >= 3);
        var first = fixture.Probe.Reads.First();
        Assert.NotNull(first.Transaction);
        Assert.All(fixture.Probe.Reads, read => {
            Assert.Same(first.Connection, read.Connection);
            Assert.Same(first.Transaction, read.Transaction);
        });
        fixture.Probe.Disarm();
        Assert.True((await fixture.Definitions.ReadAsync(fixture.Project.ProjectId)).IsSuccess);
    }

    [Fact]
    public async Task Capture_waiting_for_project_gate_reads_committed_scalar_and_empty_scope_insertion() {
        await using var fixture = await Fixture.CreateAsync();
        await using var owner = await fixture.WorkbenchAsync();
        await using var transaction = await owner.Database.BeginTransactionAsync();
        await fixture.LockProjectAsync(owner);
        fixture.Probe.Arm();
        var capture = fixture.CaptureAsync();
        try {
            await fixture.Probe.ProjectGateReached.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await owner.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Projects_Projects\" SET \"Objective\" = {"Committed objective"}, \"ConcurrencyToken\" = {Guid.NewGuid()} WHERE \"Id\" = {fixture.Project.ProjectId}");
            owner.Add(TaskRow(fixture.Project.ProjectId, 0));
            await owner.SaveChangesAsync();
            await transaction.CommitAsync();
        } finally {
            await transaction.DisposeAsync();
            await capture;
        }
        var basis = Captured(await capture);
        Assert.Equal("Committed objective", basis.Project.Scalars.Objective);
        Assert.Single(basis.Tasks);
    }

    [Fact]
    public async Task Capture_gate_prevents_a_mixed_scalar_and_task_snapshot() {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.SeedTasksAsync(1);
        fixture.Probe.Arm(pauseScalar: true);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var capture = fixture.CaptureAsync(deadline.Token);
        var writerAttempt = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task? writer = null;
        try {
            await fixture.Probe.ScalarRead.Task.WaitAsync(deadline.Token);
            writer = WriteAsync();
            await writerAttempt.Task.WaitAsync(deadline.Token);
            Assert.False(writer.IsCompleted);
        } finally {
            fixture.Probe.ReleaseScalar.TrySetResult();
            await capture;
            if (writer is not null) {
                await writer;
            }
        }
        var basis = Captured(await capture);
        Assert.Equal("Original objective", basis.Project.Scalars.Objective);
        Assert.Single(basis.Tasks);
        fixture.Probe.Disarm();
        var after = Captured(await fixture.CaptureAsync());
        Assert.Equal("Later objective", after.Project.Scalars.Objective);
        Assert.Equal(2, after.Tasks.Count);

        async Task WriteAsync() {
            await using var owner = await fixture.WorkbenchAsync();
            await using var transaction = await owner.Database.BeginTransactionAsync();
            writerAttempt.TrySetResult();
            await fixture.LockProjectAsync(owner);
            await owner.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Projects_Projects\" SET \"Objective\" = {"Later objective"}, \"ConcurrencyToken\" = {Guid.NewGuid()} WHERE \"Id\" = {fixture.Project.ProjectId}");
            owner.Add(TaskRow(fixture.Project.ProjectId, 1));
            await owner.SaveChangesAsync();
            await transaction.CommitAsync();
        }
    }

    [Fact]
    public async Task Lifecycle_exclusion_and_binding_validation_precede_payload_reads() {
        await using var fixture = await Fixture.CreateAsync();
        var binding = await fixture.Services.GetRequiredService<DatasetActivationService>()
            .ReadActiveBindingAsync(fixture.Project.DatabaseProfileId);
        await using var owner = await fixture.WorkbenchAsync();
        await using var transaction = await owner.Database.BeginTransactionAsync();
        await DatasetLifecycleStore.RequireActiveForMaintenanceAsync(owner, binding);
        fixture.Probe.Arm();
        var capture = fixture.CaptureAsync();
        try {
            var capturePid = await fixture.Probe.CaptureBackend.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await WaitForLifecycleWaiterAsync(owner, capturePid);
            await owner.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Infrastructure_DatasetLifecycle\" SET \"NamespaceId\" = {Guid.NewGuid()} WHERE \"Id\" = 1");
            await transaction.CommitAsync();
        } finally {
            await transaction.DisposeAsync();
            await capture;
        }
        Refused(await capture, ProjectTaskPlanBasisRefusal.DatasetChanged);
        Assert.Equal(0, fixture.Probe.PayloadReads);
    }

    [Fact]
    public async Task Candidate_1001_refuses_before_materializing_any_task_text() {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.SeedTasksAsync(1_001);
        fixture.Probe.Arm();
        Refused(await fixture.CaptureAsync(), ProjectTaskPlanBasisRefusal.LimitExceeded);
        Assert.Equal(0, fixture.Probe.PayloadReads);
        Assert.Equal(1, fixture.Probe.PreflightReads);
    }

    [Fact]
    public async Task Field_and_aggregate_byte_preflight_refuses_before_payload_and_accepts_exact_field_limit() {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.SeedTasksAsync(1, new string('\u00e9', 32_768));
        fixture.Probe.Arm();
        Assert.Equal(32_768, Assert.Single(Captured(await fixture.CaptureAsync()).Tasks).Notes.Length);
        await using (var context = await fixture.WorkbenchAsync()) {
            await context.Set<ProjectObjectRecord>().Where(row => row.ProjectId == fixture.Project.ProjectId)
                .ExecuteUpdateAsync(update => update.SetProperty(row => row.Notes, new string('\u00e9', 32_769)));
        }
        fixture.Probe.Arm();
        Refused(await fixture.CaptureAsync(), ProjectTaskPlanBasisRefusal.LimitExceeded);
        Assert.Equal(0, fixture.Probe.PayloadReads);
        await using (var context = await fixture.WorkbenchAsync()) {
            await context.Set<ProjectObjectRecord>().Where(row => row.ProjectId == fixture.Project.ProjectId).ExecuteDeleteAsync();
        }
        await fixture.SeedTasksAsync(33, new string('a', 65_536));
        fixture.Probe.Arm();
        Refused(await fixture.CaptureAsync(), ProjectTaskPlanBasisRefusal.LimitExceeded);
        Assert.Equal(0, fixture.Probe.PayloadReads);
    }

    [Fact]
    public async Task Missing_lifetime_and_profile_refusals_never_substitute_a_current_admission() {
        await using var fixture = await Fixture.CreateAsync();
        Refused(await fixture.Capture.CaptureAsync(new(fixture.Project.DatabaseProfileId, Guid.NewGuid(), Guid.NewGuid()), Allow),
            ProjectTaskPlanBasisRefusal.SourceMissing);
        Refused(await fixture.Capture.CaptureAsync(new(fixture.Project.DatabaseProfileId, fixture.Project.ProjectId, Guid.NewGuid()), Allow),
            ProjectTaskPlanBasisRefusal.LifetimeChanged);
        Refused(await fixture.Capture.CaptureAsync(new(Guid.NewGuid(), fixture.Project.ProjectId, fixture.Project.LifetimeId), Allow),
            ProjectTaskPlanBasisRefusal.LifetimeChanged);
        Assert.Equal(fixture.Project, Captured(await fixture.CaptureAsync()).Project.Expectation.Project);
    }

    [Fact]
    public async Task Invalid_stored_selected_metadata_refuses_but_excluded_extensions_are_nonmaterial() {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.SeedTasksAsync(1);
        var original = Captured(await fixture.CaptureAsync());
        await SetMetadataAsync("{}");
        Refused(await fixture.CaptureAsync(), ProjectTaskPlanBasisRefusal.InvalidSource);
        await SetMetadataAsync("""{"workItem":{"workItemKind":"task","description":"Description","executionState":"notStarted","expectedCostAmount":99},"extension":{"ignored":true}}""");
        var changed = Captured(await fixture.CaptureAsync());
        Assert.True(ProjectTaskPlanBasisCodec.MaterialEquals(original, changed));

        async Task SetMetadataAsync(string value) {
            await using var context = await fixture.WorkbenchAsync();
            await context.Set<ProjectObjectRecord>().Where(row => row.ProjectId == fixture.Project.ProjectId)
                .ExecuteUpdateAsync(update => update.SetProperty(row => row.MetadataJson, value));
        }
    }

    [Fact]
    public async Task Enlisted_reader_requires_a_real_matching_owner_transaction() {
        await using var fixture = await Fixture.CreateAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Definitions.ReadScalarsEnlistedAsync(fixture.Project));
        await using var owner = await fixture.WorkbenchAsync();
        var transactions = fixture.Services.GetRequiredService<CoordinatedDatabaseTransaction>();
        Assert.Throws<InvalidOperationException>(() => transactions.Enter(owner));
        await using var invalid = new WorkbenchDbContext(new DbContextOptionsBuilder<WorkbenchDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        Assert.Throws<InvalidOperationException>(() => transactions.RequireOwnerProfile(invalid));
        var unsupported = new ProjectTaskPlanBasisCapture(new InMemoryFactory(), fixture.Definitions,
            fixture.Services.GetRequiredService<ProjectWriteAdmissionService>(), transactions,
            fixture.Services.GetRequiredService<DatasetActivationService>());
        await Assert.ThrowsAsync<NotSupportedException>(() => unsupported.CaptureAsync(fixture.Project, Allow));
        var otherConnection = new Npgsql.NpgsqlConnectionStringBuilder(owner.Database.GetConnectionString()) {
            Database = "unopened-other-database"
        };
        await using var other = new WorkbenchDbContext(new DbContextOptionsBuilder<WorkbenchDbContext>()
            .UseNpgsql(otherConnection.ConnectionString).Options);
        Assert.Throws<InvalidOperationException>(() => transactions.RequireOwnerProfile(other));
        await using var transaction = await owner.Database.BeginTransactionAsync();
        using (transactions.Enter(owner)) {
            Assert.True((await fixture.Definitions.ReadScalarsEnlistedAsync(fixture.Project)).IsSuccess);
        }
        await transaction.CommitAsync();
    }

    [Fact]
    public async Task Cancellation_and_final_authority_rejection_release_the_project_gate() {
        await using var fixture = await Fixture.CreateAsync();
        await using (var owner = await fixture.WorkbenchAsync()) {
            await using var transaction = await owner.Database.BeginTransactionAsync();
            await fixture.LockProjectAsync(owner);
            fixture.Probe.Arm();
            using var cancelled = new CancellationTokenSource();
            var capture = fixture.Capture.CaptureAsync(fixture.Project, Allow, cancelled.Token);
            await fixture.Probe.ProjectGateReached.Task.WaitAsync(TimeSpan.FromSeconds(10));
            cancelled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => capture);
        }
        var calls = 0;
        await Assert.ThrowsAsync<ReadAuthorityRevokedException>(() => fixture.Capture.CaptureAsync(fixture.Project, (_, _) => {
            if (Interlocked.Increment(ref calls) == 2) {
                throw new ReadAuthorityRevokedException();
            }
            return Task.CompletedTask;
        }));
        Assert.Equal(2, calls);
        Assert.Empty(Captured(await fixture.CaptureAsync()).Tasks);
    }

    private static Task Allow(ProjectWriteAdmission _, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    private static ProjectTaskPlanBasis Captured(ProjectTaskPlanBasisReadResult result)
        => Assert.IsType<ProjectTaskPlanBasisReadResult.Captured>(result).Basis;

    private static void Refused(ProjectTaskPlanBasisReadResult result, ProjectTaskPlanBasisRefusal expected)
        => Assert.Equal(expected, Assert.IsType<ProjectTaskPlanBasisReadResult.Refused>(result).Reason);

    private static ProjectObjectRecord TaskRow(Guid projectId, int index, string notes = "") => new() {
        ProjectId = projectId, NodeKey = $"task:{index}", ObjectType = ProjectObjectType.WorkItem, ObjectSubtype = "task",
        Title = $"Task {index}", Notes = notes, Status = "Draft", MetadataJson =
            """{"workItem":{"workItemKind":"task","description":"Description","executionState":"notStarted"}}""",
        CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow
    };

    private sealed class ReadAuthorityRevokedException : Exception {
    }

    private static async Task WaitForLifecycleWaiterAsync(WorkbenchDbContext observer, int capturePid) {
        var holderPid = ((NpgsqlConnection)observer.Database.GetDbConnection()).ProcessID;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (true) {
            var waiting = await observer.Database.SqlQuery<bool>(
                $"SELECT {holderPid} = ANY(pg_blocking_pids({capturePid})) AS \"Value\"").SingleAsync(deadline.Token);
            if (waiting) {
                return;
            }
            await Task.Delay(TimeSpan.FromMilliseconds(10), deadline.Token);
        }
    }
    private sealed class Fixture(TestApplication application, AsyncServiceScope scope, CaptureProbe probe,
        ProjectWriteAdmission project) : IAsyncDisposable {
        internal IServiceProvider Services => scope.ServiceProvider;
        internal CaptureProbe Probe => probe;
        internal ProjectWriteAdmission Project => project;
        internal ProjectDefinitionReader Definitions => Services.GetRequiredService<ProjectDefinitionReader>();
        internal ProjectTaskPlanBasisCapture Capture => new(Services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>(),
            Definitions, Services.GetRequiredService<ProjectWriteAdmissionService>(),
            Services.GetRequiredService<CoordinatedDatabaseTransaction>(), Services.GetRequiredService<DatasetActivationService>());

        internal static async Task<Fixture> CreateAsync() {
            var probe = new CaptureProbe();
            var application = await TestApplication.CreateAsync(new TestHarnessOptions {
                ConfigureServices = services => {
                    services.ConfigureDbContext<ProjectsDbContext>(options => options.AddInterceptors(probe), ServiceLifetime.Singleton);
                    services.ConfigureDbContext<WorkbenchDbContext>(options => options.AddInterceptors(probe), ServiceLifetime.Singleton);
                }
            });
            var scope = application.Services.CreateAsyncScope();
            try {
                var services = scope.ServiceProvider;
                await using var context = await services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync();
                var project = new Project { Name = "Basis project", Slug = Guid.NewGuid().ToString("N"), Objective = "Original objective" };
                context.Add(project);
                await context.SaveChangesAsync();
                var admission = await services.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(project.Id);
                return new(application, scope, probe, admission!);
            } catch {
                await scope.DisposeAsync();
                await application.DisposeAsync();
                throw;
            }
        }

        internal Task<ProjectTaskPlanBasisReadResult> CaptureAsync(CancellationToken cancellationToken = default)
            => Capture.CaptureAsync(Project, Allow, cancellationToken);

        internal Task<WorkbenchDbContext> WorkbenchAsync()
            => Services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync();

        internal Task LockProjectAsync(WorkbenchDbContext context)
            => SerializableMutationScope.AcquireRelationalScopeLocksAsync(context, [ProjectMutationScopeKeys.ForProject(Project.ProjectId)], default);

        internal async Task SeedTasksAsync(int count, string notes = "") {
            await using var context = await WorkbenchAsync();
            context.AddRange(Enumerable.Range(0, count).Select(index => TaskRow(Project.ProjectId, index, notes)));
            await context.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync() {
            await scope.DisposeAsync();
            await application.DisposeAsync();
        }
    }

    private sealed class InMemoryFactory : IDbContextFactory<WorkbenchDbContext> {
        public WorkbenchDbContext CreateDbContext() => new(new DbContextOptionsBuilder<WorkbenchDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    }
    private sealed class CaptureProbe : DbCommandInterceptor {
        private bool armed;
        private bool pauseScalar;
        internal int PayloadReads { get; private set; }
        internal int PreflightReads { get; private set; }
        internal ConcurrentQueue<(DbConnection? Connection, DbTransaction? Transaction)> Reads { get; } = new();
        internal TaskCompletionSource ProjectGateReached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource<int> CaptureBackend { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource ScalarRead { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource ReleaseScalar { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal void Arm(bool pauseScalar = false) {
            this.pauseScalar = pauseScalar;
            PayloadReads = 0;
            PreflightReads = 0;
            Reads.Clear();
            armed = true;
        }

        internal void Disarm() => armed = false;

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            if (armed && command.CommandText.StartsWith("SET LOCAL transaction_timeout", StringComparison.Ordinal)) {
                CaptureBackend.TrySetResult(((NpgsqlConnection)command.Connection!).ProcessID);
            }
            if (armed && command.CommandText.Contains("pg_advisory_xact_lock", StringComparison.Ordinal)) {
                ProjectGateReached.TrySetResult();
            }
            return ValueTask.FromResult(result);
        }

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default) {
            if (!armed) {
                return result;
            }
            if (command.CommandText.Contains("Projects_Projects", StringComparison.Ordinal) ||
                    command.CommandText.Contains("ProjectTaskPlanBasis.", StringComparison.Ordinal)) {
                Reads.Enqueue((command.Connection, command.Transaction));
            }
            if (command.CommandText.Contains("ProjectTaskPlanBasis.Payload", StringComparison.Ordinal)) {
                PayloadReads++;
            }
            if (command.CommandText.Contains("ProjectTaskPlanBasis.Preflight", StringComparison.Ordinal)) {
                PreflightReads++;
            }
            if (pauseScalar && command.CommandText.Contains("Projects_Projects", StringComparison.Ordinal) &&
                    !command.CommandText.Contains("octet_length", StringComparison.Ordinal)) {
                pauseScalar = false;
                ScalarRead.TrySetResult();
                await ReleaseScalar.Task.WaitAsync(cancellationToken);
            }
            return result;
        }
    }
}
