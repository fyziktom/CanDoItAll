namespace CanDoItAll.Conversations.Components.Presentation;

public sealed record PresentationBadge
{
    public PresentationBadge(
        string text,
        PresentationTone tone = PresentationTone.Default,
        string? icon = null,
        string? accessibleDescription = null,
        string? testId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        Text = text;
        Tone = tone;
        Icon = icon;
        AccessibleDescription = accessibleDescription;
        TestId = testId;
    }

    public string Text { get; }

    public PresentationTone Tone { get; }

    public string? Icon { get; }

    public string? AccessibleDescription { get; }

    public string? TestId { get; }
}
