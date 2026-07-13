using CanDoItAll.FileTools.FileInteraction;

namespace CanDoItAll.FileTools.Integration;

public sealed record AuthorizedFileHttpContent(
    Stream Stream,
    string ContentType,
    string DisplayName,
    long? Length);

public interface IAuthorizedFileHttpContentService
{
    ValueTask<AuthorizedFileHttpContent> OpenAsync(
        string handle,
        FileAccessOperation operation,
        CancellationToken cancellationToken = default);
}

internal sealed class AuthorizedFileHttpContentService(
    IStorageFileAccessAuthorizationCoordinator coordinator,
    IFileAccessContextProvider contextProvider,
    AuthorizedFileContentSource contentSource) : IAuthorizedFileHttpContentService
{
    public async ValueTask<AuthorizedFileHttpContent> OpenAsync(
        string handle,
        FileAccessOperation operation,
        CancellationToken cancellationToken = default)
    {
        if (operation is not FileAccessOperation.View and not FileAccessOperation.Download)
        {
            throw new ArgumentOutOfRangeException(nameof(operation));
        }

        var file = new FileReference(AuthorizedFileReference.SourceId, handle);
        FileAccessContext context = await contextProvider.GetCurrentAsync(cancellationToken);
        AuthorizedStorageFile authorized = await coordinator.ResolveAsync(
            file,
            context,
            operation,
            cancellationToken);
        FileContentLease lease = await contentSource
            .For(file, operation)
            .OpenReadAsync(new FileContentReadRequest(file), cancellationToken);
        return new AuthorizedFileHttpContent(
            lease.Stream,
            authorized.Reference.ContentType,
            authorized.Reference.DisplayName,
            lease.Length);
    }
}
