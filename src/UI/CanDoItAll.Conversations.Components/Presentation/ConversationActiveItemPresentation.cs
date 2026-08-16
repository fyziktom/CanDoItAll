namespace CanDoItAll.Conversations.Components.Presentation;

public sealed record ConversationActiveItemPresentation(
    ConversationPresentationKey Key,
    string DisplayName,
    IReadOnlyList<PresentationBadge> Badges,
    bool IsVisible,
    bool CanStop);
