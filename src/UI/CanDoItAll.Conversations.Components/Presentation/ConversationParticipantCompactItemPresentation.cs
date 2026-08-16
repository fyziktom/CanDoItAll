namespace CanDoItAll.Conversations.Components.Presentation;

public sealed record ConversationParticipantCompactItemPresentation(
    ConversationParticipantPresentation Participant,
    IReadOnlyList<ParticipantActionPresentation> Actions,
    string? ShellTestId = null,
    string? SelectTestId = null);
