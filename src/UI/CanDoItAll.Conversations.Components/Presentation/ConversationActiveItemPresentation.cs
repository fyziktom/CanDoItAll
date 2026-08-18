namespace CanDoItAll.Conversations.Components.Presentation;

public sealed record ConversationActiveItemPresentation(
    ConversationPresentationKey Key,
    string DisplayName,
    IReadOnlyList<PresentationBadge> Badges,
    IReadOnlyList<ConversationActionPresentation> Actions)
{
    private IReadOnlyList<PresentationBadge> badges =
        PresentationCollection.Snapshot(Badges, nameof(Badges));
    private IReadOnlyList<ConversationActionPresentation> actions =
        PresentationCollection.Snapshot(Actions, nameof(Actions));

    public ConversationPresentationKey Key { get; init; } =
        Key ?? throw new ArgumentNullException(nameof(Key));

    public string DisplayName { get; init; } =
        string.IsNullOrWhiteSpace(DisplayName)
            ? throw new ArgumentException("A display name is required.", nameof(DisplayName))
            : DisplayName;

    public IReadOnlyList<PresentationBadge> Badges
    {
        get => badges;
        init => badges = PresentationCollection.Snapshot(value, nameof(Badges));
    }

    public IReadOnlyList<ConversationActionPresentation> Actions
    {
        get => actions;
        init => actions = PresentationCollection.Snapshot(value, nameof(Actions));
    }
}
