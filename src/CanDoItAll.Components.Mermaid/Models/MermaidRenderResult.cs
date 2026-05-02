namespace CanDoItAll.Components.Mermaid;

public sealed class MermaidRenderResult
{
    public bool Succeeded { get; set; }

    public string? DiagramId { get; set; }

    public string? SvgElementId { get; set; }

    public int NodeCount { get; set; }

    public MermaidRenderError? Error { get; set; }
}
