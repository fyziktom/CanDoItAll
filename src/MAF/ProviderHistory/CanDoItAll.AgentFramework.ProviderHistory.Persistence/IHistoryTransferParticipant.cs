using CanDoItAll.Infrastructure.ControlPlane;

namespace CanDoItAll.AgentFramework.ProviderHistory.Persistence;

public interface IHistoryTransferParticipant {
    HistorySourceKind Kind { get; }
    Task ValidateTargetAsync(DatabaseTransferOwnerRequest transfer, CancellationToken cancellationToken);
    Task<int> CopyAsync(DatabaseTransferOwnerRequest transfer, CancellationToken cancellationToken);
}
