namespace CanDoItAll.Conversations.Components.Presentation;

public sealed record ParticipantFavoritePresentation(
    bool IsFavorite,
    bool IsDisabled,
    string MarkLabel,
    string RemoveLabel,
    string TestId);
