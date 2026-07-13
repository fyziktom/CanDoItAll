namespace CanDoItAll.FileTools.Integration;

internal sealed class AuthorizedFileToolsKnownFileSessionFactory(
    IStorageFileAccessAuthorizationCoordinator coordinator,
    IFileAccessContextProvider contextProvider,
    AuthorizedFileContentSource contentSource,
    AuthorizedFileSaveTarget saveTarget) : IFileToolsKnownFileSessionFactory
{
    public async ValueTask<FileToolsKnownFileSession> CreateAsync(
        FileToolsKnownFileRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        FileAccessContext context = await contextProvider.GetCurrentAsync(cancellationToken);
        AuthorizedStorageFile authorized = await coordinator.ResolveAsync(
            request.File,
            context,
            FileAccessOperation.View,
            cancellationToken);
        if (authorized.Scope != request.Scope)
        {
            throw new FileAccessDeniedException(
                FileAccessFailureCode.ContextMismatch,
                "The file access handle does not belong to the requested semantic scope.");
        }

        if (request.Intent == FileToolsKnownFileIntent.Edit)
        {
            await coordinator.ResolveAsync(request.File, context, FileAccessOperation.Edit, cancellationToken);
        }

        return new FileToolsKnownFileSession(
            request.File,
            contentSource.For(request.File),
            request.Intent,
            request.Intent == FileToolsKnownFileIntent.Edit ? saveTarget.For(request.File) : null);
    }
}
