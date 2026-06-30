using CanDoItAll.Components.CanvasLib;

namespace CanDoItAll.Modules.Workbench.CanvasAdapters;

internal static class ProjectStructureNodeAnnotationBuilder
{
    public static IReadOnlyList<CanvasWorkbenchAnnotation> Build(ProjectStructureNode node)
    {
        var annotations = new List<CanvasWorkbenchAnnotation>
        {
            new()
            {
                Id = $"{node.Id}:copy-id",
                Kind = "clipboard",
                Tone = "neutral",
                Label = "ID",
                Description = "Copy this node id to the clipboard.",
                ActionId = "copy-id"
            },
            new()
            {
                Id = $"{node.Id}:copy-info",
                Kind = "clipboard",
                Tone = "primary",
                Label = "INF",
                Description = "Copy this node as type_title:id-hash.",
                ActionId = "copy-info"
            },
            new()
            {
                Id = $"{node.Id}:copy-tree",
                Kind = "clipboard",
                Tone = "info",
                Label = "Tree",
                Description = "Copy this node and descendants as type_title:id-hash entries.",
                ActionId = "copy-subtree-ids"
            }
        };

        annotations.AddRange(ProjectStructureValidationOverlay.BuildNodeAnnotations(node));
        return annotations;
    }
}
