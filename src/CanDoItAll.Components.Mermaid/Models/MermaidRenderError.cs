namespace CanDoItAll.Components.Mermaid;

public sealed class MermaidRenderError
{
    public string Message { get; set; } = "Mermaid could not render this diagram.";

    public int? Line { get; set; }

    public int? Column { get; set; }

    public string? Token { get; set; }

    public string? Text { get; set; }

    public string? Excerpt { get; set; }

    public IReadOnlyList<string> ExpectedTokens { get; set; } = [];

    public string? Raw { get; set; }

    public string LocationText => Line is null
        ? "Location unavailable"
        : Column is null
            ? $"Line {Line}"
            : $"Line {Line}, column {Column}";
}
