using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.UI.Teams;

public sealed record TeamMetadataSubmission(Guid? Id, string Name, string Description, string Icon, AgentTeamMetadataExpectation? ExpectedMetadata = null) {
    public AgentTeamEditorModel ToEditorModel() => new() { Id = Id, Name = Name, Description = Description, Icon = Icon, ExpectedMetadata = ExpectedMetadata };
    public static TeamMetadataSubmission Capture(AgentTeamEditorModel model) => new(model.Id, model.Name, model.Description, model.Icon, model.ExpectedMetadata);
}

public abstract record TeamMetadataOutcome {
    public sealed record Accepted(Guid TeamId, AgentTeamEditorModel? Snapshot = null, string? Warning = null) : TeamMetadataOutcome;
    public sealed record Rejected : TeamMetadataOutcome;
    public sealed record Conflict : TeamMetadataOutcome;
    public sealed record Unknown : TeamMetadataOutcome;
}

public sealed record TeamMetadataOperations(
    Func<Guid?, CancellationToken, Task<AgentTeamEditorModel>> Load,
    Func<TeamMetadataSubmission, CancellationToken, Task<TeamMetadataOutcome>> Save,
    Func<string, CancellationToken, Task<string?>> ChooseIcon);

public sealed record TeamMembershipSelection(Guid TeamId, ImmutableArray<Guid> AgentIds);

public sealed record TeamMembershipCommand(Guid TeamId, DateTimeOffset ExpectedUpdatedAtUtc, ImmutableArray<Guid> AgentIds);

public abstract record TeamMembershipOutcome {
    public sealed record Accepted : TeamMembershipOutcome;
    public sealed record Conflict : TeamMembershipOutcome;
    public sealed record Rejected : TeamMembershipOutcome;
    public sealed record Unknown : TeamMembershipOutcome;
}

public sealed record TeamMembershipOperations(
    Func<TeamMembershipCommand, CancellationToken, Task<TeamMembershipOutcome>> Save,
    Func<Guid, CancellationToken, Task<AgentTeamDefinition>> ReadCurrent);
