namespace CanDoItAll.Conversations.Components.Presentation;

public sealed record ConversationHeaderPresentation(
    ConversationAvatarPresentation Avatar,
    IReadOnlyList<PresentationBadge> Badges);
