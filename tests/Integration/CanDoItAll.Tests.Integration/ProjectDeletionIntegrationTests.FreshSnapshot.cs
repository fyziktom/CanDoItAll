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
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CanDoItAll.Tests.Integration.ProjectStructure;

public sealed partial class ProjectDeletionIntegrationTests {
    [Fact]
    public async Task Deletion_handoff_keeps_one_backend_and_only_transaction_exclusion_after_return() {
        await using var fixture = await DeletionEntryFixture.CreateAsync();
        await using var owner = fixture.Owner();
        await using var transaction = await fixture.EnterAsync(owner);
        Assert.Equal(IsolationLevel.Serializable, transaction.GetDbTransaction().IsolationLevel);
        Assert.Same(transaction, owner.Database.CurrentTransaction);
        Assert.Equal(ConnectionState.Open, owner.Database.GetDbConnection().State);
        var pid = ((NpgsqlConnection)owner.Database.GetDbConnection()).ProcessID;
        Assert.All(fixture.Probe.Backends, value => Assert.Equal(pid, value));
        Assert.Equal(new[] { IsolationLevel.ReadCommitted, IsolationLevel.Serializable }, fixture.Probe.Isolations);
        await fixture.AssertKeysAsync(available: false);
        await transaction.CommitAsync();
        Assert.Equal(ConnectionState.Closed, owner.Database.GetDbConnection().State);
        await fixture.AssertKeysAsync(available: true);
    }

