using System.Data;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CanDoItAll.Tests.Integration;

public sealed class DatasetLifecyclePersistenceTests {
    [Fact]
    public async Task Offline_deletion_recovery_refuses_an_active_owner_without_waiting_and_holds_exclusion_until_rollback() {
        await using var fixture = await Fixture.CreateAsync();
        var original = Bootstrap();
        await fixture.ActivateAsync(original);
        await using (var owner = fixture.Context()) {
            await using var ownership = await owner.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            await DatasetLifecycleStore.RequireActiveForMutationAsync(owner, original.Binding);
            await using var offline = fixture.Context();
            await using var attempt = await offline.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            await offline.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '0'; SET LOCAL statement_timeout = '2s'");
            var busy = await Assert.ThrowsAsync<PostgresException>(() =>
                DatasetLifecycleStore.RequireActiveForDeletionRecoveryAsync(offline, original.Binding));
            Assert.Equal(PostgresErrorCodes.LockNotAvailable, busy.SqlState);
            Assert.Equal(original.Binding, (await fixture.Store.ReadAsync())!.Binding);
        }
        await using (var offline = fixture.Context()) {
            await using var exclusion = await offline.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            await DatasetLifecycleStore.RequireActiveForDeletionRecoveryAsync(offline, original.Binding);
            await using var writer = fixture.Context();
            await using var write = await writer.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            await writer.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '100ms'");
            var busy = await Assert.ThrowsAsync<PostgresException>(() =>
                DatasetLifecycleStore.RequireActiveForMutationAsync(writer, original.Binding));
            Assert.Equal(PostgresErrorCodes.LockNotAvailable, busy.SqlState);
            await exclusion.RollbackAsync();
        }
        await using var successor = fixture.Context();
        await using var current = await successor.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await DatasetLifecycleStore.RequireActiveForMutationAsync(successor, original.Binding);
    }

    [Fact]
    public async Task Offline_deletion_recovery_refuses_a_replaced_namespace_without_changing_the_current_binding() {
        await using var fixture = await Fixture.CreateAsync();
        var original = Bootstrap();
        await fixture.ActivateAsync(original);
        await using var offline = fixture.Context();
        await using var attempt = await offline.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await Assert.ThrowsAsync<DatasetActivationMismatchException>(() =>
            DatasetLifecycleStore.RequireActiveForDeletionRecoveryAsync(offline, Binding()));
        await attempt.RollbackAsync();
        Assert.Equal(original.Binding, (await fixture.Store.ReadAsync())!.Binding);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Staged_lifecycle_and_owner_initialization_share_the_callers_commit_or_rollback(bool commit) {
        await using var fixture = await Fixture.CreateAsync();
        var plan = Bootstrap();
        await using (var owner = fixture.Context()) {
            await using var transaction = await owner.Database.BeginTransactionAsync();
            using var participation = fixture.Transactions.Enter(owner);
            await fixture.Store.StageActivationAsync(plan);
            await owner.Database.ExecuteSqlRawAsync("INSERT INTO \"ActivationOwnerProbe\" VALUES (1)");
            Assert.Null(await fixture.Store.ReadAsync());
            if (commit) {
                await transaction.CommitAsync();
            }
        }
        var state = await fixture.Store.ReadAsync();
        Assert.Equal(commit ? plan.Binding : null, state?.Binding);
        await using var read = fixture.Context();
        var ownerRows = await read.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM \"ActivationOwnerProbe\"").SingleAsync();
        Assert.Equal(commit ? 1 : 0, ownerRows);
    }

