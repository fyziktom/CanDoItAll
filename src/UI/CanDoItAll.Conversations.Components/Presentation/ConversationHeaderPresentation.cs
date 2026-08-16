namespace CanDoItAll.Conversations.Components.Presentation;

public sealed record ConversationHeaderPresentation(
    ConversationAvatarPresentation Avatar,
    IReadOnlyList<PresentationBadge> Badges)
{
    private IReadOnlyList<PresentationBadge> badges =
        PresentationCollection.Snapshot(Badges, nameof(Badges));

    public ConversationAvatarPresentation Avatar { get; init; } =
        Avatar ?? throw new ArgumentNullException(nameof(Avatar));

    public IReadOnlyList<PresentationBadge> Badges
    {
        get => badges;
        init => badges = PresentationCollection.Snapshot(value, nameof(Badges));
    }
}
