using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed class ProcessProjectAdmissionPersistenceTests {
    [Fact]
    public async Task Commit_returns_persisted_timestamp_precision_for_sequential_commands_and_still_rejects_stale_state() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var admission = await CreateProjectAsync(services);
        var initial = ProcessProjectAdmissionFixture.Initial(admission);
        var timestamp = ProcessProjectAdmissionFixture.Now.AddTicks(7);
        initial = initial with {
            OriginalState = initial.OriginalState with { UpdatedAtUtc = timestamp },
            Mutation = initial.Mutation with {
                State = initial.Mutation.State with { UpdatedAtUtc = timestamp },
                Events = initial.Mutation.Events.Select(item => item with { OccurredAtUtc = timestamp }).ToArray()
            }
        };
        var coordinator = Coordinator(services);
        await using var owner = Context(services);
        var unit = new EfProcessRuntimeUnitOfWork(owner, coordinatedTransaction: coordinator,
            projectAdmissionPolicy: Policy(services, coordinator));
        var committed = await unit.CommitAsync(initial);
        Assert.True(committed.Succeeded);
        await using var restarted = Context(services);
        var reader = new EfProcessRuntimeUnitOfWork(restarted);
        var persisted = Assert.IsType<ProcessRuntimeStateSnapshot>(await reader.LoadAsync(committed.State.RunId));
        Assert.Equal(ProcessProjectAdmissionFixture.Now, persisted.UpdatedAtUtc);
        Assert.Equal(persisted.UpdatedAtUtc, committed.State.UpdatedAtUtc);
        var originalToken = (await restarted.RuntimeStates.AsNoTracking().SingleAsync()).ConcurrencyToken;
        Assert.NotEqual(Guid.Empty, originalToken);

        var cancelled = await unit.CommitAsync(ProcessProjectAdmissionFixture.Cancel(committed.State));
        Assert.True(cancelled.Succeeded);
        Assert.Equal(ProcessRuntimeStatus.Cancelled, cancelled.State.Status);
        var stored = await restarted.RuntimeStates.AsNoTracking().SingleAsync();
        Assert.Equal(cancelled.State.UpdatedAtUtc, stored.UpdatedAtUtc);
        Assert.NotEqual(originalToken, stored.ConcurrencyToken);
        await Assert.ThrowsAsync<ProcessRuntimeOptimisticConcurrencyException>(() =>
            unit.CommitAsync(ProcessProjectAdmissionFixture.Cancel(committed.State)));
        Assert.Equal(2, await restarted.IdempotencyKeys.CountAsync());
        Assert.Equal(2, await restarted.RuntimeEvents.CountAsync());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Explicit_admission_without_both_policy_and_coordinator_fails_before_any_initial_write(bool withCoordinator, bool withPolicy) {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var admission = await CreateProjectAsync(services);
        var request = ProcessProjectAdmissionFixture.Initial(admission);
        var coordinator = Coordinator(services);
        await using var owner = Context(services);
        var unitOfWork = new EfProcessRuntimeUnitOfWork(owner,
            coordinatedTransaction: withCoordinator ? coordinator : null,
            projectAdmissionPolicy: withPolicy ? Policy(services, coordinator) : null);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => unitOfWork.CommitAsync(request));
        Assert.Contains("configured transaction coordination", error.Message, StringComparison.Ordinal);
        await AssertRowsAsync(owner, request, expected: 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Retired_project_tuple_is_rejected_before_plan_assignments_or_runtime_are_written(bool recreate) {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var admission = await CreateProjectAsync(services);
        var request = ProcessProjectAdmissionFixture.Initial(admission);
        var projects = services.GetRequiredService<ProjectsService>();
        await projects.DeleteAsync(admission.ProjectId);
        if (recreate) {
            Assert.True((await projects.CreateAsync(admission.ProjectId, new() { Name = "New lifetime" })).IsSuccess);
        }
        var coordinator = Coordinator(services);
        await using var owner = Context(services);
        var unitOfWork = new EfProcessRuntimeUnitOfWork(owner, coordinatedTransaction: coordinator, projectAdmissionPolicy: Policy(services, coordinator));
        var error = await Assert.ThrowsAsync<ProjectWriteAdmissionRejectedException>(() => unitOfWork.CommitAsync(request));
        Assert.Equal(admission.LifetimeId, error.Admission.LifetimeId);
        await AssertRowsAsync(owner, request, expected: 0);
        if (recreate) {
            var current = await services.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(admission.ProjectId);
            Assert.NotNull(current);
            Assert.NotEqual(admission.LifetimeId, current.LifetimeId);
        }
    }

    [Fact]
    public async Task Identical_project_and_lifetime_values_do_not_admit_a_different_database_profile() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var actual = await CreateProjectAsync(services);
        var request = ProcessProjectAdmissionFixture.Initial(new(Guid.NewGuid(), actual.ProjectId, actual.LifetimeId));
        var coordinator = Coordinator(services);
        await using var owner = Context(services);
        var unitOfWork = new EfProcessRuntimeUnitOfWork(owner, coordinatedTransaction: coordinator, projectAdmissionPolicy: Policy(services, coordinator));
        await Assert.ThrowsAsync<ProjectWriteAdmissionRejectedException>(() => unitOfWork.CommitAsync(request));
        await AssertRowsAsync(owner, request, expected: 0);
    }

    [Fact]
    public async Task Project_gate_and_exact_tuple_read_share_the_process_transaction_and_block_deletion_through_commit() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        await using var deletionScope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var admission = await CreateProjectAsync(services);
        var request = ProcessProjectAdmissionFixture.Initial(admission);
        var coordinator = Coordinator(services);
        var commands = new CommandProbe();
        var pause = new BeforeCommitPause();
        await using var owner = Context(services, commands, pause);
        var unitOfWork = new EfProcessRuntimeUnitOfWork(owner, coordinatedTransaction: coordinator,
            projectAdmissionPolicy: Policy(services, coordinator, commands));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var commit = unitOfWork.CommitAsync(request, timeout.Token);
        Task<ProjectDeletionResult>? deletion = null;
        try {
            var transaction = await pause.Entered.Task.WaitAsync(timeout.Token);
            await using var observer = Context(services);
            await AssertRowsAsync(observer, request, expected: 0);
            Assert.Contains(commands.Items, item => item.Sql.Contains("Projects_Projects", StringComparison.Ordinal));
            Assert.Contains(commands.Items, item => item.Sql.Contains("pg_advisory_xact_lock", StringComparison.Ordinal));
            Assert.All(commands.Items, item => {
                Assert.Same(owner.Database.GetDbConnection(), item.Connection);
                Assert.Same(transaction, item.Transaction);
                Assert.Equal(IsolationLevel.ReadCommitted, item.Isolation);
            });
            var deletionProbe = new LockWaitProbe();
            var deleteOptions = ProjectOptions(services, deletionProbe);
            var projects = ActivatorUtilities.CreateInstance<ProjectsService>(deletionScope.ServiceProvider,
                new ProjectFactory(deleteOptions));
            deletion = projects.DeleteAsync(admission.ProjectId, timeout.Token);
            var backendPid = await deletionProbe.BackendPid.Task.WaitAsync(timeout.Token);
            await WaitForAdvisoryWaitAsync(services, backendPid, timeout.Token);
            Assert.False(deletion.IsCompleted);
        } finally {
            pause.Release.TrySetResult(true);
        }
        Assert.True((await commit).Succeeded);
        Assert.NotNull(deletion);
        await deletion;
        await using var restarted = Context(services);
        var stored = Assert.IsType<ProcessRuntimeStateSnapshot>(await new EfProcessRuntimeUnitOfWork(restarted).LoadAsync(request.Mutation.State.RunId));
        Assert.Equal(admission, stored.ProjectAdmission);
        await AssertRowsAsync(restarted, request, expected: 1);
        Assert.Null(await services.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(admission.ProjectId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Policy(services, coordinator).RequireForMutationAsync(admission));
    }

    [Fact]
    public async Task Failure_after_actual_process_flush_rolls_back_every_initial_row_and_retry_reuses_the_prepared_identity() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var admission = await CreateProjectAsync(services);
        var request = ProcessProjectAdmissionFixture.Initial(admission);
        var failure = new ArgumentException("Injected after Process SQL flush.");
        var fault = new AfterFlushFault(failure);
        var coordinator = Coordinator(services);
        await using (var owner = Context(services, fault)) {
            var unitOfWork = new EfProcessRuntimeUnitOfWork(owner, coordinatedTransaction: coordinator, projectAdmissionPolicy: Policy(services, coordinator));
            Assert.Same(failure, await Assert.ThrowsAsync<ArgumentException>(() => unitOfWork.CommitAsync(request)));
            Assert.True(fault.Flushed);
        }
        await using var restarted = Context(services);
        await AssertRowsAsync(restarted, request, expected: 0);
        var retry = new EfProcessRuntimeUnitOfWork(restarted, coordinatedTransaction: coordinator, projectAdmissionPolicy: Policy(services, coordinator));
        Assert.True((await retry.CommitAsync(request)).Succeeded);
        Assert.Equal(admission, (await retry.LoadAsync(request.Mutation.State.RunId))!.ProjectAdmission);
        await AssertRowsAsync(restarted, request, expected: 1);
    }

    [Fact]
    public async Task Lost_commit_ack_preserves_exact_exception_and_receipt_after_restart_and_project_retirement() {
        await using var environment = CanDoItAllTestEnvironment.Create("process-project-admission-restart");
        var profile = environment.CreatePostgreSqlProfile("process-admission");
        var harness = new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = profile };
        ProcessRuntimeCommitRequest request;
        ProcessProjectAdmission admission;
        var failure = new ArgumentException("Injected committed Process acknowledgement loss.");
        await using (var application = await TestApplication.CreateAsync(harness)) {
            await using var scope = application.Services.CreateAsyncScope();
            var services = scope.ServiceProvider;
            admission = await CreateProjectAsync(services);
            request = ProcessProjectAdmissionFixture.Initial(admission);
            var coordinator = Coordinator(services);
            await using var owner = Context(services, new AfterCommitFault(failure));
            var unitOfWork = new EfProcessRuntimeUnitOfWork(owner, coordinatedTransaction: coordinator, projectAdmissionPolicy: Policy(services, coordinator));
            Assert.Same(failure, await Assert.ThrowsAsync<ArgumentException>(() => unitOfWork.CommitAsync(request)));
        }
        await using var restarted = await TestApplication.CreateAsync(harness);
        await using var restartedScope = restarted.Services.CreateAsyncScope();
        var currentServices = restartedScope.ServiceProvider;
        await currentServices.GetRequiredService<ProjectsService>().DeleteAsync(admission.ProjectId);
        await using var context = Context(currentServices);
        var reader = new EfProcessRuntimeUnitOfWork(context);
        var loaded = Assert.IsType<ProcessRuntimeStateSnapshot>(await reader.LoadAsync(request.Mutation.State.RunId));
        Assert.Equal(admission, loaded.ProjectAdmission);
        var replay = await reader.CommitAsync(request);
        Assert.Equal(request.Mutation.Outcome, replay.Outcome);
        Assert.Equal(request.Mutation.State.RunId, replay.State.RunId);
        await AssertRowsAsync(context, request, expected: 1);
        var changed = new ProcessProjectAdmission(admission.DatabaseProfileId, admission.ProjectId, Guid.NewGuid());
        await Assert.ThrowsAsync<InvalidOperationException>(() => reader.CommitAsync(ProcessProjectAdmissionFixture.WithAdmission(request, changed)));
        Assert.True((await reader.CommitAsync(ProcessProjectAdmissionFixture.Cancel(loaded))).Succeeded);
        Assert.Equal(admission, (await reader.LoadAsync(loaded.RunId))!.ProjectAdmission);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Child_cannot_drop_or_replace_the_persisted_parent_project_lifetime(bool replace) {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var admission = await CreateProjectAsync(services);
        var rootId = ProcessRunId.New();
        await using var owner = Context(services);
        owner.RuntimeStates.Add(new() {
            RunId = rootId.Value, RootRunId = rootId.Value, PlanId = Guid.NewGuid(), PlanHash = "sha256:parent",
            Status = ProcessRuntimeStatus.Active, UpdatedAtUtc = ProcessProjectAdmissionFixture.Now, ConcurrencyToken = Guid.NewGuid(),
            ProjectAdmissionDatabaseProfileId = admission.DatabaseProfileId, ProjectAdmissionProjectId = admission.ProjectId,
            ProjectAdmissionLifetimeId = admission.LifetimeId
        });
        await owner.SaveChangesAsync();
        var requested = replace ? new ProcessProjectAdmission(admission.DatabaseProfileId, admission.ProjectId, Guid.NewGuid()) : null;
        var request = ProcessProjectAdmissionFixture.Initial(requested);
        request = request with {
            OriginalState = request.OriginalState with { RootRunId = rootId },
            Mutation = request.Mutation with {
                State = request.Mutation.State with { RootRunId = rootId },
                Events = request.Mutation.Events.Select(item => item with { RootRunId = rootId }).ToArray()
            },
            ParentStepPrecondition = new(rootId, ProcessStepInstanceId.New())
        };
        var coordinator = Coordinator(services);
        var unitOfWork = new EfProcessRuntimeUnitOfWork(owner, coordinatedTransaction: coordinator, projectAdmissionPolicy: Policy(services, coordinator));
        var result = await unitOfWork.CommitAsync(request);
        Assert.Equal(ProcessRuntimeTransitionOutcome.Rejected, result.Outcome);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "Runtime.ParentProjectAdmissionMismatch");
        await AssertRowsAsync(owner, request, expected: 0);
    }

    [Fact]
    public async Task Legacy_run_with_a_project_launch_variable_remains_unadmitted_after_restart_and_can_be_cancelled() {
        await using var environment = CanDoItAllTestEnvironment.Create("legacy-process-project-admission");
        var profile = environment.CreatePostgreSqlProfile("legacy-process");
        var harness = new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = profile };
        ProcessRuntimeCommitRequest request;
        await using (var application = await TestApplication.CreateAsync(harness)) {
            await using var scope = application.Services.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var currentProject = await CreateProjectAsync(services);
            request = ProcessProjectAdmissionFixture.Initial();
            request = request with {
                InitialAssignments = request.InitialAssignments!.Select(assignment => assignment with {
                    LaunchVariables = new Dictionary<string, string> {
                        [ProcessRuntimeLaunchVariables.ProjectId] = currentProject.ProjectId.ToString("D")
                    }
                }).ToArray()
            };
            await using var owner = Context(services);
            Assert.True((await new EfProcessRuntimeUnitOfWork(owner).CommitAsync(request)).Succeeded);
        }
        await using var restarted = await TestApplication.CreateAsync(harness);
        await using var context = Context(restarted.Services);
        var unitOfWork = new EfProcessRuntimeUnitOfWork(context);
        var loaded = Assert.IsType<ProcessRuntimeStateSnapshot>(await unitOfWork.LoadAsync(request.Mutation.State.RunId));
        Assert.Null(loaded.ProjectAdmission);
        Assert.Equal(request.InitialPlan!.Header.PlanId, loaded.PlanId);
        Assert.Equal(request.InitialPlan.PlanHash, loaded.PlanHash);
        Assert.True((await unitOfWork.CommitAsync(ProcessProjectAdmissionFixture.Cancel(loaded))).Succeeded);
        var row = await context.RuntimeStates.SingleAsync(item => item.RunId == loaded.RunId.Value);
        Assert.Null(row.ProjectAdmissionDatabaseProfileId);
        Assert.Null(row.ProjectAdmissionProjectId);
        Assert.Null(row.ProjectAdmissionLifetimeId);
        Assert.Equal(ProcessRuntimeStatus.Cancelled, row.Status);
    }

    private static async Task<ProcessProjectAdmission> CreateProjectAsync(IServiceProvider services) {
        var projectId = Guid.NewGuid();
        Assert.True((await services.GetRequiredService<ProjectsService>().CreateAsync(projectId, new() { Name = "Process admission fixture" })).IsSuccess);
        var admission = Assert.IsType<ProjectWriteAdmission>(await services.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(projectId));
        return new(admission.DatabaseProfileId, admission.ProjectId, admission.LifetimeId);
    }

    private static CoordinatedDatabaseTransaction Coordinator(IServiceProvider services)
        => CoordinatedDatabaseTransaction.ForProfile(services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile);

    private static ProcessPersistenceDbContext Context(IServiceProvider services, params IInterceptor[] interceptors) {
        var options = new DbContextOptionsBuilder<ProcessPersistenceDbContext>();
        AppDbContextOptionsConfigurator.Configure(options, services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile);
        options.AddInterceptors(interceptors);
        return new(options.Options);
    }

    private static DbContextOptions<ProjectsDbContext> ProjectOptions(IServiceProvider services, params IInterceptor[] interceptors) {
        var options = new DbContextOptionsBuilder<ProjectsDbContext>();
        AppDbContextOptionsConfigurator.Configure(options, services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile);
        return options.AddInterceptors(interceptors).Options;
    }

    private static ProjectProcessAdmissionPolicy Policy(IServiceProvider services, CoordinatedDatabaseTransaction coordinator, params IInterceptor[] interceptors) {
        var options = ProjectOptions(services, interceptors);
        return new(new ProjectWriteAdmissionService(new ProjectFactory(options), options, coordinator, services.GetRequiredService<ICanonicalRuntimeDatabase>()));
    }

    private static async Task AssertRowsAsync(ProcessPersistenceDbContext context, ProcessRuntimeCommitRequest request, int expected) {
        var runId = request.Mutation.State.RunId.Value;
        var planId = request.InitialPlan!.Header.PlanId.Value;
        var eventId = request.Mutation.Events.Single().EventId.Value;
        Assert.Equal(expected, await context.RuntimeStates.CountAsync(item => item.RunId == runId));
        Assert.Equal(expected, await context.InstancePlans.CountAsync(item => item.PlanId == planId));
        Assert.Equal(expected, await context.RuntimeStepAssignments.CountAsync(item => item.RunId == runId));
        Assert.Equal(expected, await context.RuntimeEvents.CountAsync(item => item.RunId == runId));
        Assert.Equal(expected, await context.OutboxMessages.CountAsync(item => item.EventId == eventId));
        Assert.Equal(expected, await context.ArtifactLedgerEvents.CountAsync(item => item.EventId == eventId));
        Assert.Equal(expected, await context.IdempotencyKeys.CountAsync(item => item.RunId == runId));
    }

    private static async Task WaitForAdvisoryWaitAsync(IServiceProvider services, int backendPid, CancellationToken cancellationToken) {
        await using var connection = new NpgsqlConnection(services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        while (true) {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT EXISTS (SELECT 1 FROM pg_locks WHERE pid = @pid AND locktype = 'advisory' AND NOT granted)";
            command.Parameters.AddWithValue("pid", backendPid);
            if (await command.ExecuteScalarAsync(cancellationToken) is true) {
                return;
            }
            await Task.Delay(20, cancellationToken);
        }
    }

    private sealed class ProjectFactory(DbContextOptions<ProjectsDbContext> options) : IDbContextFactory<ProjectsDbContext> {
        public ProjectsDbContext CreateDbContext() => new(options);
    }

    private sealed record CommandObservation(string Sql, DbConnection Connection, DbTransaction Transaction, IsolationLevel Isolation);

    private sealed class CommandProbe : DbCommandInterceptor {
        public ConcurrentQueue<CommandObservation> Items { get; } = [];
        private void Record(DbCommand command) {
            if (command.Transaction is { } transaction) {
                Items.Enqueue(new(command.CommandText, command.Connection!, transaction, transaction.IsolationLevel));
            }
        }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            Record(command);
            return ValueTask.FromResult(result);
        }
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            Record(command);
            return ValueTask.FromResult(result);
        }
    }

    private sealed class LockWaitProbe : DbCommandInterceptor {
        public TaskCompletionSource<int> BackendPid { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            if (command.CommandText.Contains("pg_advisory_xact_lock", StringComparison.Ordinal)) {
                BackendPid.TrySetResult(((NpgsqlConnection)command.Connection!).ProcessID);
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class BeforeCommitPause : DbTransactionInterceptor {
        public TaskCompletionSource<DbTransaction> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData,
            InterceptionResult result, CancellationToken cancellationToken = default) {
            Entered.TrySetResult(transaction);
            await Release.Task.WaitAsync(cancellationToken);
            return result;
        }
    }

    private sealed class AfterFlushFault(Exception failure) : SaveChangesInterceptor {
        public bool Flushed { get; private set; }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default) {
            Flushed = result > 0 && eventData.Context!.Database.CurrentTransaction is not null;
            throw failure;
        }
    }

    private sealed class AfterCommitFault(Exception failure) : DbTransactionInterceptor {
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default) => throw failure;
    }
}
