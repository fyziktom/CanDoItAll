namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

public interface IProviderProfileCommitObserver
{
    Task ProviderHealthChangedAsync(Guid providerId, CancellationToken cancellationToken = default)
        => ProviderSavedAsync(providerId, cancellationToken);

    Task ProviderSavedAsync(
        Guid providerId,
        CancellationToken cancellationToken = default);

    Task ProviderDeletedAsync(
        Guid providerId,
        CancellationToken cancellationToken = default);
}
