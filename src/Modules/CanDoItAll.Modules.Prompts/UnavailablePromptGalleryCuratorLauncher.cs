namespace CanDoItAll.Modules.Prompts;

internal sealed class UnavailablePromptGalleryCuratorLauncher : IPromptGalleryCuratorLauncher
{
    public bool IsAvailable => false;

    public IPromptGalleryCuratorContextLease ActivateContext()
        => throw new InvalidOperationException("The Prompts Curator integration is not available in this host.");

    public Task<PromptGalleryCuratorPresentation> GetPresentationAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new InvalidOperationException("The Prompts Curator integration is not available in this host.");
    }

    public Task OpenAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new InvalidOperationException("The Prompts Curator integration is not available in this host.");
    }
}
