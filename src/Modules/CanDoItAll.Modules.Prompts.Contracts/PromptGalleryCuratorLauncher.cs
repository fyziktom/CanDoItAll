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
