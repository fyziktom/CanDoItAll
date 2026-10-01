namespace CanDoItAll.Modules.Projects;

/// <summary>
/// Identifier of a project-deletion participant, the module part that cleans up its own data after a project is
/// deleted. It is serialized as an object with one member, for example <c>{ "value": "workbench" }</c>; send the
/// <c>value</c> text as <c>participantId</c> in the cleanup retry route.
/// </summary>
public readonly record struct ProjectDeletionParticipantId
{
    public ProjectDeletionParticipantId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }

    /// <summary>
    /// Text identifier of the participant, currently <c>workbench</c> (Project Structure nodes and managed media) or
    /// <c>agent-project-structure-access</c> (agents' access to the project). Never null in responses.
    /// </summary>
    public string Value { get; }

    public override string ToString()
        => Value;
}
