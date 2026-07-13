using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.Integration;

namespace CanDoItAll.Modules.Projects;

internal sealed class ProjectFileReadOnlyInteractionFactory(
    IFileToolsBrowseItemActivator itemActivator,
    IFileToolsKnownFileSessionFactory knownFileSessionFactory,
    IFileToolsKnownFileSessionReleaser knownFileSessionReleaser)
{
    public async ValueTask<ProjectFilesPilotInteraction> CreateAsync(
        FileToolsSemanticScope scope,
        FileBrowserItemKey itemKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        FileToolsKnownFileActivation activation = await itemActivator.ActivateAsync(
            scope,
            itemKey,
            FileToolsKnownFileIntent.ReadOnly,
            cancellationToken);
        try
        {
            if (!ProjectFileBrowserPolicy.IsSupportedTextFile(activation.FileName, activation.MediaType))
            {
                throw new FileBrowserProviderException(new FileBrowserError(
                    FileBrowserErrorCode.Unsupported,
                    "Project files open only supported Markdown and plain-text content."));
            }

            FileToolsKnownFileSession session = await knownFileSessionFactory.CreateAsync(
                activation.Request,
                cancellationToken);
            var request = new FileInteractionRequest(
                session.File,
                activation.FileName,
                FileInteractionMode.View,
                activation.MediaType,
                activation.Size);
            return new ProjectFilesPilotInteraction(request, session, knownFileSessionReleaser);
        }
        catch
        {
            await knownFileSessionReleaser.ReleaseAsync(activation.Request.File, CancellationToken.None);
            throw;
        }
    }
}
