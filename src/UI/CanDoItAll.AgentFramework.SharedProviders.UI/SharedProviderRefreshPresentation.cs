namespace CanDoItAll.AgentFramework.SharedProviders.UI;

public sealed record SharedProviderRefreshOrigin(Guid ViewId, long Activation, Guid ProviderId);
public sealed record SharedProviderRefreshPresentation(SharedProviderRefreshOrigin Origin, bool Busy, bool PendingSource,
    Guid? PendingDelivery, string? Message, bool Failed);
public interface ISharedProviderRefreshView {
    SharedProviderRefreshPresentation Presentation { get; }
    Task RefreshAsync(SharedProviderRefreshOrigin origin);
    Task RetryDeliveryAsync(SharedProviderRefreshOrigin origin, Guid attemptId);
}
