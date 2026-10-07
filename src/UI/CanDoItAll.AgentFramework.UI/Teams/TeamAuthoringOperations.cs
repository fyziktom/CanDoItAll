using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.UI.Teams;

public sealed record TeamMetadataSubmission(Guid? Id, string Name, string Description, string Icon) {
    public AgentTeamEditorModel ToEditorModel() => new() { Id = Id, Name = Name, Description = Description, Icon = Icon };
    public static TeamMetadataSubmission Capture(AgentTeamEditorModel model) => new(model.Id, model.Name, model.Description, model.Icon);
}

public abstract record TeamMetadataOutcome {
    public sealed record Accepted(Guid TeamId) : TeamMetadataOutcome;
    public sealed record Rejected : TeamMetadataOutcome;
    public sealed record Unknown : TeamMetadataOutcome;
}

public sealed record TeamMetadataOperations(
    Func<Guid?, CancellationToken, Task<AgentTeamEditorModel>> Load,
    Func<TeamMetadataSubmission, CancellationToken, Task<TeamMetadataOutcome>> Save,
    Func<string, CancellationToken, Task<string?>> ChooseIcon);

public sealed record TeamMembershipSelection(Guid TeamId, ImmutableArray<Guid> AgentIds);
