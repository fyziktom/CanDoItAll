using CanDoItAll.Infrastructure.ControlPlane;

namespace CanDoItAll.AgentFramework.ProviderHistory.Persistence;

public interface IHistoryTransferParticipant {
    HistorySourceKind Kind { get; }
    Task ValidateTargetAsync(DatabaseTransferContext context, CancellationToken cancellationToken);
    Task<int> CopyAsync(DatabaseTransferContext context, CancellationToken cancellationToken);
}
