using CanDoItAll.FileTools.FileInteraction;

namespace CanDoItAll.FileTools.Integration;

internal sealed class AuthorizedFileToolsKnownFileSessionReleaser(
    IStorageFileAccessAuthorizationCoordinator coordinator) : IFileToolsKnownFileSessionReleaser
{
    public ValueTask ReleaseAsync(
        FileReference file,
        CancellationToken cancellationToken = default)
        => coordinator.RevokeAsync(file, cancellationToken);
}
