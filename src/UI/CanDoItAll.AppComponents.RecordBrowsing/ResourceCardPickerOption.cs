namespace CanDoItAll.AppComponents;

public enum ResourceCardPickerVisualKind
{
    Icon,
    Avatar
}

public sealed record ResourceCardPickerOption<TItem>(
    TItem Item,
    string Title,
    string KindLabel)
{
    public string Subtitle { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string Meta { get; init; } = string.Empty;

    public string Icon { get; init; } = "category";

    public ResourceCardPickerVisualKind VisualKind { get; init; }

    public string? ImageUrl { get; init; }

    public string AvatarFallback { get; init; } = string.Empty;

    public IReadOnlyList<string> Tags { get; init; } = [];

    public string AdditionalSearchText { get; init; } = string.Empty;

    public bool IsSelected { get; init; }

    public bool IsDisabled { get; init; }

    public string DisabledReason { get; init; } = string.Empty;

    public bool ShowFavorite { get; init; }

    public bool IsFavorite { get; init; }

    public bool FavoriteDisabled { get; init; }

    public string TestId { get; init; } = string.Empty;
}
