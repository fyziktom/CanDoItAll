using System.Data.Common;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace CanDoItAll.Tests.Components.Shell;

public sealed class SerializableMutationScopeTests {
    [Fact]
    public async Task InMemory_multi_project_scope_serializes_both_project_keys() {
        var options = new DbContextOptionsBuilder<DbContext>()
            .UseInMemoryDatabase($"serializable-scope-{Guid.NewGuid():N}")
            .Options;
        var sourceProjectId =
            Guid.Parse("10000000-0000-0000-0000-000000000021");
        var targetProjectId =
            Guid.Parse("20000000-0000-0000-0000-000000000022");
        var projectKeys = new[]
        {
            $"project:{sourceProjectId:D}",
            $"project:{targetProjectId:D}"
        };
        await using var ownerContext = new DbContext(options);
        var ownerScope = await SerializableMutationScope.BeginAsync(
            ownerContext,
            projectKeys,
            CancellationToken.None);
        try {
            foreach (var projectKey in projectKeys) {
                await using var contenderContext =
                    new DbContext(options);
                using var cancellationSource =
                    new CancellationTokenSource(
                        TimeSpan.FromMilliseconds(200));

                await Assert.ThrowsAnyAsync<OperationCanceledException>(
                    async () => {
                        await using var blockedScope =
                            await SerializableMutationScope.BeginAsync(
                                contenderContext,
                                projectKey,
                                cancellationSource.Token);
                    });
            }
        } finally {
            await ownerScope.DisposeAsync();
        }

        foreach (var projectKey in projectKeys) {
            await using var context = new DbContext(options);
            using var cancellationSource =
                new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await using var acquiredScope =
                await SerializableMutationScope.BeginAsync(
                    context,
                    projectKey,
                    cancellationSource.Token);
            await acquiredScope.CommitAsync(cancellationSource.Token);
        }
    }

    [Fact]
    public async Task Cancelled_multi_lock_acquisition_releases_its_prefix_while_the_later_lock_remains_held() {
        var scopeKeys = CreateDistinctStripeKeys().OrderBy(CurrentProcessStripe).ToArray();
        await using var holderContext = CreateInMemoryContext();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using (var holder = await SerializableMutationScope.BeginAsync(holderContext, scopeKeys[1], timeout.Token)) {
            await using var attemptContext = CreateInMemoryContext();
            using var acquisitionCancellation = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
            var pending = SerializableMutationScope.BeginAsync(attemptContext, scopeKeys, acquisitionCancellation.Token);
            try {
                await using var prefixContender = CreateInMemoryContext();
                using var blockedTimeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
                await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => {
                    await using var blocked = await SerializableMutationScope.BeginAsync(
                        prefixContender, scopeKeys[0], blockedTimeout.Token);
                });

                acquisitionCancellation.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
                await AssertProcessLocksReleasedAsync([scopeKeys[0]]);
            } finally {
                acquisitionCancellation.Cancel();
                try {
                    await using var acquired = await pending;
                } catch (OperationCanceledException) when (acquisitionCancellation.IsCancellationRequested) {
                }
            }
        }

