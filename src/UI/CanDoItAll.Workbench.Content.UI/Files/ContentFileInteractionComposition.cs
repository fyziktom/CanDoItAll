using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.FileInteraction.Components;

namespace CanDoItAll.Workbench.Content.UI;

public static class ContentFileInteractionProfileIds {
    public const string Mermaid = "workbench-mermaid";
}

public static class ContentFileInteractionComposition {
    public static FileInteractionComponentBuilder AddContentMermaid(
        this FileInteractionComponentBuilder builder) {
        ArgumentNullException.ThrowIfNull(builder);

        return builder
            .AddProfile(new FileInteractionProfileDescriptor(
                ContentFileInteractionProfileIds.Mermaid,
                FileInteractionCapabilities.View
                    | FileInteractionCapabilities.Edit
                    | FileInteractionCapabilities.Preview
                    | FileInteractionCapabilities.Save
                    | FileInteractionCapabilities.Undo
                    | FileInteractionCapabilities.Redo,
                extensions: [".mmd", ".mermaid"],
                mediaTypes: ["text/vnd.mermaid"],
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
                ContentFileInteractionProfileIds.Mermaid,
                FileInteractionMode.View,
                typeof(CanDoItAll.Modules.Workbench.WorkbenchMermaidFileView),
                FileInteractionContentKind.Text))
            .AddRenderer(new FileInteractionRendererDescriptor(
                "workbench-mermaid-edit",
                ContentFileInteractionProfileIds.Mermaid,
                FileInteractionMode.Edit,
                typeof(TextFileEditor),
                FileInteractionContentKind.Text));
    }

}
