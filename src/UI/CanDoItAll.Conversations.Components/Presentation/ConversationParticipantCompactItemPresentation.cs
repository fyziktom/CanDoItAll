namespace CanDoItAll.Conversations.Components.Presentation;

public sealed record ConversationParticipantCompactItemPresentation(
    ConversationParticipantPresentation Participant,
    IReadOnlyList<ParticipantActionPresentation> Actions,
    string? ShellTestId = null,
    string? SelectTestId = null)
{
    private IReadOnlyList<ParticipantActionPresentation> actions =
        PresentationCollection.Snapshot(Actions, nameof(Actions));

    public ConversationParticipantPresentation Participant { get; init; } =
        Participant ?? throw new ArgumentNullException(nameof(Participant));

    public IReadOnlyList<ParticipantActionPresentation> Actions
    {
        get => actions;
        init => actions = PresentationCollection.Snapshot(value, nameof(Actions));
    }
}
