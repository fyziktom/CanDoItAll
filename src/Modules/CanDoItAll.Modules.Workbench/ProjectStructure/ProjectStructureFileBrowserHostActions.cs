using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.FileBrowser.Components;

namespace CanDoItAll.Modules.Workbench;

internal static class ProjectStructureFileBrowserActionIds
{
    public const string Preview = "host:preview";
}

internal static class ProjectStructureFileBrowserHostActions
{
    public static ValueTask<IReadOnlyList<FileBrowserActionDescriptor>> GetAdditionalActionsAsync(
        FileBrowserHostActionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<IReadOnlyList<FileBrowserActionDescriptor>>(
        [
            new FileBrowserActionDescriptor(
                ProjectStructureFileBrowserActionIds.Preview,
                "Preview here",
                "visibility",
                description: "Open the governed read-only preview inside CanDoItAll.")
        ]);
    }
}
