using System.Data;
using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectOperationEpochTests {
    private const long OwnerWaitKey = 863349102614;
    private static readonly ProjectMutationCallerScope Caller = ProjectMutationCallerScope.LocalWorkspace;

    [Fact]
    public async Task Initialization_is_idempotent_and_existing_replay_promise_survives_tighter_options() {
        await using var fixture = await Fixture.CreateAsync();
        var original = await fixture.Service.ReadCurrentAsync(fixture.Binding, fixture.Pool, CancellationToken.None);
        var tighter = ProjectFiniteOperationPool.Create(new() {
            AdmissionWindow = TimeSpan.FromHours(1),
            ReplayRetention = TimeSpan.FromHours(1),
            MutationDeadline = TimeSpan.FromSeconds(1),
            StatementDeadline = TimeSpan.FromSeconds(1),
            LockDeadline = TimeSpan.FromSeconds(1)
        });
        await fixture.InitializeAsync(fixture.Service, tighter);

        var current = await fixture.Service.ReadCurrentAsync(fixture.Binding, tighter, CancellationToken.None);

        Assert.Equal(original.Binding.Namespace, current.Binding.Namespace);
        Assert.Equal(original.Epoch, current.Epoch);
        Assert.Equal(original.AdmissionClosesAtUtc, current.AdmissionClosesAtUtc);
        Assert.Equal(original.ReplayUntilUtc, current.ReplayUntilUtc);
        await using var read = fixture.Context();
        Assert.Equal(1, await read.Set<FiniteOperationEpochRecord>().CountAsync());
        Assert.Equal(1, await read.Set<FiniteOperationStateRecord>().CountAsync());
    }

    [Fact]
    public async Task Expired_arrival_rejects_before_a_blocked_lifecycle_guard() {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.SetDeadlineAsync(TimeSpan.FromSeconds(-1));
        await using var held = fixture.Context();
        await using var holding = await held.Database.BeginTransactionAsync();
        await DatasetLifecycleStore.RequireActiveForMaintenanceAsync(held, fixture.Binding);
        await using var arriving = fixture.Context();
        await using var transaction = await arriving.Database.BeginTransactionAsync();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));

        var rejected = await Assert.ThrowsAsync<FiniteOperationAdmissionException>(() =>
            fixture.Service.EnterAsync(arriving, fixture.Binding, fixture.Pool, fixture.Operation().Epoch.Value, deadline.Token));

        Assert.Equal(FiniteOperationAdmissionFailure.AdmissionDeadlineElapsed, rejected.Failure);
        Assert.NotNull(held.Database.CurrentTransaction);
    }

    [Fact]
    public async Task Open_epoch_maintenance_skips_lifecycle_row_guards_while_purging_only_retired_rows() {
        await using var fixture = await Fixture.CreateAsync();
        var now = await fixture.NowAsync();
        await fixture.SeedHistoryAsync([
            Closed(fixture, 1, now.AddDays(-3), now.AddDays(-3).AddSeconds(1), 60)
        ], retiredThrough: 1);
        await fixture.SetDeadlineAsync(TimeSpan.FromHours(1));
        await fixture.SeedReceiptsAsync(epoch: 1, count: 1, payloadBytes: 1);
        await using var held = fixture.Context();
        await using var holding = await held.Database.BeginTransactionAsync();
        await DatasetLifecycleStore.RequireActiveForMaintenanceAsync(held, fixture.Binding);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));

        Assert.Equal(FiniteOperationRotationResult.Unchanged, await fixture.Service.RotateExpiredAsync(fixture.Binding, fixture.Pool, deadline.Token));
        Assert.Equal(1, (await fixture.PurgeAsync(fixture.Binding, deadline.Token)).DeletedReceipts);
        Assert.Equal(1, await fixture.RetiredThroughAsync());
    }

    [Fact]
    public async Task Held_epoch_share_prevents_rotation_until_the_owner_releases_after_server_deadline() {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.SetDeadlineAsync(TimeSpan.FromSeconds(2));
        await using var owner = fixture.Context();
        await using var transaction = await owner.Database.BeginTransactionAsync();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var admitted = await fixture.Service.EnterAsync(owner, fixture.Binding, fixture.Pool, fixture.Operation().Epoch.Value, deadline.Token);
        await fixture.WaitForDeadlineAsync(admitted.AdmissionClosesAtUtc, deadline.Token);

        Assert.Equal(FiniteOperationRotationResult.Busy, await fixture.Service.RotateExpiredAsync(fixture.Binding, fixture.Pool, deadline.Token));
        await using (var read = fixture.Context()) {
            Assert.Equal(FiniteOperationEpochState.Open, (await read.Set<FiniteOperationEpochRecord>().SingleAsync()).State);
        }
        await transaction.RollbackAsync(deadline.Token);

        Assert.Equal(FiniteOperationRotationResult.Rotated, await fixture.Service.RotateExpiredAsync(fixture.Binding, fixture.Pool, deadline.Token));
        await using var after = fixture.Context();
        var epochs = await after.Set<FiniteOperationEpochRecord>().OrderBy(epoch => epoch.Epoch).ToArrayAsync();
        Assert.Equal(FiniteOperationEpochState.Closed, epochs[0].State);
        Assert.Equal(FiniteOperationEpochState.Open, epochs[1].State);
        Assert.Equal(2, (await after.Set<FiniteOperationStateRecord>().SingleAsync()).CurrentEpoch);
    }

    [Fact]
    public async Task Authoritative_repeat_check_rejects_an_owner_that_waited_past_admission_deadline() {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.SetDeadlineAsync(TimeSpan.FromSeconds(2));
        await using var blocker = fixture.Context();
        await using var blocking = await blocker.Database.BeginTransactionAsync();
        await blocker.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({OwnerWaitKey})");
        await using var owner = fixture.Context();
        await using var transaction = await owner.Database.BeginTransactionAsync();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var admitted = await fixture.Service.EnterAsync(owner, fixture.Binding, fixture.Pool, fixture.Operation().Epoch.Value, deadline.Token);
        var pid = await owner.Database.SqlQuery<int>($"SELECT pg_backend_pid() AS \"Value\"").SingleAsync(deadline.Token);
        var waiting = WaitForOwnerKeyAsync();
        Exception? failure = null;
        try {
            await fixture.WaitForBlockedBackendAsync(pid, deadline.Token);
            await fixture.WaitForDeadlineAsync(admitted.AdmissionClosesAtUtc, deadline.Token);
        } finally {
            await blocking.RollbackAsync(CancellationToken.None);
            failure = await Record.ExceptionAsync(() => waiting);
        }

        var rejected = Assert.IsType<FiniteOperationAdmissionException>(failure);
        Assert.Equal(FiniteOperationAdmissionFailure.AdmissionDeadlineElapsed, rejected.Failure);

        async Task WaitForOwnerKeyAsync() {
            await owner.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({OwnerWaitKey})", deadline.Token);
            await fixture.Service.RequireAdmissibleAsync(owner, admitted, deadline.Token);
        }
    }

    [Fact]
    public async Task Rotation_failure_after_closing_flush_rolls_back_closure_watermark_and_successor() {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.SetDeadlineAsync(TimeSpan.FromSeconds(-1));
        var failure = new RotationFailure();
        var service = fixture.CreateService(interceptor: failure);

        await Assert.ThrowsAsync<RotationInterrupted>(() => service.RotateExpiredAsync(fixture.Binding, fixture.Pool, CancellationToken.None));

        Assert.True(failure.ClosedEpochWasFlushed);
        await using var read = fixture.Context();
        var epoch = await read.Set<FiniteOperationEpochRecord>().SingleAsync();
        var state = await read.Set<FiniteOperationStateRecord>().SingleAsync();
        Assert.Equal(FiniteOperationEpochState.Open, epoch.State);
        Assert.Null(epoch.ClosedAtUtc);
        Assert.Equal(1, state.CurrentEpoch);
        Assert.Equal(0, state.RetiredThroughEpoch);
        Assert.Equal(FiniteOperationRotationResult.Rotated, await fixture.Service.RotateExpiredAsync(fixture.Binding, fixture.Pool, CancellationToken.None));
    }

    [Fact]
    public async Task Retirement_is_contiguous_and_observes_both_promised_and_actual_closed_retention() {
        await using var fixture = await Fixture.CreateAsync();
        var now = await fixture.NowAsync();
        await fixture.SeedHistoryAsync([
            Closed(fixture, 1, now.AddDays(-3), now.AddDays(-3).AddSeconds(1), 60),
            Closed(fixture, 2, now.AddDays(-1).AddSeconds(-10), now.AddDays(-1).AddSeconds(-5), 86400, 60000),
            Closed(fixture, 3, now.AddDays(-3), now.AddDays(-3).AddSeconds(1), 60)
        ], retiredThrough: 0);

        await fixture.Service.AdvanceRetirementAsync(fixture.Binding, fixture.Pool, CancellationToken.None);
        Assert.Equal(1, await fixture.RetiredThroughAsync());
        await using (var edit = fixture.Context()) {
            var blocked = await edit.Set<FiniteOperationEpochRecord>().SingleAsync(epoch => epoch.Epoch == 2);
            blocked.AdmissionClosesAtUtc = now.AddDays(-2);
            blocked.OpenedAtUtc = now.AddDays(-2).AddHours(-1);
            blocked.ClosedAtUtc = now.AddSeconds(-1);
            await edit.SaveChangesAsync();
        }
        await fixture.SetDeadlineAsync(TimeSpan.FromHours(1));
        await fixture.Service.AdvanceRetirementAsync(fixture.Binding, fixture.Pool, CancellationToken.None);
        Assert.Equal(1, await fixture.RetiredThroughAsync());

        await using (var edit = fixture.Context()) {
            var blocked = await edit.Set<FiniteOperationEpochRecord>().SingleAsync(epoch => epoch.Epoch == 2);
            blocked.ClosedAtUtc = now.AddDays(-2).AddSeconds(1);
            await edit.SaveChangesAsync();
        }
        await fixture.SetDeadlineAsync(TimeSpan.FromHours(1));
        await fixture.Service.AdvanceRetirementAsync(fixture.Binding, fixture.Pool, CancellationToken.None);
        Assert.Equal(3, await fixture.RetiredThroughAsync());
    }

    [Theory]
    [InlineData(PurgeBoundary.RowCount)]
    [InlineData(PurgeBoundary.PayloadBytes)]
    public async Task Purge_respects_both_bounds_and_never_deletes_unretired_evidence(PurgeBoundary boundary) {
        await using var fixture = await Fixture.CreateAsync();
        var now = await fixture.NowAsync();
        await fixture.SeedHistoryAsync([
            Closed(fixture, 1, now.AddDays(-3), now.AddDays(-3).AddSeconds(1), 60),
            Closed(fixture, 2, now.AddDays(-3), now.AddDays(-3).AddSeconds(1), 60)
        ], retiredThrough: 1);
        var payloadBytes = boundary == PurgeBoundary.RowCount ? 1 : ProjectDefinitionReceiptRecord.MaximumPayloadBytes;
        var expected = boundary == PurgeBoundary.RowCount ? ProjectDefinitionOperationOptions.PurgeBatchCount
            : ProjectDefinitionOperationOptions.PurgeBatchBytes / payloadBytes;
        await fixture.SeedReceiptsAsync(epoch: 1, expected + 2, payloadBytes);
        await fixture.SeedReceiptsAsync(epoch: 2, count: 1, payloadBytes: 1);
        Assert.Equal(1, await fixture.RetiredThroughAsync());

        Assert.Equal(expected, (await fixture.PurgeAsync(fixture.Binding)).DeletedReceipts);

        await using (var read = fixture.Context()) {
            Assert.Equal(2, await read.Set<ProjectDefinitionReceiptRecord>().CountAsync(receipt => receipt.Epoch == 1));
            Assert.Equal(1, await read.Set<ProjectDefinitionReceiptRecord>().CountAsync(receipt => receipt.Epoch == 2));
            Assert.True(await read.Set<FiniteOperationEpochRecord>().AnyAsync(epoch => epoch.Epoch == 1));
        }
        Assert.Equal(1, await fixture.RetiredThroughAsync());
        Assert.Equal(2, (await fixture.PurgeAsync(fixture.Binding)).DeletedReceipts);
        await fixture.CleanupMetadataAsync();
        await using var after = fixture.Context();
        Assert.False(await after.Set<FiniteOperationEpochRecord>().AnyAsync(epoch => epoch.Epoch == 1));
        Assert.True(await after.Set<FiniteOperationEpochRecord>().AnyAsync(epoch => epoch.Epoch == 2));
        Assert.Equal(2, (await after.Set<ProjectDefinitionReceiptRecord>().SingleAsync()).Epoch);
        Assert.Equal(1, await fixture.RetiredThroughAsync());
    }

    [Fact]
    public async Task Reactivation_closure_and_new_epoch_share_the_SQL_activation_commit_and_preserve_old_retention() {
        await using var fixture = await Fixture.CreateAsync();
        var replacement = new DatasetActivationBinding(new DatasetNamespaceId(Guid.NewGuid()), Guid.NewGuid(),
            DatasetLifecycleRecord.CurrentSchemaVersion, DatasetGuardProtocol.SharedRows);
        await fixture.SeedReceiptsAsync(epoch: 1, count: 1, payloadBytes: 1);

        await fixture.ReactivateAsync(replacement, commit: false);

        Assert.Equal(fixture.Binding, (await fixture.LifecycleStore.ReadAsync())!.Binding);
        await using (var rolledBack = fixture.Context()) {
            Assert.Equal(FiniteOperationEpochState.Open, (await rolledBack.Set<FiniteOperationEpochRecord>().SingleAsync()).State);
            Assert.Single(await rolledBack.Set<FiniteOperationStateRecord>().ToArrayAsync());
        }
        await fixture.ReactivateAsync(replacement, commit: true);
        await fixture.ReactivateAsync(replacement, commit: true);
        Assert.Equal(replacement, (await fixture.LifecycleStore.ReadAsync())!.Binding);
        var metadata = await fixture.Service.ReadCurrentAsync(replacement, fixture.Pool, CancellationToken.None);
        Assert.Equal(replacement.Namespace, metadata.Binding.Namespace);
        Assert.Equal(1, metadata.Epoch);
        Assert.Equal(0, (await fixture.PurgeAsync(replacement)).DeletedReceipts);
        await using var read = fixture.Context();
        var original = await read.Set<FiniteOperationEpochRecord>().SingleAsync(epoch => epoch.NamespaceId == fixture.Binding.Namespace.Value);
        Assert.Equal(FiniteOperationEpochState.Closed, original.State);
        Assert.NotNull(original.ClosedAtUtc);
        Assert.Single(await read.Set<ProjectDefinitionReceiptRecord>().ToArrayAsync());
    }

    [Fact]
    public async Task Inactive_namespace_cleanup_observes_both_horizons_and_removes_state_only_after_all_evidence() {
        await using var fixture = await Fixture.CreateAsync();
        var now = await fixture.NowAsync();
        await fixture.SeedHistoryAsync([
            Closed(fixture, 1, now.AddDays(-3), now.AddDays(-3).AddSeconds(1), 60),
            Closed(fixture, 2, now.AddDays(-1).AddSeconds(-10), now.AddDays(-1).AddSeconds(-5), 86400, 60000)
        ], retiredThrough: 0);
        await fixture.SeedReceiptsAsync(epoch: 1, count: 1, payloadBytes: 1);
        await fixture.SeedReceiptsAsync(epoch: 2, count: 1, payloadBytes: 1);
        var replacement = new DatasetActivationBinding(new DatasetNamespaceId(Guid.NewGuid()), Guid.NewGuid(),
            DatasetLifecycleRecord.CurrentSchemaVersion, DatasetGuardProtocol.SharedRows);
        await fixture.ReactivateAsync(replacement, commit: true);

        await Assert.ThrowsAsync<DatasetActivationMismatchException>(() => fixture.PurgeAsync(fixture.Binding));
        Assert.Equal(1, (await fixture.PurgeAsync(replacement)).DeletedReceipts);
        await using (var read = fixture.Context()) {
            Assert.Equal(2, (await read.Set<ProjectDefinitionReceiptRecord>().SingleAsync()).Epoch);
            Assert.Equal(2, await read.Set<FiniteOperationStateRecord>().CountAsync());
        }
        await using (var edit = fixture.Context()) {
            var retained = await edit.Set<FiniteOperationEpochRecord>()
                .Where(epoch => epoch.NamespaceId == fixture.Binding.Namespace.Value).ToArrayAsync();
            foreach (var epoch in retained) {
                epoch.OpenedAtUtc = now.AddDays(-4);
                epoch.AdmissionClosesAtUtc = now.AddDays(-3);
                epoch.ClosedAtUtc = now.AddSeconds(-1);
                epoch.ReplayRetentionSeconds = 86400;
            }
            await edit.SaveChangesAsync();
        }
        Assert.Equal(0, (await fixture.PurgeAsync(replacement)).DeletedReceipts);
        await using (var edit = fixture.Context()) {
            await edit.Set<FiniteOperationEpochRecord>().Where(epoch => epoch.NamespaceId == fixture.Binding.Namespace.Value)
                .ExecuteUpdateAsync(setters => setters.SetProperty(epoch => epoch.ClosedAtUtc, now.AddDays(-3).AddSeconds(1)));
        }

        Assert.Equal(1, (await fixture.PurgeAsync(replacement)).DeletedReceipts);
        await fixture.CleanupMetadataAsync(replacement);
        await using var after = fixture.Context();
        Assert.Empty(await after.Set<ProjectDefinitionReceiptRecord>().ToArrayAsync());
        Assert.Equal(replacement.Namespace.Value, (await after.Set<FiniteOperationStateRecord>().SingleAsync()).NamespaceId);
        Assert.Equal(replacement.Namespace.Value, (await after.Set<FiniteOperationEpochRecord>().SingleAsync()).NamespaceId);
    }

    [Fact]
    public async Task Old_serializable_snapshot_cannot_admit_an_operation_after_its_epoch_rotated() {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.SetDeadlineAsync(TimeSpan.FromSeconds(1));
        await using var old = fixture.Context();
        await using var transaction = await old.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var snapshot = await old.Set<FiniteOperationEpochRecord>().AsNoTracking().SingleAsync();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await fixture.WaitForDeadlineAsync(snapshot.AdmissionClosesAtUtc, deadline.Token);
        Assert.Equal(FiniteOperationRotationResult.Rotated, await fixture.Service.RotateExpiredAsync(fixture.Binding, fixture.Pool, deadline.Token));

        var rejected = await Record.ExceptionAsync(() => fixture.Service.EnterAsync(old,
            fixture.Binding, fixture.Pool, fixture.Operation().Epoch.Value, deadline.Token));

        Assert.True(rejected is FiniteOperationAdmissionException { Failure: FiniteOperationAdmissionFailure.AdmissionDeadlineElapsed }
            or PostgresException { SqlState: PostgresErrorCodes.SerializationFailure }, rejected?.ToString());
        await using var read = fixture.Context();
        Assert.Equal(2, (await read.Set<FiniteOperationStateRecord>().SingleAsync()).CurrentEpoch);
        Assert.Empty(await read.Set<ProjectDefinitionReceiptRecord>().ToArrayAsync());
    }

    public enum PurgeBoundary { RowCount, PayloadBytes }

    private static FiniteOperationEpochRecord Closed(Fixture fixture, long epoch, DateTimeOffset admission,
        DateTimeOffset closed, long retention, int mutationMilliseconds = 1000) => new() {
        NamespaceId = fixture.Binding.Namespace.Value,
        PoolId = fixture.Pool.Pool.Value,
        Epoch = epoch,
        State = FiniteOperationEpochState.Closed,
        OpenedAtUtc = admission.AddHours(-1),
        AdmissionClosesAtUtc = admission,
        ClosedAtUtc = closed,
        MutationDeadlineMilliseconds = mutationMilliseconds,
        ReplayRetentionSeconds = retention
    };

    private sealed class RotationInterrupted : Exception;

    private sealed class RotationFailure : SaveChangesInterceptor {
        public bool ClosedEpochWasFlushed { get; private set; }

        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default) {
            ClosedEpochWasFlushed |= eventData.Context!.ChangeTracker.Entries<FiniteOperationEpochRecord>()
                .Any(entry => entry.Entity.State == FiniteOperationEpochState.Closed);
            return ValueTask.FromResult(result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            if (ClosedEpochWasFlushed && eventData.Context!.ChangeTracker.Entries<FiniteOperationEpochRecord>()
                    .Any(entry => entry.State == EntityState.Added && entry.Entity.Epoch > 1)) {
                throw new RotationInterrupted();
            }
            return ValueTask.FromResult(result);
        }
    }

    internal sealed class Fixture(PostgresTestDatabaseLease lease) : IAsyncDisposable, IDbContextFactory<ProjectsDbContext>, ICanonicalRuntimeDatabase {
        private DbContextOptions<ProjectsDbContext> ContextOptions { get; } = new DbContextOptionsBuilder<ProjectsDbContext>()
            .UseNpgsql(lease.ConnectionString).Options;
        private DbContextOptions<AppDbContext> CanonicalOptions => lease.CreateAppDbContextOptions();
        public DatasetActivationBinding Binding { get; } = new(new DatasetNamespaceId(Guid.NewGuid()), Guid.NewGuid(),
            DatasetLifecycleRecord.CurrentSchemaVersion, DatasetGuardProtocol.SharedRows);
        public ResolvedDatabaseProfile Profile { get; } = new(
            new DatabaseProfileRecord { ProviderKind = DatabaseProviderKind.PostgreSql },
            DatabaseProfileResolutionSource.ExplicitOverride, lease.ConnectionString);
        public long Generation => 0;
        private CoordinatedDatabaseTransaction? transactions;
        internal CoordinatedDatabaseTransaction Transactions => transactions ??= CoordinatedDatabaseTransaction.ForProfile(Profile);
        public FiniteOperationPoolRegistration Pool { get; } = ProjectFiniteOperationPool.Create(new());
        public FiniteOperationEpochStore Service => CreateService();
        public DatasetLifecycleStore LifecycleStore => new(new DbContextOptionsBuilder<DatasetLifecycleDbContext>()
            .UseNpgsql(lease.ConnectionString).Options, Transactions);
        public AppDbContext Context() => new(CanonicalOptions);
        public ProjectsDbContext CreateDbContext() => new(ContextOptions);
        public ProjectDefinitionOperation Operation() => new(Binding.Namespace, new(1), Guid.NewGuid(), Caller.Bind(Binding.Namespace));

        public ProjectDefinitionReceiptReader CreateReceiptReader(DatasetActivationService activation)
            => new(this, activation, new(this, ContextOptions, Transactions, this), NullLogger<ProjectDefinitionReceiptReader>.Instance);

        public FiniteOperationEpochStore CreateService(IInterceptor? interceptor = null)
            => new(CreateNeutralOptions(interceptor), Transactions);

        public FiniteOperationReceiptMaintenance CreateMaintenance(IInterceptor? interceptor = null) {
            var projects = new DbContextOptionsBuilder<ProjectsDbContext>(ContextOptions);
            if (interceptor is not null) {
                projects.AddInterceptors(interceptor);
            }
            return new(CreateNeutralOptions(interceptor), Transactions, [Pool],
                [new ProjectDefinitionReceiptRetention(projects.Options, Transactions)]);
        }

        public FiniteOperationMaintenanceCycle CreateCycle() => new(Pool, new([Pool]));

        public Task<FiniteReceiptPurgeObservation> PurgeAsync(DatasetActivationBinding? binding = null,
            CancellationToken cancellationToken = default, IInterceptor? interceptor = null)
            => CreateMaintenance(interceptor).PurgeRetiredBatchAsync(binding ?? Binding, Pool,
                ProjectFiniteOperationPool.DefinitionFamily, cancellationToken);

        public Task<FiniteMetadataCleanupResult> CleanupMetadataAsync(DatasetActivationBinding? binding = null,
            CancellationToken cancellationToken = default)
            => CreateMaintenance().CleanupMetadataAsync(binding ?? Binding, Pool, cancellationToken);

        internal DbContextOptions<FiniteOperationDbContext> CreateNeutralOptions(IInterceptor? interceptor = null) {
            var configured = new DbContextOptionsBuilder<FiniteOperationDbContext>().UseNpgsql(lease.ConnectionString);
            if (interceptor is not null) {
                configured.AddInterceptors(interceptor);
            }
            return configured.Options;
        }

        public static async Task<Fixture> CreateAsync() {
            AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
            var fixture = new Fixture(PostgresTestDatabaseLease.Create("project-operation-epochs"));
            try {
                await using var context = new AppDbContext(fixture.CanonicalOptions);
                await context.Database.MigrateAsync();
                await fixture.InitializeAsync(fixture.Service);
                return fixture;
            } catch {
                await fixture.DisposeAsync();
                throw;
            }
        }

        public async Task InitializeAsync(FiniteOperationEpochStore service, FiniteOperationPoolRegistration? pool = null) {
            await using var owner = Context();
            await using var transaction = await owner.Database.BeginTransactionAsync();
            using var participation = Transactions.Enter(owner);
            await LifecycleStore.StageActivationAsync(new(Guid.NewGuid(),
                DatasetActivationKind.Bootstrap, Binding, null));
            await service.InitializeNewNamespaceEnlistedAsync(Binding, pool ?? Pool, CancellationToken.None);
            await transaction.CommitAsync();
        }

        public async Task ReactivateAsync(DatasetActivationBinding replacement, bool commit,
            CancellationToken cancellationToken = default) {
            await using var owner = Context();
            await using var transaction = await owner.Database.BeginTransactionAsync(cancellationToken);
            using var participation = Transactions.Enter(owner);
            await LifecycleStore.StageActivationAsync(new(Guid.NewGuid(), DatasetActivationKind.Reactivate, replacement, Binding), cancellationToken);
            await Service.CloseForReactivationEnlistedAsync(Binding, replacement, Pool, cancellationToken);
            await Service.InitializeNewNamespaceEnlistedAsync(replacement, Pool, cancellationToken);
            if (commit) {
                await transaction.CommitAsync(cancellationToken);
            }
        }

        public async Task<DateTimeOffset> NowAsync(CancellationToken cancellationToken = default) {
            await using var context = Context();
            return await FiniteOperationEpochStore.ReadTimeAsync(context, cancellationToken);
        }

        public async Task SetDeadlineAsync(TimeSpan offset) {
            await using var context = Context();
            var state = await context.Set<FiniteOperationStateRecord>().SingleAsync(record => record.NamespaceId == Binding.Namespace.Value && record.PoolId == Pool.Pool.Value);
            var epoch = await context.Set<FiniteOperationEpochRecord>().SingleAsync(record => record.NamespaceId == Binding.Namespace.Value &&
                record.PoolId == Pool.Pool.Value && record.Epoch == state.CurrentEpoch);
            var now = await FiniteOperationEpochStore.ReadTimeAsync(context, CancellationToken.None);
            epoch.OpenedAtUtc = now.AddHours(-1);
            epoch.AdmissionClosesAtUtc = now.Add(offset);
            await context.SaveChangesAsync();
        }

        public async Task WaitForDeadlineAsync(DateTimeOffset deadline, CancellationToken cancellationToken) {
            await using var context = Context();
            while (await FiniteOperationEpochStore.ReadTimeAsync(context, cancellationToken) < deadline) {
                await Task.Delay(TimeSpan.FromMilliseconds(20), cancellationToken);
            }
        }

        public async Task WaitForBlockedBackendAsync(int pid, CancellationToken cancellationToken) {
            await using var context = Context();
            while (!await context.Database.SqlQuery<bool>(
                    $"SELECT cardinality(pg_blocking_pids({pid})) > 0 AS \"Value\"").SingleAsync(cancellationToken)) {
                await Task.Delay(TimeSpan.FromMilliseconds(20), cancellationToken);
            }
        }

        public async Task<long> RetiredThroughAsync() {
            await using var context = Context();
            return await context.Set<FiniteOperationStateRecord>().Select(record => record.RetiredThroughEpoch).SingleAsync();
        }

        public async Task SeedHistoryAsync(FiniteOperationEpochRecord[] closed, long retiredThrough) {
            await using var context = Context();
            await using var transaction = await context.Database.BeginTransactionAsync();
            await context.Set<FiniteOperationEpochRecord>().Where(epoch => epoch.NamespaceId == Binding.Namespace.Value && epoch.PoolId == Pool.Pool.Value)
                .ExecuteDeleteAsync();
            var state = await context.Set<FiniteOperationStateRecord>().SingleAsync(state => state.NamespaceId == Binding.Namespace.Value && state.PoolId == Pool.Pool.Value);
            state.CurrentEpoch = closed.Max(epoch => epoch.Epoch) + 1;
            state.RetiredThroughEpoch = retiredThrough;
            var now = await FiniteOperationEpochStore.ReadTimeAsync(context, CancellationToken.None);
            context.AddRange(closed);
            context.Add(new FiniteOperationEpochRecord {
                NamespaceId = Binding.Namespace.Value,
                PoolId = Pool.Pool.Value,
                Epoch = state.CurrentEpoch,
                State = FiniteOperationEpochState.Open,
                OpenedAtUtc = now.AddHours(-1),
                AdmissionClosesAtUtc = now.AddMinutes(-1),
                MutationDeadlineMilliseconds = 30000,
                ReplayRetentionSeconds = 86400
            });
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        public async Task SeedReceiptsAsync(long epoch, int count, int payloadBytes) {
            await using var context = Context();
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "Projects_DefinitionReceipts" ("NamespaceId", "Epoch", "CallerKind", "CallerKey", "OperationId",
                    "SchemaVersion", "NormalizationVersion", "ResultVersion", "CommandKind", "SemanticDigest", "ProjectId",
                    "LifetimeId", "AcceptedRevision", "AcceptedAtUtc", "AcceptedPayload")
                SELECT {Binding.Namespace.Value}, {epoch}, {(int)Caller.Kind}, {Caller.Key.ToArray()}, gen_random_uuid(),
                    1, 1, 1, {(int)ProjectDefinitionCommandKind.Replace}, {new byte[32]}, gen_random_uuid(), gen_random_uuid(),
                    gen_random_uuid(), clock_timestamp(), decode(repeat('ab', {payloadBytes}), 'hex')
                FROM generate_series(1, {count})
                """);
        }

        public ValueTask DisposeAsync() => lease.DisposeAsync();
    }
}