        await AssertProcessLocksReleasedAsync(scopeKeys);
    }

    [Fact]
    public async Task Failed_acquisition_preserves_the_original_failure_and_releases_all_process_locks() {
        await using var database = PostgresTestDatabaseLease.Create("mutation-acquisition-cleanup");
        var probe = new AdvisoryFailureInterceptor();
        var options = new DbContextOptionsBuilder<DbContext>()
            .UseNpgsql(database.ConnectionString)
            .AddInterceptors(probe)
            .Options;
        await using var context = new DbContext(options);
        var scopeKeys = CreateDistinctStripeKeys();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            SerializableMutationScope.BeginAsync(context, scopeKeys, timeout.Token));

        Assert.Same(probe.Failure, failure);
        Assert.NotNull(probe.Transaction);
        Assert.Null(context.Database.CurrentTransaction);
        await AssertProcessLocksReleasedAsync(scopeKeys);
    }

    [Fact]
    public async Task Failed_acquisition_and_cleanup_preserve_both_failures_and_release_all_process_locks() {
        await using var database = PostgresTestDatabaseLease.Create("mutation-double-failure");
        var probe = new AdvisoryFailureInterceptor();
        var options = new DbContextOptionsBuilder<DbContext>()
            .UseNpgsql(database.ConnectionString)
            .ReplaceService<IRelationalTransactionFactory, DisposeFailureTransactionFactory>()
            .AddInterceptors(probe)
            .Options;
        await using var context = new DbContext(options);
        var scopeKeys = CreateDistinctStripeKeys();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        var failure = await Assert.ThrowsAsync<AggregateException>(() =>
            SerializableMutationScope.BeginAsync(context, scopeKeys, timeout.Token));

        var transaction = Assert.IsType<DisposeFailureTransaction>(probe.Transaction);
        Assert.Collection(failure.InnerExceptions,
            primary => Assert.Same(probe.Failure, primary),
            cleanup => Assert.Same(transaction.Failure, cleanup));
        Assert.Equal(1, transaction.DisposeCalls);
        Assert.Null(context.Database.CurrentTransaction);
        await AssertProcessLocksReleasedAsync(scopeKeys);
    }

    [Fact]
    public async Task Failed_disposal_is_reported_and_releases_all_process_locks() {
        await using var database = PostgresTestDatabaseLease.Create("mutation-disposal-failure");
        var options = new DbContextOptionsBuilder<DbContext>()
            .UseNpgsql(database.ConnectionString)
            .ReplaceService<IRelationalTransactionFactory, DisposeFailureTransactionFactory>()
            .Options;
        await using var context = new DbContext(options);
        var scopeKeys = CreateDistinctStripeKeys();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var scope = await SerializableMutationScope.BeginAsync(context, scopeKeys, timeout.Token);
        var transaction = Assert.IsType<DisposeFailureTransaction>(context.Database.CurrentTransaction);
        try {
            var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => scope.DisposeAsync().AsTask());

            Assert.Same(transaction.Failure, failure);
            Assert.Equal(1, transaction.DisposeCalls);
            Assert.Null(context.Database.CurrentTransaction);
            await AssertProcessLocksReleasedAsync(scopeKeys);
        } finally {
            await scope.DisposeAsync();
        }
    }

    [Fact]
    public async Task Repeated_disposal_after_failure_cannot_release_a_successors_process_locks() {
        await using var database = PostgresTestDatabaseLease.Create("mutation-disposal-idempotence");
        var options = new DbContextOptionsBuilder<DbContext>()
            .UseNpgsql(database.ConnectionString)
            .ReplaceService<IRelationalTransactionFactory, DisposeFailureTransactionFactory>()
            .Options;
        await using var context = new DbContext(options);
        var scopeKeys = CreateDistinctStripeKeys();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var scope = await SerializableMutationScope.BeginAsync(context, scopeKeys, timeout.Token);
        var transaction = Assert.IsType<DisposeFailureTransaction>(context.Database.CurrentTransaction);
        try {
            var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => scope.DisposeAsync().AsTask());
            Assert.Same(transaction.Failure, failure);

            await using (var successorContext = CreateInMemoryContext()) {
                await using var successor = await SerializableMutationScope.BeginAsync(
                    successorContext, scopeKeys, timeout.Token);
                await scope.DisposeAsync();
                Assert.Equal(1, transaction.DisposeCalls);

                foreach (var scopeKey in scopeKeys) {
                    await using var contender = CreateInMemoryContext();
                    using var blockedTimeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
                    await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => {
                        await using var blocked = await SerializableMutationScope.BeginAsync(
                            contender, scopeKey, blockedTimeout.Token);
                    });
                }
            }

            await AssertProcessLocksReleasedAsync(scopeKeys);
        } finally {
            await scope.DisposeAsync();
        }
    }

    private static DbContext CreateInMemoryContext() => new(new DbContextOptionsBuilder<DbContext>()
        .UseInMemoryDatabase($"mutation-cleanup-{Guid.NewGuid():N}")
        .Options);

    private static string[] CreateDistinctStripeKeys() {
        var first = $"mutation-cleanup:{Guid.NewGuid():N}";
        var firstStripe = CurrentProcessStripe(first);
        var second = Enumerable.Range(1, 128)
            .Select(index => $"{first}:{index}")
            .First(key => CurrentProcessStripe(key) != firstStripe);
        return [first, second];
    }

    private static int CurrentProcessStripe(string key) => (key.GetHashCode(StringComparison.Ordinal) & int.MaxValue) % 64;

    private static async Task AssertProcessLocksReleasedAsync(IReadOnlyCollection<string> scopeKeys) {
        foreach (var scopeKey in scopeKeys) {
            await using var successor = CreateInMemoryContext();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await using var scope = await SerializableMutationScope.BeginAsync(successor, scopeKey, timeout.Token);
            await scope.CommitAsync(timeout.Token);
        }
    }

    private sealed class AdvisoryFailureInterceptor : DbCommandInterceptor {
        public InvalidOperationException Failure { get; } = new("Injected advisory acquisition failure.");
        public IDbContextTransaction? Transaction { get; private set; }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            if (command.CommandText.Contains("pg_advisory_xact_lock", StringComparison.Ordinal)) {
                Transaction = eventData.Context!.Database.CurrentTransaction;
                throw Failure;
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class DisposeFailureTransactionFactory : RelationalTransactionFactory {
        public DisposeFailureTransactionFactory(RelationalTransactionFactoryDependencies dependencies) : base(dependencies) {
        }

        public override RelationalTransaction Create(IRelationalConnection connection, DbTransaction transaction,
            Guid transactionId, IDiagnosticsLogger<DbLoggerCategory.Database.Transaction> logger, bool transactionOwned) =>
            new DisposeFailureTransaction(connection, transaction, transactionId, logger, transactionOwned,
                Dependencies.SqlGenerationHelper);
    }

    private sealed class DisposeFailureTransaction(IRelationalConnection connection, DbTransaction transaction,
        Guid transactionId, IDiagnosticsLogger<DbLoggerCategory.Database.Transaction> logger, bool transactionOwned,
        ISqlGenerationHelper sqlGenerationHelper)
        : RelationalTransaction(connection, transaction, transactionId, logger, transactionOwned, sqlGenerationHelper) {
        public InvalidOperationException Failure { get; } = new("Injected transaction disposal failure.");
        public int DisposeCalls { get; private set; }

        public override async ValueTask DisposeAsync() {
            DisposeCalls++;
            await base.DisposeAsync();
            if (DisposeCalls == 1) {
                throw Failure;
            }
        }
    }
}
