namespace CanDoItAll.Conversations.Components.Presentation;

public sealed record ParticipantActionRequest(
    ConversationPresentationKey ParticipantKey,
    ConversationPresentationKey ActionKey);
