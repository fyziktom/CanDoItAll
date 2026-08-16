namespace CanDoItAll.Conversations.Components.Presentation;

public sealed record ParticipantActionPresentation(
    ConversationPresentationKey Key,
    string Label,
    string Icon,
    string? TestId = null,
    bool IsDisabled = false,
    ParticipantActionStyle Style = ParticipantActionStyle.Primary);
