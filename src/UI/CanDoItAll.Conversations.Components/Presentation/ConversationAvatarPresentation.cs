namespace CanDoItAll.Conversations.Components.Presentation;

public sealed record ConversationAvatarPresentation(
    string Alt,
    string? ImageUrl,
    string FallbackText,
    string Seed);
