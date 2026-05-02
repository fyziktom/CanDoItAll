namespace CanDoItAll.Components.Mermaid;

public sealed class MermaidDiagramOptions
{
    public string Theme { get; set; } = "default";

    public string SecurityLevel { get; set; } = "loose";

    public bool FlowchartUseMaxWidth { get; set; } = true;

    public bool HtmlLabels { get; set; } = true;

    public bool ArchitectureRandomize { get; set; }

    public Dictionary<string, object?> AdditionalConfig { get; set; } = [];
}