    [Fact]
    public async Task Committed_plan_resumes_exactly_alias_attach_preserves_binding_and_stale_rotation_cannot_overwrite() {
        await using var fixture = await Fixture.CreateAsync();
        var original = Bootstrap();
        await fixture.ActivateAsync(original);
        await fixture.ActivateAsync(original);
        var alias = new DatasetActivationPlan(Guid.NewGuid(), DatasetActivationKind.Attach, original.Binding, original.Binding);
        await fixture.ActivateAsync(alias);
        Assert.Equal(original.Binding, (await fixture.Store.ReadAsync())!.Binding);

        var replacement = new DatasetActivationPlan(Guid.NewGuid(), DatasetActivationKind.Reactivate, Binding(), original.Binding);
        await fixture.ActivateAsync(replacement);
        await fixture.ActivateAsync(replacement);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.ActivateAsync(original));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.ActivateAsync(alias));
        Assert.Equal(replacement.Binding, (await fixture.Store.ReadAsync())!.Binding);
        await using var current = fixture.Context();
        await using var transaction = await current.Database.BeginTransactionAsync();
        await Assert.ThrowsAsync<DatasetActivationMismatchException>(() =>
            DatasetLifecycleStore.RequireActiveForMutationAsync(current, original.Binding));
        await DatasetLifecycleStore.RequireActiveForMutationAsync(current, replacement.Binding);
    }

    [Fact]
    public async Task Shared_owner_guard_is_retained_until_outer_transaction_finishes_and_bounds_maintenance_wait() {
        await using var fixture = await Fixture.CreateAsync();
        var original = Bootstrap();
        await fixture.ActivateAsync(original);
        var replacement = new DatasetActivationPlan(Guid.NewGuid(), DatasetActivationKind.Reactivate, Binding(), original.Binding);
        await using (var held = fixture.Context()) {
            await using var holding = await held.Database.BeginTransactionAsync();
            using (fixture.Transactions.Enter(held)) {
                await fixture.Store.RequireActiveForMutationAsync(original.Binding);
            }
            await using (var waiting = fixture.Context()) {
                await using var waitingTransaction = await waiting.Database.BeginTransactionAsync();
                await waiting.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '100ms'");
                using var participation = fixture.Transactions.Enter(waiting);
                var blocked = await Assert.ThrowsAsync<PostgresException>(() => fixture.Store.StageActivationAsync(replacement));
                Assert.Equal(PostgresErrorCodes.LockNotAvailable, blocked.SqlState);
            }
            await holding.RollbackAsync();
        }
        await fixture.ActivateAsync(replacement);
        Assert.Equal(replacement.Binding, (await fixture.Store.ReadAsync())!.Binding);
    }

    [Fact]
    public async Task Serializable_guard_rejects_a_namespace_changed_since_its_original_snapshot() {
        await using var fixture = await Fixture.CreateAsync();
        var original = Bootstrap();
        await fixture.ActivateAsync(original);
        await using var old = fixture.Context();
        await using var transaction = await old.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        Assert.Equal(original.Binding.Namespace.Value, (await old.Set<DatasetLifecycleRecord>().AsNoTracking().SingleAsync()).NamespaceId);
        var replacement = new DatasetActivationPlan(Guid.NewGuid(), DatasetActivationKind.Reactivate, Binding(), original.Binding);
        await fixture.ActivateAsync(replacement);

        var failure = await Assert.ThrowsAsync<PostgresException>(() =>
            DatasetLifecycleStore.RequireActiveForMutationAsync(old, original.Binding));

        Assert.Equal(PostgresErrorCodes.SerializationFailure, failure.SqlState);
        Assert.Equal(replacement.Binding, (await fixture.Store.ReadAsync())!.Binding);
    }

    private static DatasetActivationBinding Binding()
        => new(new DatasetNamespaceId(Guid.NewGuid()), Guid.NewGuid(), DatasetLifecycleRecord.CurrentSchemaVersion,
            DatasetGuardProtocol.SharedRows);

    private static DatasetActivationPlan Bootstrap()
        => new(Guid.NewGuid(), DatasetActivationKind.Bootstrap, Binding(), null);

    private sealed class Fixture(PostgresTestDatabaseLease lease) : IAsyncDisposable {
        private DbContextOptions<DatasetLifecycleDbContext> Options { get; } = new DbContextOptionsBuilder<DatasetLifecycleDbContext>()
            .UseNpgsql(lease.ConnectionString).Options;
        public CoordinatedDatabaseTransaction Transactions { get; } = CoordinatedDatabaseTransaction.ForProfile(new(
            new DatabaseProfileRecord { ProviderKind = DatabaseProviderKind.PostgreSql },
            DatabaseProfileResolutionSource.ExplicitOverride, lease.ConnectionString));
        public DatasetLifecycleStore Store => new(Options, Transactions);
        public DatasetLifecycleDbContext Context() => new(Options);

        public static async Task<Fixture> CreateAsync() {
            var fixture = new Fixture(PostgresTestDatabaseLease.Create("dataset-lifecycle"));
            try {
                await using var context = fixture.Context();
                await context.Database.EnsureCreatedAsync();
                await context.Database.ExecuteSqlRawAsync("CREATE TABLE \"ActivationOwnerProbe\" (\"Id\" integer PRIMARY KEY)");
                return fixture;
            } catch {
                await fixture.DisposeAsync();
                throw;
            }
        }

        public async Task ActivateAsync(DatasetActivationPlan plan) {
            await using var context = Context();
            await using var transaction = await context.Database.BeginTransactionAsync();
            using var participation = Transactions.Enter(context);
            await Store.StageActivationAsync(plan);
            await transaction.CommitAsync();
        }

        public ValueTask DisposeAsync() => lease.DisposeAsync();
    }
}
