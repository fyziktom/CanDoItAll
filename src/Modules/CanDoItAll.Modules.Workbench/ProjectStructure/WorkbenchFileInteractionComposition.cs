using CanDoItAll.FileTools.FileInteraction.Components;
using CanDoItAll.Workbench.Content.UI;

namespace CanDoItAll.Modules.Workbench;

internal static class WorkbenchFileInteractionProfileIds {
    public const string Mermaid = ContentFileInteractionProfileIds.Mermaid;
}

internal static class WorkbenchFileInteractionComposition {
    public static FileInteractionComponentBuilder AddWorkbenchMermaid(this FileInteractionComponentBuilder builder)
        => builder.AddContentMermaid();
}
