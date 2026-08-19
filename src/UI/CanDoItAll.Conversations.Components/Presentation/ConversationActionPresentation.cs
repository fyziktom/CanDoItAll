namespace CanDoItAll.Conversations.Components.Presentation;

public sealed record ConversationActionPresentation
{
    public ConversationActionPresentation(
        ConversationPresentationKey key,
        string label,
        string icon,
        string? testId = null,
        bool isDisabled = false,
        ConversationActionStyle style = ConversationActionStyle.Primary)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentException.ThrowIfNullOrWhiteSpace(icon);
        Key = key;
        Label = label;
        Icon = icon;
        TestId = testId;
        IsDisabled = isDisabled;
        Style = style;
    }

    public ConversationPresentationKey Key { get; }

    public string Label { get; }

    public string Icon { get; }

    public string? TestId { get; }

    public bool IsDisabled { get; }

    public ConversationActionStyle Style { get; }
}
