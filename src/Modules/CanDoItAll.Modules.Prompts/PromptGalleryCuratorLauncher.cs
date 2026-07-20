namespace CanDoItAll.Modules.Prompts;

public sealed record PromptGalleryCuratorPresentation(
    string Name,
    string? AvatarImageUrl);

public interface IPromptGalleryCuratorLauncher
{
    bool IsAvailable { get; }

    IPromptGalleryCuratorContextLease ActivateContext();

    Task<PromptGalleryCuratorPresentation> GetPresentationAsync(
        CancellationToken cancellationToken = default);

    Task OpenAsync(CancellationToken cancellationToken = default);
}

public interface IPromptGalleryCuratorContextLease : IDisposable
{
    void SynchronizeNavigation();
}

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
