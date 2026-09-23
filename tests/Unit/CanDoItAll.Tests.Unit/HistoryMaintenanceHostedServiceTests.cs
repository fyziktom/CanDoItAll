using CanDoItAll.AgentFramework.ProviderHistory.Persistence;
using CanDoItAll.Infrastructure.ControlPlane;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit;

public sealed class HistoryMaintenanceHostedServiceTests
{
    [Fact]
    public async Task In_memory_profile_does_not_start_postgresql_history_maintenance()
    {
        using var service = new HistoryMaintenanceHostedService(
            factory: null!,
            runtime: null!,
            writeFence: null!,
            sources: [],
            sourceRunner: null!,
            partitions: null!,
            hostLease: null!,
            projection: null!,
            recovery: null!,
            retention: null!,
            database: new InMemoryCanonicalRuntimeDatabase(),
            lifetime: null!,
            clock: TimeProvider.System,
            logger: NullLogger<HistoryMaintenanceHostedService>.Instance);

        await service.StartAsync(CancellationToken.None);

        Assert.NotNull(service.ExecuteTask);
        await service.ExecuteTask.WaitAsync(TimeSpan.FromSeconds(1));
    }

    private sealed class InMemoryCanonicalRuntimeDatabase : ICanonicalRuntimeDatabase
    {
        public long Generation => 0;

        public ResolvedDatabaseProfile Profile { get; } = new(
            new DatabaseProfileRecord
            {
                ProviderKind = DatabaseProviderKind.InMemory,
                SourceKind = DatabaseProfileSourceKind.InMemory,
                InMemory = new InMemoryDatabaseProfileConnection()
            },
            DatabaseProfileResolutionSource.ExplicitOverride,
            "history-maintenance-in-memory");
    }
}
