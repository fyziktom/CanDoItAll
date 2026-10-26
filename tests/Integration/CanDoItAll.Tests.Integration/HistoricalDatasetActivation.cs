using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;

namespace CanDoItAll.Tests.Integration;

internal static class HistoricalDatasetActivation {
    internal static async Task<DatasetActivationBinding> SeedSharedRowsAsync(AppDbContext context,
        DatasetActivationService files, Guid profileId, DateTimeOffset openedAt,
        params FiniteOperationPoolId[] pools) {
        DatasetActivationBinding binding = new(new(Guid.NewGuid()), Guid.NewGuid(), 1, DatasetGuardProtocol.SharedRows);
        DatasetActivationPlan plan = new(Guid.NewGuid(), DatasetActivationKind.Bootstrap, binding, null);
        using var file = files.Acquire(profileId);
        await file.WritePreparedAsync(plan);
        context.Add(new DatasetLifecycleRecord {
            NamespaceId = binding.Namespace.Value, MaintenanceId = binding.MaintenanceId,
            SchemaVersion = 1, GuardProtocol = DatasetGuardProtocol.SharedRows, State = DatasetLifecycleState.Active
        });
        foreach (var pool in pools) {
            context.Add(new FiniteOperationStateRecord {
                NamespaceId = binding.Namespace.Value, PoolId = pool.Value, RetentionProtocolVersion = 1,
                CurrentEpoch = 1, RetiredThroughEpoch = 0
            });
            context.Add(new FiniteOperationEpochRecord {
                NamespaceId = binding.Namespace.Value, PoolId = pool.Value, Epoch = 1, State = FiniteOperationEpochState.Open,
                OpenedAtUtc = openedAt, AdmissionClosesAtUtc = openedAt.AddDays(1),
                MutationDeadlineMilliseconds = 30000, ReplayRetentionSeconds = 604800
            });
        }
        await context.SaveChangesAsync();
        await file.MarkActiveAsync(plan);
        return binding;
    }
}
