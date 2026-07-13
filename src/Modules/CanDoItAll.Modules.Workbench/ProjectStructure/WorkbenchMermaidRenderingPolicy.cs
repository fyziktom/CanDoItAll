using CanDoItAll.Components.Mermaid;

namespace CanDoItAll.Modules.Workbench;

internal static class WorkbenchMermaidRenderingPolicy
{
    public static MermaidDiagramOptions StrictOptions { get; } = new()
    {
        SecurityLevel = "strict",
        HtmlLabels = false,
        FlowchartUseMaxWidth = true,
        ArchitectureRandomize = false
    };
}
