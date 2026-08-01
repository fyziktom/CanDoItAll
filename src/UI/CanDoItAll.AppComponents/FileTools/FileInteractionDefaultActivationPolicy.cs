using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.FileInteraction;

namespace CanDoItAll.AppComponents.FileTools;

public static class FileInteractionDefaultActivationPolicy
{
    public static bool ShouldOpenInternally(
        FileBrowserItem item,
        FileInteractionCoreComposition composition)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(composition);
        if (item.IsContainer)
        {
            return false;
        }

        var request = new FileInteractionRequest(
            new FileReference(item.Key.SourceId.Value, item.Key.Value),
            item.Name,
            FileInteractionMode.View,
            item.MediaType,
            item.Size);

        return ShouldOpenInternally(request, composition);
    }

    public static bool ShouldOpenInternally(
        FileInteractionRequest request,
        FileInteractionCoreComposition composition)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(composition);
        FileInteractionResolution resolution = composition.Profiles.Resolve(request);

        return resolution.IsResolved &&
               resolution.Candidates is [FileInteractionProfileMatch { MatchKind: not FileInteractionMatchKind.Fallback }];
    }
}
