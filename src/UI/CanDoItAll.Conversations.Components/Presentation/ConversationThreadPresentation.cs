namespace CanDoItAll.Conversations.Components.Presentation;

public sealed record ConversationThreadPresentation
{
    public ConversationThreadPresentation(
        ConversationPresentationKey key,
        string title,
        DateTimeOffset updatedAtUtc,
        string updatedAtDisplay,
        string metadata,
        string preview,
        string? searchText = null,
        string? tooltipText = null,
        string? selectLabel = null,
        IReadOnlyList<PresentationBadge>? badges = null,
        bool isSelected = false,
        bool isBusy = false,
        bool isDisabled = false)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(updatedAtDisplay);
        ArgumentException.ThrowIfNullOrWhiteSpace(metadata);
        ArgumentException.ThrowIfNullOrWhiteSpace(preview);
        Key = key;
        Title = title;
        UpdatedAtUtc = updatedAtUtc;
        UpdatedAtDisplay = updatedAtDisplay;
        Metadata = metadata;
        Preview = preview;
        SearchText = string.IsNullOrWhiteSpace(searchText)
            ? string.Join(' ', title, metadata, preview)
            : searchText;
        TooltipText = string.IsNullOrWhiteSpace(tooltipText) ? preview : tooltipText;
        SelectLabel = string.IsNullOrWhiteSpace(selectLabel) ? $"Open thread {title}" : selectLabel;
        Badges = PresentationCollection.Snapshot(badges, nameof(badges));
        IsSelected = isSelected;
        IsBusy = isBusy;
        IsDisabled = isDisabled;
    }

    public ConversationPresentationKey Key { get; }

    public string Title { get; }

    public DateTimeOffset UpdatedAtUtc { get; }

    public string UpdatedAtDisplay { get; }

    public string Metadata { get; }

    public string Preview { get; }

    public string SearchText { get; }

    public string TooltipText { get; }

    public string SelectLabel { get; }

    public IReadOnlyList<PresentationBadge> Badges { get; }

    public bool IsSelected { get; }

    public bool IsBusy { get; }

    public bool IsDisabled { get; }
}
