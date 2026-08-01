namespace CanDoItAll.Modules.Workspace;

public interface IWorkspaceProviderProfileCommitObserver
{
    Task ProviderSavedAsync(
        Guid providerId,
        CancellationToken cancellationToken = default);

    Task ProviderDeletedAsync(
        Guid providerId,
        CancellationToken cancellationToken = default);
}
