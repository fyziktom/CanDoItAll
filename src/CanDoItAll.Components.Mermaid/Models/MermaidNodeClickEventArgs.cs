namespace CanDoItAll.Components.Mermaid;

public sealed class MermaidNodeClickEventArgs : EventArgs
{
    public string? NodeId { get; set; }

    public string? Text { get; set; }

    public string? SvgElementId { get; set; }

    public string? TagName { get; set; }

    public string? ClassName { get; set; }

    public string? DiagramId { get; set; }
}