    [Fact]
    public async Task Deletion_snapshot_reads_the_competing_owner_commit_after_its_observed_project_wait() {
        await using var fixture = await DeletionEntryFixture.CreateAsync();
        var projectId = Guid.NewGuid();
        var key = ProjectMutationScopeKeys.ForProject(projectId);
        await using var holder = fixture.Owner(observed: false);
        holder.Add(new Project { Id = projectId, Name = "Before wait" });
        await holder.SaveChangesAsync();
        await using var held = await holder.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await SerializableMutationScope.AcquireRelationalScopeLocksAsync(holder, [key], CancellationToken.None);
        await holder.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Projects_Projects\" SET \"Name\" = 'Committed during wait' WHERE \"Id\" = {projectId}");
        await using var owner = fixture.Owner();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var pending = ProjectDeletionTransactionEntry.BeginAsync(owner, fixture.Binding, [key], deadline.Token);
        try {
            var waiter = await fixture.Probe.SessionAttempt.Task.WaitAsync(deadline.Token);
            await fixture.WaitForBlockerAsync(waiter, ((NpgsqlConnection)holder.Database.GetDbConnection()).ProcessID, deadline.Token);
            Assert.False(pending.IsCompleted);
            await held.CommitAsync(deadline.Token);
            await using var admitted = await pending;
            Assert.Equal("Committed during wait", (await owner.Set<Project>().SingleAsync(row => row.Id == projectId, deadline.Token)).Name);
            await admitted.RollbackAsync(deadline.Token);
        } finally {
            deadline.Cancel();
            try {
                await held.DisposeAsync();
            } finally {
                await DrainDeletionAsync(pending);
            }
        }
    }

    [Fact]
    public async Task Deletion_waits_for_lifecycle_before_taking_any_project_session_key() {
        await using var fixture = await DeletionEntryFixture.CreateAsync();
        await using var holder = fixture.Owner(observed: false);
        await using var held = await holder.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await holder.Database.ExecuteSqlRawAsync("LOCK TABLE \"Infrastructure_DatasetLifecycle\" IN EXCLUSIVE MODE");
        await using var owner = fixture.Owner();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var pending = fixture.EnterAsync(owner, deadline.Token);
        try {
            var waiter = await fixture.Probe.FirstBackend.Task.WaitAsync(deadline.Token);
            await fixture.WaitForBlockerAsync(waiter, ((NpgsqlConnection)holder.Database.GetDbConnection()).ProcessID, deadline.Token);
            Assert.False(fixture.Probe.SessionAttempt.Task.IsCompleted);
            await fixture.AssertKeysAsync(available: true);
            await held.RollbackAsync(deadline.Token);
            await using var admitted = await pending;
            await admitted.RollbackAsync(deadline.Token);
        } finally {
            deadline.Cancel();
            try {
                await held.DisposeAsync();
            } finally {
                await DrainDeletionAsync(pending);
            }
        }
    }

    [Fact]
    public async Task Deletion_refuses_a_changed_activation_between_prelude_and_owner_snapshot() {
        await using var fixture = await DeletionEntryFixture.CreateAsync();
        fixture.Probe.AfterPrelude = async () => {
            await using var independent = fixture.Owner(observed: false);
            await independent.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Infrastructure_DatasetLifecycle\" SET \"MaintenanceId\" = {Guid.NewGuid()} WHERE \"Id\" = 1");
        };
        await using var owner = fixture.Owner();
        await Assert.ThrowsAsync<DatasetActivationMismatchException>(() => fixture.EnterAsync(owner));
        Assert.Equal(0, fixture.Probe.Commits);
        await fixture.AssertKeysAsync(available: true);
    }

    [Fact]
    public async Task Deletion_handoff_refuses_table_or_row_lifecycle_contention_without_waiting_under_session_keys() {
        await using var fixture = await DeletionEntryFixture.CreateAsync();
        foreach (var table in new[] { true, false }) {
            await using var blocker = fixture.Owner(observed: false);
            IDbContextTransaction? held = null;
            fixture.Probe.AfterPrelude = async () => {
                held = await blocker.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
                await blocker.Database.ExecuteSqlRawAsync(table
                    ? "LOCK TABLE \"Infrastructure_DatasetLifecycle\" IN EXCLUSIVE MODE"
                    : "SELECT \"Id\" FROM \"Infrastructure_DatasetLifecycle\" WHERE \"Id\" = 1 FOR UPDATE");
            };
            await using var owner = fixture.Owner();
            try {
                var failure = await Assert.ThrowsAnyAsync<Exception>(() => fixture.EnterAsync(owner));
                Assert.Equal(PostgresErrorCodes.LockNotAvailable, FindPostgres(failure)?.SqlState);
                Assert.NotNull(held);
                Assert.Same(held, blocker.Database.CurrentTransaction);
                await fixture.AssertKeysAsync(available: true);
            } finally {
                fixture.Probe.AfterPrelude = null;
                if (held is not null) {
                    await held.DisposeAsync();
                }
            }
        }
    }

    [Fact]
    public async Task Cancelled_partial_session_acquisition_releases_earlier_keys_before_context_disposal() {
        await using var fixture = await DeletionEntryFixture.CreateAsync();
        await using var blocker = fixture.Owner(observed: false);
        await using var held = await blocker.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await SerializableMutationScope.AcquireRelationalScopeLocksAsync(blocker, [fixture.Keys[^1]], CancellationToken.None);
        await using var owner = fixture.Owner();
        using var cancellation = new CancellationTokenSource();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var pending = fixture.EnterAsync(owner, cancellation.Token);
        try {
            var waiter = await fixture.Probe.SessionAttempt.Task.WaitAsync(deadline.Token);
            await fixture.WaitForBlockerAsync(waiter, ((NpgsqlConnection)blocker.Database.GetDbConnection()).ProcessID, deadline.Token);
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            Assert.Equal(ConnectionState.Closed, owner.Database.GetDbConnection().State);
            await held.RollbackAsync(deadline.Token);
            await fixture.AssertKeysAsync(available: true);
        } finally {
            cancellation.Cancel();
            try {
                await held.DisposeAsync();
            } finally {
                await DrainDeletionAsync(pending);
            }
        }
    }

    [Fact]
    public async Task Lost_session_acquisition_or_unlock_acknowledgement_cannot_leave_a_reusable_locked_backend() {
        await using var fixture = await DeletionEntryFixture.CreateAsync();
        foreach (var unlock in new[] { false, true }) {
            fixture.Probe.FailAfterSession = !unlock;
            fixture.Probe.FailAfterUnlock = unlock;
            await using var owner = fixture.Owner();
            await Assert.ThrowsAnyAsync<Exception>(() => fixture.EnterAsync(owner));
            Assert.Equal(ConnectionState.Closed, owner.Database.GetDbConnection().State);
            Assert.False(fixture.Probe.FailAfterSession);
            Assert.False(fixture.Probe.FailAfterUnlock);
            await fixture.AssertKeysAsync(available: true);
            if (unlock) {
                await fixture.AssertBackendAbsentAsync(fixture.Probe.StartedBackend);
            }
        }
    }

    [Fact]
    public async Task Actual_begin_and_prelude_end_faults_release_the_owned_session_without_entering_business_work() {
        await using var fixture = await DeletionEntryFixture.CreateAsync();
        foreach (var stage in new[] { DeletionEntryFault.FirstBegin, DeletionEntryFault.PreludeRollback, DeletionEntryFault.OwnerBegin }) {
            fixture.Probe.Fault = stage;
            fixture.Probe.Starts = 0;
            fixture.Probe.StartedBackend = 0;
            await using var owner = fixture.Owner();
            await Assert.ThrowsAnyAsync<Exception>(() => fixture.EnterAsync(owner));
            Assert.Equal(DeletionEntryFault.None, fixture.Probe.Fault);
            Assert.Equal(ConnectionState.Closed, owner.Database.GetDbConnection().State);
            Assert.Equal(0, fixture.Probe.Commits);
            await fixture.AssertKeysAsync(available: true);
            await fixture.AssertBackendAbsentAsync(fixture.Probe.StartedBackend);
        }
    }

    [Fact]
    public async Task Source_and_current_owner_checks_remain_after_exclusion_and_immediately_before_commit() {
        await using var fixture = await DeletionEntryFixture.CreateAsync();
        foreach (var changeOwner in new[] { false, true }) {
            await using var owner = fixture.Owner();
            var source = new DeletionSource();
            var current = true;
            await using var scope = await ProjectSourceMutationScope.BeginDeletionAsync(owner, fixture.Keys,
                ProjectMutationPurpose.ExistingWrite, Guid.NewGuid(), [], new(source, []),
                fixture.Services.GetRequiredService<ProjectWriteAdmissionService>(), fixture.Services.GetRequiredService<CoordinatedDatabaseTransaction>(),
                fixture.Binding, () => {
                    if (!current) {
                        throw new InvalidOperationException("The captured owner is no longer current.");
                    }
                }, CancellationToken.None);
            Assert.Equal(1, source.Checks);
            await fixture.AssertKeysAsync(available: false);
            var id = Guid.NewGuid();
            owner.Add(new Project { Id = id, Name = "Must roll back" });
            await owner.SaveChangesAsync();
            current = !changeOwner;
            source.Allow = changeOwner;
            await Assert.ThrowsAsync<InvalidOperationException>(() => scope.CommitAsync(CancellationToken.None));
            Assert.False(scope.CommitAttempted);
            Assert.False(scope.IsCommitted);
            await scope.DisposeAsync();
            Assert.Equal(1, source.Releases);
            await using var verify = fixture.Owner(observed: false);
            Assert.False(await verify.Set<Project>().AnyAsync(row => row.Id == id));
        }
    }

    [Fact]
    public async Task Real_deletion_commit_and_lost_acknowledgement_remain_distinct_when_owner_disposal_also_fails() {
        await using var application = await TestApplication.CreateAsync();
        await using var services = application.Services.CreateAsyncScope();
        var provider = services.ServiceProvider;
        foreach (var loseAck in new[] { false, true }) {
            var id = await CreateProjectAsync(provider.GetRequiredService<ProjectsService>(), "Deletion cleanup knowledge");
            var probe = new DeletionOwnerFault { LoseAcknowledgement = loseAck };
            var options = new DbContextOptionsBuilder<ProjectsDbContext>(provider.GetRequiredService<DbContextOptions<ProjectsDbContext>>())
                .AddInterceptors(new DeletionOwnerSave(probe), new DeletionOwnerCommit(probe), new DeletionOwnerDisposal(probe)).Options;
            var original = await ProjectLifecycleTestCommands.CaptureDeleteAsync(provider, id);
            var writer = ActivatorUtilities.CreateInstance<ProjectLifecycleWriter>(provider, new DeletionOwnerFactory(options));
            var operations = ActivatorUtilities.CreateInstance<CanDoItAll.Modules.Projects.Pages.ProjectNativeLifecycleOperations>(provider, writer);
            var outcome = await operations.DispatchAsync(original);
            Assert.Equal(1, probe.Commits);
            Assert.Equal(1, probe.Disposals);
            if (loseAck) {
                var unconfirmed = Assert.IsType<ProjectLifecycleOutcome.Unconfirmed>(outcome);
                Assert.Equal(ProjectLifecycleRecoveryReason.OwnerReleaseUnconfirmed, unconfirmed.Reason);
                Assert.Equal(original, unconfirmed.Original);
            } else {
                var committed = Assert.IsType<ProjectLifecycleOutcome.Accepted>(outcome);
                Assert.Equal(ProjectLifecycleCompletionIssue.OwnerReleaseUnconfirmed, committed.CompletionIssue);
                var completion = Assert.IsType<ProjectLifecycleAcknowledgement.DeletionCommitted>(committed.Result.Acknowledgement).Completion;
                Assert.Equal(id, completion.Original.Project.ProjectId);
                var pending = await provider.GetRequiredService<ProjectsService>().ListPendingDeletionCleanupsAsync();
                Assert.NotEmpty(completion.Obligations);
                Assert.All(completion.Obligations, item => Assert.Contains(pending,
                    row => row.ProjectId == id && row.ParticipantId == item.Participant && row.RecoveryId == item.RecoveryId));
            }
            await using var verify = await provider.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync();
            Assert.False(await verify.Set<Project>().AnyAsync(row => row.Id == id));
            Assert.True(await verify.Set<ProjectRetirementRecord>().AnyAsync(row => row.ProjectId == id));
            await using var search = await provider.GetRequiredService<IDbContextFactory<CanDoItAll.Infrastructure.Search.SearchDbContext>>().CreateDbContextAsync();
            Assert.False(await search.Set<CanDoItAll.Infrastructure.Search.SearchDocument>().AnyAsync(row => row.ProjectId == id));
        }
        var source = new DeletionSource();
        var ordinary = provider.GetRequiredService<ProjectsService>();
        var created = await ordinary.CreateWithReceiptAsync(Guid.NewGuid(), new() { Name = "Compensation release ownership" },
            authorization: new(source, []));
        Assert.True(created.IsSuccess);
        var receipt = created.Value!;
        var releases = source.Releases;
        source.HoldRelease = true;
        var compensation = ordinary.CompensateCreationAsync(receipt);
        try {
            await source.ReleaseStarted.Task.WaitAsync(TimeSpan.FromSeconds(15));
            Assert.False(compensation.IsCompleted);
            await using var verify = await provider.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync();
            Assert.False(await verify.Set<Project>().AnyAsync(row => row.Id == receipt.Project.ProjectId));
            source.ReleaseAllowed.TrySetResult();
            var committed = Assert.IsType<ProjectCreationCompensationOutcome.Committed>(await compensation);
            Assert.Equal(ProjectLifecycleCompletionIssue.OwnerReleaseUnconfirmed, committed.CompletionIssue);
            Assert.Equal(receipt.Project, committed.Completion.Original.Project);
            Assert.Equal(releases + 1, source.Releases);
            Assert.NotEmpty(committed.Completion.Obligations);
            var pending = await ordinary.ListPendingDeletionCleanupsAsync();
            Assert.All(committed.Completion.Obligations, item => Assert.Contains(pending,
                row => row.ProjectId == receipt.Project.ProjectId && row.ParticipantId == item.Participant && row.RecoveryId == item.RecoveryId));
        } finally {
            source.ReleaseAllowed.TrySetResult();
            await DrainDeletionAsync(compensation);
        }
    }

    private static async Task DrainDeletionAsync(Task task) {
        try {
            await task;
        } catch when (task.IsCompleted) {
        }
    }

    private static PostgresException? FindPostgres(Exception failure) => failure switch {
        PostgresException postgres => postgres,
        AggregateException aggregate => aggregate.InnerExceptions.Select(FindPostgres).FirstOrDefault(value => value is not null),
        { InnerException: { } inner } => FindPostgres(inner),
        _ => null
    };

    private sealed class DeletionEntryFixture(TestApplication application, AsyncServiceScope scope,
        DatasetActivationBinding binding) : IAsyncDisposable {
        public IServiceProvider Services => scope.ServiceProvider;
        public DatasetActivationBinding Binding => binding;
        public DeletionEntryProbe Probe { get; } = new();
        public string[] Keys { get; } = new[] { ProjectMutationScopeKeys.ForProject(Guid.NewGuid()), ProjectMutationScopeKeys.Hierarchy }
            .Order(StringComparer.Ordinal).ToArray();
        public static async Task<DeletionEntryFixture> CreateAsync() {
            var application = await TestApplication.CreateAsync();
            var scope = application.Services.CreateAsyncScope();
            try {
                var profile = scope.ServiceProvider.GetRequiredService<ICanonicalRuntimeDatabase>().Profile;
                var binding = await scope.ServiceProvider.GetRequiredService<DatasetActivationService>().ReadActiveBindingAsync(profile.Profile.Id);
                return new(application, scope, binding);
            } catch (Exception failure) {
                var failures = new List<Exception> { failure };
                try {
                    try {
                        await scope.DisposeAsync();
                    } catch (Exception cleanupFailure) {
                        failures.Add(cleanupFailure);
                    }
                } finally {
                    try {
                        await application.DisposeAsync();
                    } catch (Exception cleanupFailure) {
                        failures.Add(cleanupFailure);
                    }
                }
                if (failures.Count > 1) {
                    throw new AggregateException("Deletion fixture admission and cleanup did not both finish.", failures);
                }
                throw;
            }
        }
        public ProjectsDbContext Owner(bool observed = true) {
            var options = new DbContextOptionsBuilder<ProjectsDbContext>(Services.GetRequiredService<DbContextOptions<ProjectsDbContext>>());
            if (observed) {
                options.AddInterceptors(new DeletionEntryCommands(Probe), new DeletionEntryTransactions(Probe));
            }
            return new(options.Options);
        }
        public Task<IDbContextTransaction> EnterAsync(ProjectsDbContext owner, CancellationToken token = default)
            => ProjectDeletionTransactionEntry.BeginAsync(owner, binding, Keys, token);
        public async Task AssertKeysAsync(bool available) {
            await using var observer = Owner(observed: false);
            await using var transaction = await observer.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            foreach (var key in Keys) {
                Assert.Equal(available, await observer.Database.SqlQuery<bool>($"SELECT pg_try_advisory_xact_lock(hashtextextended({key}, 0)) AS \"Value\"").SingleAsync());
            }
        }
        public async Task WaitForBlockerAsync(int waiter, int holder, CancellationToken token) {
            await using var observer = Owner(observed: false);
            while (!await observer.Database.SqlQuery<bool>($"SELECT {holder} = ANY(pg_blocking_pids({waiter})) AS \"Value\"").SingleAsync(token)) {
                await Task.Delay(10, token);
            }
        }
        public async Task AssertBackendAbsentAsync(int backend) {
            Assert.InRange(backend, 1, int.MaxValue);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await using var observer = Owner(observed: false);
            while (await observer.Database.SqlQuery<bool>($"SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE pid = {backend}) AS \"Value\"")
                .SingleAsync(deadline.Token)) {
                await Task.Delay(10, deadline.Token);
            }
        }
        public async ValueTask DisposeAsync() {
            try {
                await scope.DisposeAsync();
            } finally {
                await application.DisposeAsync();
            }
        }
    }

    private enum DeletionEntryFault { None, FirstBegin, PreludeRollback, OwnerBegin }
    private sealed class DeletionEntryProbe {
        public ConcurrentQueue<int> Backends { get; } = new();
        public ConcurrentQueue<IsolationLevel> Isolations { get; } = new();
        public TaskCompletionSource<int> FirstBackend { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<int> SessionAttempt { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Func<Task>? AfterPrelude { get; set; }
        public bool FailAfterSession { get; set; }
        public bool FailAfterUnlock { get; set; }
        public DeletionEntryFault Fault { get; set; }
        public int Starts { get; set; }
        public int StartedBackend { get; set; }
        public int Commits { get; set; }
        public HashSet<Guid> PreludeTransactions { get; } = [];
    }
    private sealed class DeletionEntryCommands(DeletionEntryProbe probe) : DbCommandInterceptor {
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            var pid = ((NpgsqlConnection)command.Connection!).ProcessID;
            probe.Backends.Enqueue(pid);
            probe.FirstBackend.TrySetResult(pid);
            if (command.CommandText.Contains("pg_advisory_lock(", StringComparison.Ordinal)) {
                probe.SessionAttempt.TrySetResult(pid);
            }
            return ValueTask.FromResult(result);
        }
        public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            int result, CancellationToken cancellationToken = default) {
            if (probe.FailAfterSession && command.CommandText.Contains("pg_advisory_lock(", StringComparison.Ordinal)) {
                probe.FailAfterSession = false;
                throw new IOException("Lost acknowledgement after actual session lock acquisition.");
            }
            return ValueTask.FromResult(result);
        }
        public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default) {
            if (probe.FailAfterUnlock && command.CommandText.Contains("pg_advisory_unlock(", StringComparison.Ordinal)) {
                probe.FailAfterUnlock = false;
                throw new IOException("Lost acknowledgement after actual session unlock.");
            }
            return ValueTask.FromResult(result);
        }
    }
    private sealed class DeletionEntryTransactions(DeletionEntryProbe probe) : DbTransactionInterceptor {
        public override ValueTask<DbTransaction> TransactionStartedAsync(DbConnection connection, TransactionEndEventData eventData,
            DbTransaction result, CancellationToken cancellationToken = default) {
            probe.Starts++;
            probe.StartedBackend = ((NpgsqlConnection)connection).ProcessID;
            probe.Backends.Enqueue(probe.StartedBackend);
            probe.Isolations.Enqueue(result.IsolationLevel);
            if (result.IsolationLevel == IsolationLevel.ReadCommitted) {
                probe.PreludeTransactions.Add(eventData.TransactionId);
            }
            if (probe.Fault == DeletionEntryFault.FirstBegin && probe.Starts == 1 ||
                probe.Fault == DeletionEntryFault.OwnerBegin && probe.Starts == 2) {
                probe.Fault = DeletionEntryFault.None;
                throw new IOException("Failure after actual transaction begin.");
            }
            return ValueTask.FromResult(result);
        }
        public override async Task TransactionRolledBackAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default) {
            if (!probe.PreludeTransactions.Contains(eventData.TransactionId)) {
                return;
            }
            if (probe.Fault == DeletionEntryFault.PreludeRollback) {
                probe.Fault = DeletionEntryFault.None;
                throw new IOException("Failure after actual prelude rollback.");
            }
            var callback = probe.AfterPrelude;
            probe.AfterPrelude = null;
            if (callback is not null) {
                await callback();
            }
        }
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default) {
            probe.Commits++;
            return Task.CompletedTask;
        }
    }
    private sealed class DeletionSource : IProjectMutationSourceAuthority, IProjectMutationSourceLease {
        public bool Allow { get; set; } = true;
        public int Checks { get; private set; }
        public int Releases { get; private set; }
        public bool HoldRelease { get; set; }
        public TaskCompletionSource ReleaseStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseAllowed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IReadOnlyCollection<ProjectWriteAdmission> SourceProjects => [];
        public ProjectDeletionSourceRecovery CaptureDeletionRecoverySource() => new ProjectDeletionSourceRecovery.NoExternalSourceLease();
        public Task<IProjectMutationSourceLease> AcquireAsync(ProjectMutationTarget target, CancellationToken cancellationToken = default)
            => Task.FromResult<IProjectMutationSourceLease>(this);
        public Task RequireForMutationAsync(CancellationToken cancellationToken = default) {
            Checks++;
            if (!Allow) {
                throw new InvalidOperationException("The source no longer permits this mutation.");
            }
            return Task.CompletedTask;
        }
        public Task RequireForDeletionCommitAsync(ProjectDeletionCommitTarget target, CancellationToken cancellationToken = default)
            => RequireForMutationAsync(cancellationToken);
        public async ValueTask DisposeAsync() {
            Releases++;
            if (HoldRelease) {
                ReleaseStarted.TrySetResult();
                await ReleaseAllowed.Task;
                throw new IOException("The owned source release failed after acknowledged deletion.");
            }
        }
    }
    private sealed class DeletionOwnerFault {
        public bool LoseAcknowledgement { get; init; }
        public ConcurrentDictionary<Guid, byte> Saved { get; } = new();
        public int Commits;
        public int Disposals;
    }
    private sealed class DeletionOwnerFactory(DbContextOptions<ProjectsDbContext> options) : IDbContextFactory<ProjectsDbContext> {
        public ProjectsDbContext CreateDbContext() => new(options);
        public Task<ProjectsDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
    }
    private sealed class DeletionOwnerSave(DeletionOwnerFault probe) : SaveChangesInterceptor {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            probe.Saved.TryAdd(eventData.Context!.ContextId.InstanceId, 0);
            return ValueTask.FromResult(result);
        }
    }
    private sealed class DeletionOwnerCommit(DeletionOwnerFault probe) : DbTransactionInterceptor {
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default) {
            if (probe.Saved.ContainsKey(eventData.Context!.ContextId.InstanceId)) {
                Interlocked.Increment(ref probe.Commits);
                if (probe.LoseAcknowledgement) {
                    throw new IOException("Lost deletion acknowledgement after actual commit.");
                }
            }
            return Task.CompletedTask;
        }
    }
    private sealed class DeletionOwnerDisposal(DeletionOwnerFault probe) : DbConnectionInterceptor {
        public override Task ConnectionDisposedAsync(DbConnection connection, ConnectionEndEventData eventData) {
            if (probe.Saved.TryRemove(eventData.Context!.ContextId.InstanceId, out _)) {
                Interlocked.Increment(ref probe.Disposals);
                throw new DbUpdateConcurrencyException("Disposal cannot establish rollback after a commit attempt.");
            }
            return Task.CompletedTask;
        }
    }
}
