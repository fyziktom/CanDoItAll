namespace CanDoItAll.Conversations.Components.Presentation;

public sealed record ConversationParticipantPresentation
{
    public ConversationParticipantPresentation(
        ConversationPresentationKey key,
        string displayName,
        string? subtitle = null,
        string? summary = null,
        string? avatarImageUrl = null,
        string? avatarSeed = null,
        string? avatarFallbackText = null,
        string? searchText = null,
        string? detailsText = null,
        string? detailsAriaLabel = null,
        string? selectLabel = null,
        IReadOnlyList<PresentationBadge>? badges = null,
        IReadOnlyList<string>? tags = null,
        IReadOnlyList<PresentationMetaItem>? metadata = null,
        PresentationBadge? ribbon = null,
        ParticipantFavoritePresentation? favorite = null,
        bool isSelected = false,
        bool isBusy = false,
        bool isDisabled = false,
        bool isFavorite = false)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        Key = key;
        DisplayName = displayName;
        Subtitle = subtitle;
        Summary = summary;
        AvatarImageUrl = avatarImageUrl;
        AvatarSeed = string.IsNullOrWhiteSpace(avatarSeed) ? displayName : avatarSeed;
        AvatarFallbackText = string.IsNullOrWhiteSpace(avatarFallbackText) ? "?" : avatarFallbackText;
        SearchText = string.IsNullOrWhiteSpace(searchText) ? displayName : searchText;
        DetailsText = detailsText;
        DetailsAriaLabel = string.IsNullOrWhiteSpace(detailsAriaLabel)
            ? "Show participant details"
            : detailsAriaLabel;
        SelectLabel = string.IsNullOrWhiteSpace(selectLabel) ? $"Select {displayName}" : selectLabel;
        Badges = PresentationCollection.Snapshot(badges, nameof(badges));
        Tags = PresentationCollection.Snapshot(tags, nameof(tags));
        Metadata = PresentationCollection.Snapshot(metadata, nameof(metadata));
        Ribbon = ribbon;
        Favorite = favorite;
        IsSelected = isSelected;
        IsBusy = isBusy;
        IsDisabled = isDisabled;
        IsFavorite = isFavorite;
    }

    public ConversationPresentationKey Key { get; }

    public string DisplayName { get; }

    public string? Subtitle { get; }

    public string? Summary { get; }

    public string? AvatarImageUrl { get; }

    public string AvatarSeed { get; }

    public string AvatarFallbackText { get; }

    public string SearchText { get; }

    public string? DetailsText { get; }

    public string DetailsAriaLabel { get; }

    public string SelectLabel { get; }

    public IReadOnlyList<PresentationBadge> Badges { get; }

    public IReadOnlyList<string> Tags { get; }

    public IReadOnlyList<PresentationMetaItem> Metadata { get; }

    public PresentationBadge? Ribbon { get; }

    public ParticipantFavoritePresentation? Favorite { get; }

    public bool IsSelected { get; }

    public bool IsBusy { get; }

    public bool IsDisabled { get; }

    public bool IsFavorite { get; }
}
