using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

public interface IProviderProfileDeletionGuard
{
    Task EnsureCanDeleteAsync(
        AppDbContext dbContext,
        Guid providerProfileId,
        CancellationToken cancellationToken);
}

public interface IProviderDatabaseTransferGuard
{
    Task<string?> FindBlockReasonAsync(
        DatabaseTransferContext context,
        IReadOnlyCollection<Guid> transferredSecretIds,
        CancellationToken cancellationToken);
}
