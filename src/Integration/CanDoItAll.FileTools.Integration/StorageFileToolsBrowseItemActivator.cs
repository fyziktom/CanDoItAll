using CanDoItAll.FileTools.FileBrowser;

namespace CanDoItAll.FileTools.Integration;

internal sealed class StorageFileToolsBrowseItemActivator(
    IFileToolsBrowseSessionFactory browseSessionFactory,
    IFileAccessContextProvider contextProvider,
    IStorageFileAccessAuthorizationCoordinator authorizationCoordinator) : IFileToolsBrowseItemActivator
{
    public async ValueTask<FileToolsKnownFileActivation> ActivateAsync(
        FileToolsSemanticScope scope,
        FileBrowserItemKey itemKey,
        FileToolsKnownFileIntent intent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (!Enum.IsDefined(intent))
        {
            throw new ArgumentOutOfRangeException(nameof(intent));
        }

        FileToolsBrowseSession currentSession = await browseSessionFactory.CreateAsync(scope, cancellationToken);
        StorageFileBrowserProvider? selectedProvider = null;
        foreach (IFileBrowserProvider provider in currentSession.Providers)
        {
            if (provider is not StorageFileBrowserProvider storageProvider ||
                storageProvider.Descriptor.Id != itemKey.SourceId)
            {
                continue;
            }

            if (selectedProvider is not null)
            {
                throw SourceUnavailable();
            }

            selectedProvider = storageProvider;
        }

        if (selectedProvider is null)
        {
            throw SourceUnavailable();
        }

        FileAccessContext context = await contextProvider.GetCurrentAsync(cancellationToken);
        FileAccessOperation operations = intent == FileToolsKnownFileIntent.Edit
            ? FileAccessOperation.View | FileAccessOperation.Edit
            : FileAccessOperation.View;
        AuthorizedBrowserFile authorized = await selectedProvider.AuthorizeItemAsync(
            itemKey,
            context,
            scope,
            operations,
            authorizationCoordinator,
            cancellationToken);
        return new FileToolsKnownFileActivation(
            new FileToolsKnownFileRequest(scope, authorized.File, intent),
            authorized.FileName,
            authorized.MediaType,
            authorized.Size);
    }

    private static FileAccessDeniedException SourceUnavailable()
        => new(
            FileAccessFailureCode.InvalidHandle,
            "The selected file source is no longer available in this semantic scope.");
}
