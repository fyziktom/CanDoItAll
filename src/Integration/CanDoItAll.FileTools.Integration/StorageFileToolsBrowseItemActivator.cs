using CanDoItAll.FileTools.FileBrowser;

namespace CanDoItAll.FileTools.Integration;

internal sealed class StorageFileToolsBrowseItemActivator(
    StorageFileToolsBrowseItemResolver itemResolver,
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

        StorageFileBrowserProvider selectedProvider = await itemResolver.ResolveProviderAsync(
            scope,
            itemKey,
            cancellationToken);

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
}
