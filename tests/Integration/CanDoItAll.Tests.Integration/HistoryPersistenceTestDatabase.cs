using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.ProviderHistory.Persistence;
using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Tests.Support;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Integration;

internal sealed class HistoryPersistenceTestDatabase : IAsyncDisposable {
    private readonly PostgresTestDatabaseLease lease;
    private HistoryPersistenceTestDatabase(PostgresTestDatabaseLease lease) {
        this.lease = lease;
        Factory = new(lease.CreateAppDbContextOptions());
        Text = new(new EphemeralDataProtectionProvider(), Secrets);
        Details = new(Text, Clock, NullLogger<HistoryDetailStore>.Instance);
        HostLease = new(Factory, Clock);
        Capture = new(Factory, Runtime, Runtime, Details, HostLease);
        Projection = new(Factory);
        Outbox = new(Clock);
        Processor = new(Factory, Clock, NullLogger<HistoryOutboxProcessor>.Instance);
        Policy = new(Factory, Access, Clock,
            new(Access, Reads, Clock, NullLogger<HistoryAuthorizedOperation>.Instance), Runtime, Runtime);
    }

    internal TestFactory Factory { get; }
    internal TestClock Clock { get; } = new();
    internal TestSecrets Secrets { get; } = new();
    internal TestRuntime Runtime { get; } = new();
    internal TestAccess Access { get; } = new();
    internal HistoryReadConcurrency Reads { get; } = new();
    internal HistoryTextProtector Text { get; }
    internal HistoryDetailStore Details { get; }
    internal HistoryHostLeaseStore HostLease { get; }
    internal HistoryCaptureStore Capture { get; }
    internal HistoryProjectionWriter Projection { get; }
    internal HistoryOutboxWriter Outbox { get; }
    internal HistoryOutboxProcessor Processor { get; }
    internal HistoryPolicyStore Policy { get; }
    internal HistoryPartition Partition { get; private set; }
    internal HistoryMaintenanceContext Maintenance => new(Partition, Runtime.GetSnapshot(), Runtime);

    internal static async Task<HistoryPersistenceTestDatabase> CreateAsync() {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        var result = new HistoryPersistenceTestDatabase(PostgresTestDatabaseLease.Create("provider-history"));
        try {
            await using var db = result.Factory.CreateDbContext();
            await db.Database.EnsureCreatedAsync();
            result.Partition = await new HistoryPartitionStore(result.Factory).GetAsync(default);
            result.Access.Context = new(result.Partition, new(0, 0),
                new(HistoryAuthenticationKind.TrustedLocalOperator), null);
            return result;
        } catch {
            await result.DisposeAsync();
            throw;
        }
    }

    internal HistoryAttemptStart Start(HistoryPolicySnapshot? policy = null) => new(
        HistoryEntryId.New(), Partition, new(Runtime.Generation, 0),
        ProviderRequestId.New(), ProviderAttemptId.New(), Clock.GetUtcNow(),
        new(new ProviderIdentity(Guid.NewGuid()), "Fixture", "OpenAI", new("exact-model"), new("exact-model")),
        HistoryOperation.CompleteChat, HistoryWorkload.Direct,
        new(HistoryAuthenticationKind.ManagedCredential, new(Guid.NewGuid()), "fixture-issuer", "fixture-subject"),
        policy ?? new(new(), 0));

    internal HistoryAttemptCompletion Completion() => new(HistoryOutcome.Succeeded, Clock.GetUtcNow().AddSeconds(1),
        new(HistoryUsageState.Complete, 10, 5, 0, 0, 0),
        new(HistoryPriceState.CalculatedAtExecution, 0.01m, "USD", "fixture-hash", "v1"));

    public ValueTask DisposeAsync() {
        HostLease.Dispose();
        Reads.Dispose();
        return lease.DisposeAsync();
    }

    internal sealed class TestFactory(DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext> {
        public TestFactory WithInterceptor(Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor interceptor) =>
            new(new DbContextOptionsBuilder<AppDbContext>(options).AddInterceptors(interceptor).Options);

        public AppDbContext CreateDbContext() => new(options);
        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
    }

    internal sealed class TestClock : TimeProvider {
        internal DateTimeOffset Now { get; set; } = new(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    internal sealed class TestRuntime : IDatabaseRuntimeState, IDatabaseRuntimeWriteFence {
        internal long Generation { get; set; }
        public DatabaseRuntimeSnapshot GetSnapshot() => new(null, "fixture", Generation);
        public void MarkCurrentProfile(ResolvedDatabaseProfile profile) => throw new NotSupportedException();
        public Task<T> ExecuteAsync<T>(DatabaseRuntimeSnapshot expected, Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
            => expected.Generation == Generation ? operation(cancellationToken) : throw new DatabaseRuntimeProfileChangedException();
    }

    internal sealed class TestAccess : IProviderHistoryAccess {
        internal HistoryAccessContext Context { get; set; } = null!;
        internal bool Denied { get; set; }
        public Task<HistoryAccessContext> AuthorizeAsync(HistoryPermission permission, CancellationToken cancellationToken)
            => Denied ? throw new ProviderHistoryException(HistoryFailure.Denied, "Fixture denied.") : Task.FromResult(Context);
        public Task EnsureCurrentAsync(HistoryAccessContext context, HistoryPermission permission, CancellationToken cancellationToken)
            => Denied ? throw new ProviderHistoryException(HistoryFailure.Denied, "Fixture denied.")
                : context != Context ? throw new ProviderHistoryException(HistoryFailure.StaleContext, "Fixture context changed.") : Task.CompletedTask;
        public Task AuthorizeOwnerAsync(HistoryAccessContext context, CanonicalEvidenceReference owner, CancellationToken cancellationToken)
            => EnsureCurrentAsync(context, HistoryPermission.ReadContent, cancellationToken);
    }

    internal sealed class TestSecrets : IProviderHistorySecrets {
        internal string Current { get; set; } = "fixture-secret-token";
        public Task<IReadOnlyList<string>> GetKnownSecretsAsync(ProviderIdentity provider, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<string>>([Current]);
    }
}
