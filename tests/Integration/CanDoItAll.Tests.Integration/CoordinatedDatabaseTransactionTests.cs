using System.Data.Common;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Collaboration;
using CanDoItAll.Modules.TestLab;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class CoordinatedDatabaseTransactionTests {
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Owner_models_share_the_exact_transaction_and_commit_or_roll_back_together(bool commit) {
        await using var application = await TestApplication.CreateAsync();
        var profile = application.Services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile;
        var coordination = CoordinatedDatabaseTransaction.ForProfile(profile);
        var interceptor = new CommandProbe();
        var participantOptions = new DbContextOptionsBuilder<TestLabDbContext>(Options<TestLabDbContext>(profile))
            .AddInterceptors(interceptor).Options;
        await using (var owner = new CollaborationDbContext(Options<CollaborationDbContext>(profile))) {
            await using var transaction = await owner.Database.BeginTransactionAsync();
            using var entered = coordination.Enter(owner);
            owner.Add(new CollaborationThreadRecord { Subject = "Coordinated owner write" });
            await owner.SaveChangesAsync();
            await using (var participant = await coordination.CreateEnlistedAsync(participantOptions,
                static options => new TestLabDbContext(options))) {
                Assert.Same(owner.Database.GetDbConnection(), participant.Database.GetDbConnection());
                Assert.Same(transaction.GetDbTransaction(), participant.Database.CurrentTransaction!.GetDbTransaction());
                participant.Add(new TestPlan { Title = "Coordinated participant write" });
                await participant.SaveChangesAsync();
                Assert.True(interceptor.Readers > 0);
                using var nested = coordination.Enter(participant);
                await using var nestedParticipant = await coordination.CreateEnlistedAsync(
                    Options<TestLabDbContext>(profile), static options => new TestLabDbContext(options));
                Assert.Single(await nestedParticipant.Set<TestPlan>().ToListAsync());
                await using var independent = new TestLabDbContext(Options<TestLabDbContext>(profile));
                Assert.Null(independent.Database.CurrentTransaction);
                Assert.NotSame(owner.Database.GetDbConnection(), independent.Database.GetDbConnection());
                Assert.Empty(await independent.Set<TestPlan>().ToListAsync());
            }
            Assert.Same(owner.Database.GetDbConnection(), transaction.GetDbTransaction().Connection);
            if (commit) {
                await transaction.CommitAsync();
            }
        }
        await using var threads = new CollaborationDbContext(Options<CollaborationDbContext>(profile));
        await using var plans = new TestLabDbContext(Options<TestLabDbContext>(profile));
        Assert.Equal(commit ? 1 : 0, await threads.Set<CollaborationThreadRecord>().CountAsync());
        Assert.Equal(commit ? 1 : 0, await plans.Set<TestPlan>().CountAsync());
    }

    [Fact]
    public async Task Missing_transaction_and_mismatched_profiles_or_nested_transactions_fail_before_writes() {
        await using var application = await TestApplication.CreateAsync();
        var profile = application.Services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile;
        var coordination = CoordinatedDatabaseTransaction.ForProfile(profile);
        var participantOptions = Options<TestLabDbContext>(profile);
        await Assert.ThrowsAsync<InvalidOperationException>(() => coordination.CreateEnlistedAsync(
            participantOptions, static options => new TestLabDbContext(options)));
        await using var owner = new CollaborationDbContext(Options<CollaborationDbContext>(profile));
        Assert.Throws<InvalidOperationException>(() => coordination.Enter(owner));
        await using var transaction = await owner.Database.BeginTransactionAsync();
        using var entered = coordination.Enter(owner);
        await using var independent = new CollaborationDbContext(Options<CollaborationDbContext>(profile));
        await using var independentTransaction = await independent.Database.BeginTransactionAsync();
        Assert.Throws<InvalidOperationException>(() => coordination.Enter(independent));
        var otherSettings = new Npgsql.NpgsqlConnectionStringBuilder(profile.ConnectionString) {
            Database = "different_unopened_profile"
        };
        var otherOptions = Options<TestLabDbContext>(profile with { ConnectionString = otherSettings.ConnectionString });
        await Assert.ThrowsAsync<InvalidOperationException>(() => coordination.CreateEnlistedAsync(
            otherOptions, static options => new TestLabDbContext(options)));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => coordination.CreateEnlistedAsync(
            participantOptions, static options => new TestLabDbContext(options), cancellation.Token));
        Assert.Empty(await owner.Set<CollaborationThreadRecord>().ToListAsync());
    }

    [Fact]
    public async Task Detached_work_cannot_reuse_a_retired_transaction_scope() {
        await using var application = await TestApplication.CreateAsync();
        var profile = application.Services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile;
        var coordination = CoordinatedDatabaseTransaction.ForProfile(profile);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task detached;
        await using var owner = new CollaborationDbContext(Options<CollaborationDbContext>(profile));
        await using var transaction = await owner.Database.BeginTransactionAsync();
        using (coordination.Enter(owner)) {
            detached = Task.Run(async () => {
                await resume.Task;
                await using var participant = await coordination.CreateEnlistedAsync(
                    Options<TestLabDbContext>(profile), static options => new TestLabDbContext(options));
                participant.Add(new TestPlan { Title = "Must not be written" });
                await participant.SaveChangesAsync();
            });
        }
        resume.SetResult();
        await Assert.ThrowsAsync<InvalidOperationException>(() => detached.WaitAsync(TimeSpan.FromSeconds(10)));
        await using var readback = new TestLabDbContext(Options<TestLabDbContext>(profile));
        Assert.Empty(await readback.Set<TestPlan>().ToListAsync());
    }

    [Fact]
    public async Task Explicit_target_session_keeps_the_target_and_runtime_transactions_separate() {
        await using var environment = CanDoItAllTestEnvironment.Create("coordinated-target");
        await using var source = await TestApplication.CreateAsync(new TestHarnessOptions {
            TestEnvironment = environment,
            ActiveProfile = environment.CreatePostgreSqlProfile("source")
        });
        await using var target = await TestApplication.CreateAsync(new TestHarnessOptions {
            TestEnvironment = environment,
            ActiveProfile = environment.CreatePostgreSqlProfile("target")
        });
        var sourceProfile = source.Services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile;
        var targetProfile = target.Services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile;
        var runtime = CoordinatedDatabaseTransaction.ForProfile(sourceProfile);
        var transfer = CoordinatedDatabaseTransaction.ForProfile(targetProfile);
        await using var sourceOwner = new CollaborationDbContext(Options<CollaborationDbContext>(sourceProfile));
        await using var sourceTransaction = await sourceOwner.Database.BeginTransactionAsync();
        using var sourceScope = runtime.Enter(sourceOwner);
        await using (var targetOwner = new CollaborationDbContext(Options<CollaborationDbContext>(targetProfile))) {
            await using var targetTransaction = await targetOwner.Database.BeginTransactionAsync();
            Assert.Throws<InvalidOperationException>(() => runtime.Enter(targetOwner));
            using var targetScope = transfer.Enter(targetOwner);
            await using var participant = await transfer.CreateEnlistedAsync(Options<TestLabDbContext>(targetProfile),
                static options => new TestLabDbContext(options));
            participant.Add(new TestPlan { Title = "Explicit transfer target" });
            await participant.SaveChangesAsync();
            await targetTransaction.CommitAsync();
        }
        await using var sourceReadback = new TestLabDbContext(Options<TestLabDbContext>(sourceProfile));
        await using var targetReadback = new TestLabDbContext(Options<TestLabDbContext>(targetProfile));
        Assert.Empty(await sourceReadback.Set<TestPlan>().ToListAsync());
        Assert.Equal("Explicit transfer target", (await targetReadback.Set<TestPlan>().SingleAsync()).Title);
    }

    [Fact]
    public async Task InMemory_test_scope_requires_the_same_store_and_does_not_claim_a_transaction() {
        var databaseName = $"coordinated-test-{Guid.NewGuid():N}";
        var root = new InMemoryDatabaseRoot();
        var profile = new ResolvedDatabaseProfile(new() { ProviderKind = DatabaseProviderKind.InMemory },
            DatabaseProfileResolutionSource.ExplicitOverride, databaseName);
        var coordination = CoordinatedDatabaseTransaction.ForProfile(profile);
        await using var owner = new CollaborationDbContext(new DbContextOptionsBuilder<CollaborationDbContext>()
            .UseInMemoryDatabase(databaseName, root).Options);
        using var entered = coordination.Enter(owner);
        var options = new DbContextOptionsBuilder<TestLabDbContext>().UseInMemoryDatabase(databaseName, root).Options;
        await using var participant = await coordination.CreateEnlistedAsync(options,
            static value => new TestLabDbContext(value));
        Assert.Null(participant.Database.CurrentTransaction);
        var unrelated = new DbContextOptionsBuilder<TestLabDbContext>()
            .UseInMemoryDatabase(databaseName, new InMemoryDatabaseRoot()).Options;
        await Assert.ThrowsAsync<InvalidOperationException>(() => coordination.CreateEnlistedAsync(
            unrelated, static value => new TestLabDbContext(value)));
    }

    [Fact]
    public async Task Retained_participants_reject_commands_and_saves_after_their_scope_exits() {
        await using var application = await TestApplication.CreateAsync();
        var profile = application.Services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile;
        var coordination = CoordinatedDatabaseTransaction.ForProfile(profile);
        await using var owner = new CollaborationDbContext(Options<CollaborationDbContext>(profile));
        await using var transaction = await owner.Database.BeginTransactionAsync();
        using var entered = coordination.Enter(owner);
        await using var participant = await coordination.CreateEnlistedAsync(Options<TestLabDbContext>(profile),
            static options => new TestLabDbContext(options));
        using var nested = coordination.Enter(participant);
        await using var nestedParticipant = await coordination.CreateEnlistedAsync(Options<TestLabDbContext>(profile),
            static options => new TestLabDbContext(options));

        Assert.Throws<InvalidOperationException>(() => entered.Dispose());
        nested.Dispose();
        await Assert.ThrowsAsync<InvalidOperationException>(() => nestedParticipant.Set<TestPlan>().ToListAsync());
        Assert.Empty(await participant.Set<TestPlan>().ToListAsync());
        entered.Dispose();

        participant.Add(new TestPlan { Title = "Must not survive retired scope" });
        Assert.Throws<InvalidOperationException>(() => participant.SaveChanges());
        await Assert.ThrowsAsync<InvalidOperationException>(() => participant.SaveChangesAsync());
        Assert.Throws<InvalidOperationException>(() => participant.Set<TestPlan>().ToList());
        await Assert.ThrowsAsync<InvalidOperationException>(() => participant.Set<TestPlan>().ToListAsync());
        Assert.Throws<InvalidOperationException>(() => participant.Set<TestPlan>()
            .ExecuteUpdate(setters => setters.SetProperty(item => item.Title, "Must not update")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => participant.Set<TestPlan>()
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Title, "Must not update")));
        owner.Add(new CollaborationThreadRecord { Subject = "Owner still controls transaction" });
        await owner.SaveChangesAsync();
        await transaction.CommitAsync();

        await using var readback = new TestLabDbContext(Options<TestLabDbContext>(profile));
        await using var ownerReadback = new CollaborationDbContext(Options<CollaborationDbContext>(profile));
        Assert.Empty(await readback.Set<TestPlan>().ToListAsync());
        Assert.Equal("Owner still controls transaction", (await ownerReadback.Set<CollaborationThreadRecord>().SingleAsync()).Subject);
    }

    [Fact]
    public async Task Completed_and_replaced_owner_transactions_cannot_reactivate_an_old_frame() {
        await using var application = await TestApplication.CreateAsync();
        var profile = application.Services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile;
        var coordination = CoordinatedDatabaseTransaction.ForProfile(profile);
        await using var owner = new CollaborationDbContext(Options<CollaborationDbContext>(profile));
        await owner.Database.OpenConnectionAsync();
        await using var originalTransaction = await owner.Database.BeginTransactionAsync();
        using var originalScope = coordination.Enter(owner);
        await using var originalParticipant = await coordination.CreateEnlistedAsync(Options<TestLabDbContext>(profile),
            static options => new TestLabDbContext(options));
        await originalTransaction.CommitAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => coordination.CreateEnlistedAsync(
            Options<TestLabDbContext>(profile), static options => new TestLabDbContext(options)));

        await using var replacement = await owner.Database.BeginTransactionAsync();
        Assert.NotSame(originalTransaction, replacement);
        Assert.Same(originalTransaction.GetDbTransaction(), replacement.GetDbTransaction());
        Assert.Throws<InvalidOperationException>(() => coordination.Enter(owner));
        await Assert.ThrowsAsync<InvalidOperationException>(() => coordination.CreateEnlistedAsync(
            Options<TestLabDbContext>(profile), static options => new TestLabDbContext(options)));
        originalParticipant.Add(new TestPlan { Title = "Old transaction must not write into replacement" });
        await Assert.ThrowsAsync<InvalidOperationException>(() => originalParticipant.SaveChangesAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => originalParticipant.Set<TestPlan>().ToListAsync());

        originalScope.Dispose();
        using (coordination.Enter(owner)) {
            await using var currentParticipant = await coordination.CreateEnlistedAsync(Options<TestLabDbContext>(profile),
                static options => new TestLabDbContext(options));
            currentParticipant.Add(new TestPlan { Title = "Explicit replacement transaction" });
            await currentParticipant.SaveChangesAsync();
        }
        await replacement.CommitAsync();
        await using var readback = new TestLabDbContext(Options<TestLabDbContext>(profile));
        Assert.Equal("Explicit replacement transaction", (await readback.Set<TestPlan>().SingleAsync()).Title);
    }

    [Fact]
    public async Task InMemory_same_name_with_different_internal_service_providers_is_rejected() {
        var databaseName = $"coordinated-test-{Guid.NewGuid():N}";
        var profile = new ResolvedDatabaseProfile(new() { ProviderKind = DatabaseProviderKind.InMemory },
            DatabaseProfileResolutionSource.ExplicitOverride, databaseName);
        var coordination = CoordinatedDatabaseTransaction.ForProfile(profile);
        using var firstServices = new ServiceCollection().AddEntityFrameworkInMemoryDatabase().BuildServiceProvider();
        using var secondServices = new ServiceCollection().AddEntityFrameworkInMemoryDatabase().BuildServiceProvider();
        await using var owner = new CollaborationDbContext(new DbContextOptionsBuilder<CollaborationDbContext>()
            .UseInMemoryDatabase(databaseName).UseInternalServiceProvider(firstServices).Options);
        using var entered = coordination.Enter(owner);
        var ownerStoreOptions = new DbContextOptionsBuilder<TestLabDbContext>()
            .UseInMemoryDatabase(databaseName).UseInternalServiceProvider(firstServices).Options;
        await using (var participant = await coordination.CreateEnlistedAsync(ownerStoreOptions,
            static value => new TestLabDbContext(value))) {
            participant.Add(new TestPlan { Title = "Actual owner store" });
            await participant.SaveChangesAsync();
        }
        var unrelated = new DbContextOptionsBuilder<TestLabDbContext>()
            .UseInMemoryDatabase(databaseName).UseInternalServiceProvider(secondServices).Options;
        await Assert.ThrowsAsync<InvalidOperationException>(() => coordination.CreateEnlistedAsync(
            unrelated, static value => new TestLabDbContext(value)));
        await using var separateStore = new TestLabDbContext(unrelated);
        Assert.Empty(await separateStore.Set<TestPlan>().ToListAsync());
        await using var ownerStore = new TestLabDbContext(ownerStoreOptions);
        Assert.Equal("Actual owner store", (await ownerStore.Set<TestPlan>().SingleAsync()).Title);
    }

    [Fact]
    public async Task Retained_InMemory_participants_reject_saves_after_scope_exit() {
        var databaseName = $"coordinated-test-{Guid.NewGuid():N}";
        var root = new InMemoryDatabaseRoot();
        var profile = new ResolvedDatabaseProfile(new() { ProviderKind = DatabaseProviderKind.InMemory },
            DatabaseProfileResolutionSource.ExplicitOverride, databaseName);
        var coordination = CoordinatedDatabaseTransaction.ForProfile(profile);
        await using var owner = new CollaborationDbContext(new DbContextOptionsBuilder<CollaborationDbContext>()
            .UseInMemoryDatabase(databaseName, root).Options);
        var options = new DbContextOptionsBuilder<TestLabDbContext>().UseInMemoryDatabase(databaseName, root).Options;
        using var entered = coordination.Enter(owner);
        await using var participant = await coordination.CreateEnlistedAsync(options, static value => new TestLabDbContext(value));
        participant.Add(new TestPlan { Title = "Saved while scope was active" });
        await participant.SaveChangesAsync();
        entered.Dispose();
        participant.Add(new TestPlan { Title = "Must not be saved after scope exit" });
        Assert.Throws<InvalidOperationException>(() => participant.SaveChanges());
        await Assert.ThrowsAsync<InvalidOperationException>(() => participant.SaveChangesAsync());
        await using var readback = new TestLabDbContext(options);
        Assert.Equal("Saved while scope was active", (await readback.Set<TestPlan>().SingleAsync()).Title);
    }

    private static DbContextOptions<TContext> Options<TContext>(ResolvedDatabaseProfile profile) where TContext : DbContext {
        var options = new DbContextOptionsBuilder<TContext>();
        AppDbContextOptionsConfigurator.Configure(options, profile);
        return options.Options;
    }

    private sealed class CommandProbe : DbCommandInterceptor {
        public int Readers { get; private set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default) {
            Readers++;
            return ValueTask.FromResult(result);
        }
    }
}
