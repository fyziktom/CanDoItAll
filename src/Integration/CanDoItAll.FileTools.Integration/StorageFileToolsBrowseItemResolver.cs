using CanDoItAll.FileTools.FileBrowser;

namespace CanDoItAll.FileTools.Integration;

internal sealed class StorageFileToolsBrowseItemResolver(
    IFileToolsBrowseSessionFactory browseSessionFactory)
{
    public async ValueTask<StorageFileBrowserProvider> ResolveProviderAsync(
        FileToolsSemanticScope scope,
        FileBrowserItemKey itemKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
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

        return selectedProvider ?? throw SourceUnavailable();
    }

    private static FileAccessDeniedException SourceUnavailable()
        => new(
            FileAccessFailureCode.InvalidHandle,
            "The selected file source is no longer available in this semantic scope.");
}
