using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.FileInteraction.Components;

namespace CanDoItAll.Modules.Workbench;

internal static class WorkbenchFileInteractionProfileIds
{
    public const string Mermaid = "workbench-mermaid";
    public const string SpreadsheetPreview = "workbench-spreadsheet-preview";
}

internal static class WorkbenchFileInteractionRendererIds
{
    public const string SpreadsheetPreviewView = "workbench-spreadsheet-preview-view";
}

internal static class WorkbenchFileInteractionComposition
{
    public static FileInteractionComponentBuilder AddWorkbenchMermaid(
        this FileInteractionComponentBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder
            .AddProfile(new FileInteractionProfileDescriptor(
                WorkbenchFileInteractionProfileIds.Mermaid,
                FileInteractionCapabilities.View
                    | FileInteractionCapabilities.Edit
                    | FileInteractionCapabilities.Preview
                    | FileInteractionCapabilities.Save
                    | FileInteractionCapabilities.Undo
                    | FileInteractionCapabilities.Redo,
                extensions: [".mmd", ".mermaid"],
                mediaTypes: [ProjectStructureFileInteractionPolicy.MermaidMediaType],
                priority: 200,
                preview: new FilePreviewOptions(
                    enabled: true,
                    debounce: TimeSpan.FromMilliseconds(400),
                    splitByDefault: true,
                    placement: FilePreviewPlacement.Beside),
                history: new FileHistoryOptions(
                    maxEntries: 50,
                    maxBytes: 2 * 1024 * 1024)))
            .AddRenderer(new FileInteractionRendererDescriptor(
                "workbench-mermaid-view",
                WorkbenchFileInteractionProfileIds.Mermaid,
                FileInteractionMode.View,
                typeof(WorkbenchMermaidFileView),
                FileInteractionContentKind.Text))
            .AddRenderer(new FileInteractionRendererDescriptor(
                "workbench-mermaid-edit",
                WorkbenchFileInteractionProfileIds.Mermaid,
                FileInteractionMode.Edit,
                typeof(TextFileEditor),
                FileInteractionContentKind.Text));
    }

    public static FileInteractionComponentBuilder AddWorkbenchSpreadsheetPreview(
        this FileInteractionComponentBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder
            .AddProfile(new FileInteractionProfileDescriptor(
                WorkbenchFileInteractionProfileIds.SpreadsheetPreview,
                FileInteractionCapabilities.View,
                extensions: [".xlsx"],
                mediaTypes: [ProjectStructureFileInteractionPolicy.XlsxMediaType],
                priority: 200))
            .AddRenderer(new FileInteractionRendererDescriptor(
                WorkbenchFileInteractionRendererIds.SpreadsheetPreviewView,
                WorkbenchFileInteractionProfileIds.SpreadsheetPreview,
                FileInteractionMode.View,
                typeof(WorkbenchSpreadsheetFileView),
                FileInteractionContentKind.Binary,
                contentRequirement: FileInteractionContentRequirement.FullContent));
    }
}
