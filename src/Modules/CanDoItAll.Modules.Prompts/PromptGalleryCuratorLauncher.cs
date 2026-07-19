namespace CanDoItAll.Modules.Prompts;

public interface IPromptGalleryCuratorLauncher
{
    bool IsAvailable { get; }

    IPromptGalleryCuratorContextLease ActivateContext();

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

    public Task OpenAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new InvalidOperationException("The Prompts Curator integration is not available in this host.");
    }
}
