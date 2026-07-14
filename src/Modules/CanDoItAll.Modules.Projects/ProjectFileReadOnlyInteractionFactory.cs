using CanDoItAll.AppComponents.FileTools;
using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.FileInteraction.Components;
using CanDoItAll.FileTools.Integration;

namespace CanDoItAll.Modules.Projects;

internal sealed class ProjectFileReadOnlyInteractionFactory(
    IFileToolsBrowseItemActivator itemActivator,
    IFileToolsKnownFileSessionFactory knownFileSessionFactory,
    IFileToolsKnownFileSessionReleaser knownFileSessionReleaser,
    FileInteractionComponentComposition interactionComposition)
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
            var request = new FileInteractionRequest(
                activation.Request.File,
                activation.FileName,
                FileInteractionMode.View,
                activation.MediaType,
                activation.Size);
            if (!FileInteractionDefaultActivationPolicy.ShouldOpenInternally(request, interactionComposition.Core))
            {
                throw new FileBrowserProviderException(new FileBrowserError(
                    FileBrowserErrorCode.Unsupported,
                    "No FileInteraction viewer is registered for this project file."));
            }

            FileToolsKnownFileSession session = await knownFileSessionFactory.CreateAsync(
                activation.Request,
                cancellationToken);
            request = new FileInteractionRequest(
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
