namespace CanDoItAll.FileTools.Integration;

internal sealed class AuthorizedFileToolsKnownFileSessionFactory(
    IStorageFileAccessAuthorizationCoordinator coordinator,
    IFileAccessContextProvider contextProvider,
    AuthorizedFileContentSource contentSource,
    AuthorizedFileSaveTarget saveTarget) : IFileToolsKnownFileSessionFactory
{
    public async ValueTask<FileToolsKnownFileSession> CreateAsync(
        FileToolsKnownFileRequest request,
        CancellationToken cancellationToken = default,
        Func<CancellationToken, ValueTask>? authorizeRead = null,
        Func<CancellationToken, ValueTask>? authorizeWrite = null) {
        ArgumentNullException.ThrowIfNull(request);
        if (authorizeRead is not null) {
            await authorizeRead(cancellationToken);
        }
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
            contentSource.For(request.File, authorizeRead: authorizeRead),
            request.Intent,
            request.Intent == FileToolsKnownFileIntent.Edit ? saveTarget.For(request.File, authorizeWrite) : null);
    }
}
