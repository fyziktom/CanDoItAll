using CanDoItAll.Modules.AgentFramework.ProviderManagement;

namespace CanDoItAll.Modules.AgentFramework;

public enum SharedProviderDeliveryDisposition { Acknowledged, Pending, InProgress, NotCurrent }

public sealed class SharedProviderChangeDelivery(Guid attemptId, SharedProviderChange change) {
    public Guid AttemptId { get; } = attemptId;
    public SharedProviderChange Change { get; } = change;
    public bool IsAcknowledged { get; private set; }
    internal bool InProgress { get; set; }

    public async Task ReconcileAsync(Func<Task> reconcile) {
        if (IsAcknowledged) {
            return;
        }
        await reconcile();
        IsAcknowledged = true;
    }

    internal void Acknowledge() => IsAcknowledged = true;
}
