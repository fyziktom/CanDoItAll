namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

public interface IProviderProfileCommitObserver
{
    Task ProviderSavedAsync(
        Guid providerId,
        CancellationToken cancellationToken = default);

    Task ProviderDeletedAsync(
        Guid providerId,
        CancellationToken cancellationToken = default);
}
