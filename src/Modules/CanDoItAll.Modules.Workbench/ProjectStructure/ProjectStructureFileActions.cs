using CanDoItAll.Components.CanvasLib;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

internal static class ProjectStructureFileActions
{
    public const string BrowseFilesId = "browse-files";
    public const string BrowseFilesLabel = "Browse files";
    public const string BrowseFilesIcon = "folder_open";
    public const string BrowseFilesTone = "primary";

    public static bool IsBrowseFiles(string? actionId)
        => string.Equals(actionId, BrowseFilesId, StringComparison.Ordinal);

    public static bool CanBrowseFiles(ProjectStructureNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (node.ProjectRole != ProjectStructureProjectRole.None)
        {
            return true;
        }

        return node.ObjectType == ProjectObjectType.Infrastructure &&
               node.NodeReferences?.InfrastructureStorageCatalogId is not null;
    }

    public static ProjectStructureNodeActionDescriptor CreateDescriptor()
        => new(
            BrowseFilesId,
            BrowseFilesLabel,
            "Project toolbar and node context menu",
            "Browses the authorized project or storage-backed node collection in the canvas file window.");

    public static CanvasWorkbenchAction CreateCanvasAction()
        => new()
        {
            ActionId = BrowseFilesId,
            Label = BrowseFilesLabel,
            MenuLabel = "Files",
            Description = "Browse the authorized project or storage-backed node collection.",
            Icon = BrowseFilesIcon,
            Tone = BrowseFilesTone
        };
}
