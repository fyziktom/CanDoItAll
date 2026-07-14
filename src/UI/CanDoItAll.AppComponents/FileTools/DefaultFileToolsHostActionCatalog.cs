using System.Collections.Frozen;
using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.FileBrowser.Components;
using CanDoItAll.FileTools.Integration;

namespace CanDoItAll.AppComponents.FileTools;

public delegate ValueTask<IReadOnlyList<FileBrowserActionDescriptor>> FileToolsAdditionalHostActionProvider(
    FileBrowserHostActionContext context,
    CancellationToken cancellationToken);

public static class FileToolsHostActionCapabilityCapture
{
    public static IReadOnlyDictionary<FileBrowserSourceId, FileToolsBrowseSourceActionAvailability> Capture(
        IEnumerable<IFileBrowserProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);
        var captured = new Dictionary<FileBrowserSourceId, FileToolsBrowseSourceActionAvailability>();
        foreach (IFileBrowserProvider provider in providers)
        {
            ArgumentNullException.ThrowIfNull(provider);
            FileToolsBrowseSourceActionAvailability availability =
                provider is IFileToolsBrowseSourceActionCapabilities capabilities
                    ? capabilities.ActionAvailability
                    : default;
            if (!captured.TryAdd(provider.Descriptor.Id, availability))
            {
                throw new InvalidOperationException(
                    $"File source '{provider.Descriptor.Id}' declares host action availability more than once.");
            }
        }

        return captured.ToFrozenDictionary();
    }

    public static FileToolsBrowseSourceActionAvailability Resolve(IFileBrowserProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        return provider is IFileToolsBrowseSourceActionCapabilities capabilities
            ? capabilities.ActionAvailability
            : default;
    }
}

public sealed class DefaultFileToolsHostActionCatalog(
    Func<FileBrowserSourceId, FileToolsBrowseSourceActionAvailability> resolveAvailability,
    bool isLocalLaunchAvailable,
    FileToolsAdditionalHostActionProvider? additionalActions = null) : IFileBrowserHostActionCatalog
{
    private readonly Func<FileBrowserSourceId, FileToolsBrowseSourceActionAvailability> availabilityResolver =
        resolveAvailability ?? throw new ArgumentNullException(nameof(resolveAvailability));

    public async ValueTask<IReadOnlyList<FileBrowserActionDescriptor>> GetActionsAsync(
        FileBrowserHostActionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        if (context.Item.IsContainer ||
            !context.Item.Supports(FileBrowserItemCapabilities.Preview))
        {
            return [];
        }

        FileToolsBrowseSourceActionAvailability availability = availabilityResolver(context.Item.Key.SourceId);
        var actions = new List<FileBrowserActionDescriptor>(4);
        if (availability.SupportsLocalOpen && isLocalLaunchAvailable)
        {
            actions.Add(new FileBrowserActionDescriptor(
                FileBrowserActionIds.Open,
                "Open in preferred app",
                "open_in_new",
                FileBrowserActionTone.Primary,
                isPrimary: true,
                "Open with the configured application or the operating-system default."));
        }

        if (additionalActions is not null)
        {
            IReadOnlyList<FileBrowserActionDescriptor> additional =
                await additionalActions(context, cancellationToken)
                ?? throw new InvalidOperationException("The additional host action provider returned null.");
            actions.AddRange(additional);
        }

        if (availability.SupportsLocalOpen && isLocalLaunchAvailable)
        {
            actions.Add(new FileBrowserActionDescriptor(
                FileToolsBrowseHostActionIds.OpenContainingFolder,
                "Show in folder",
                "folder_open",
                description: "Open the exact folder containing this file."));
        }

        if (availability.SupportsDownload)
        {
            actions.Add(new FileBrowserActionDescriptor(
                FileBrowserActionIds.Download,
                "Download",
                "download",
                description: "Download a newly authorized copy through the browser."));
        }

        if (actions.Any(action => action is null) ||
            actions.Select(action => action.Id).Distinct(StringComparer.Ordinal).Count() != actions.Count)
        {
            throw new InvalidOperationException("The combined host action catalog contains invalid or duplicate actions.");
        }

        return actions;
    }
}
