namespace CanDoItAll.Conversations.Components.Presentation;

public sealed record PresentationMetaItem
{
    public PresentationMetaItem(string value, string? label = null, string? tooltip = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
        Label = label;
        Tooltip = tooltip;
    }

    public string Value { get; }

    public string? Label { get; }

    public string? Tooltip { get; }

    public string DisplayText => string.IsNullOrWhiteSpace(Label)
        ? Value
        : $"{Label}: {Value}";
}
