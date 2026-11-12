using System.ComponentModel;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Web.Api;

[Description("Project creation or full definition replacement. An existing ID requires its original expectedDefinition and explicit non-null phases and options arrays; an empty array intentionally deletes that collection. Creation may omit the arrays.")]
internal sealed class ProjectEditorSaveRequest {
    public Guid? Id { get; init; }
    public ProjectDefinitionExpectation? ExpectedDefinition { get; init; }
    public Guid? ExpectedLifetimeId { get; init; }
    public string? ExternalNamespace { get; init; }
    public string? ExternalKey { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Objective { get; init; } = string.Empty;
    public ProjectStatus Status { get; init; } = ProjectStatus.Draft;
    public string CurrentPhase { get; init; } = string.Empty;
    public DateTime? TargetDateUtc { get; init; }
    public List<ProjectPhaseEditorModel>? Phases { get; init; }
    public List<ProjectOptionEditorModel>? Options { get; init; }

    internal bool HasReplacementCollections => Phases is not null && Options is not null;

    internal ProjectEditorModel ToEditor() => new() {
        Id = Id,
        ExpectedDefinition = ExpectedDefinition,
        ExpectedLifetimeId = ExpectedLifetimeId,
        ExternalNamespace = ExternalNamespace,
        ExternalKey = ExternalKey,
        Name = Name,
        Description = Description,
        Objective = Objective,
        Status = Status,
        CurrentPhase = CurrentPhase,
        TargetDateUtc = TargetDateUtc,
        Phases = Phases ?? [],
        Options = Options ?? []
    };
}
