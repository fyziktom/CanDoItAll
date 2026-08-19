namespace CanDoItAll.Conversations.Components.Presentation;

public sealed record ConversationActionRequest(
    ConversationPresentationKey ItemKey,
    ConversationPresentationKey ActionKey);
