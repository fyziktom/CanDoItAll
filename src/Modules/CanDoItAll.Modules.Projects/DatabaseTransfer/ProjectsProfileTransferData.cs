using CanDoItAll.Infrastructure.Persistence;
using System.Text.Json.Serialization;

namespace CanDoItAll.Modules.Projects;

public sealed class ProjectTransferProject {
    [JsonIgnore]
    public Guid LifetimeId { get; set; }
    [JsonIgnore]
    public bool LegacyAgentAccessBindingEligible { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Objective { get; set; } = string.Empty;
    public ProjectStatus Status { get; set; } = ProjectStatus.Draft;
    public string CurrentPhase { get; set; } = string.Empty;
    public DateTime? TargetDateUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class ProjectTransferPhase {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Goal { get; set; } = string.Empty;
    public ProjectPhaseStatus Status { get; set; } = ProjectPhaseStatus.Planned;
    public int OrderIndex { get; set; }
    public DateTime? StartDateUtc { get; set; }
    public DateTime? EndDateUtc { get; set; }
}

public sealed class ProjectTransferOption {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public ProjectOptionCategory Category { get; set; }
    public string OptionName { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}

public sealed class ProjectTransferHierarchy {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ParentProjectId { get; set; }
    public Guid ChildProjectId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class ProjectTransferRetirement {
    public Guid LifetimeId { get; set; }
    public Guid ProjectId { get; set; }
    public DateTimeOffset RetiredAtUtc { get; set; }
    public RetainedEvidenceImport? ImportedHistory { get; set; }
}

public sealed class ProjectTransferReservation {
    public Guid Id { get; set; }
    public Guid DatabaseProfileId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid LifetimeId { get; set; }
    public Guid RequesterId { get; set; }
    public Guid? ParentProjectId { get; set; }
    public Guid? ParentLifetimeId { get; set; }
    public ProjectCreationReservationState State { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? ConsumedAtUtc { get; set; }
    public DateTimeOffset? CancelledAtUtc { get; set; }
    public RetainedEvidenceImport? ImportedHistory { get; set; }
}

public sealed record ProjectsProfileTransferData(
    IReadOnlyList<ProjectTransferProject> Projects,
    IReadOnlyList<ProjectTransferPhase> Phases,
    IReadOnlyList<ProjectTransferOption> Options,
    IReadOnlyList<ProjectTransferHierarchy> HierarchyLinks,
    IReadOnlyList<ProjectTransferRetirement> Retirements,
    IReadOnlyList<ProjectTransferReservation> CreationReservations);

public sealed record ProjectsProfileTransferCounts(int Projects, int Phases, int Options, int HierarchyLinks, int Retirements, int CreationReservations);
