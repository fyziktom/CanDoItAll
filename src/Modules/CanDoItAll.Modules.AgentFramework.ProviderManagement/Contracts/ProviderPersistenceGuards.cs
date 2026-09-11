using CanDoItAll.Infrastructure.Persistence;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

public interface IProviderProfileDeletionGuard
{
    Task EnsureCanDeleteAsync(
        ProvidersDbContext dbContext,
        Guid providerProfileId,
        CancellationToken cancellationToken);
}

public sealed record ProviderDatabaseTransferInspection(
    bool SourceHasSharedProviderReferences,
    bool TargetHasSharedProviderReferences,
    bool TargetUsesTransferredSecret);

public interface IProviderDatabaseTransferGuard {
    Task<string?> FindBlockReasonAsync(
        ProviderDatabaseTransferInspection inspection,
        CancellationToken cancellationToken);
}
