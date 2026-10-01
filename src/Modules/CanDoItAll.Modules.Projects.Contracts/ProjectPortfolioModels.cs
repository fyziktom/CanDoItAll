using System.Text.Json.Serialization;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Projects;

public static class ProjectErrorCodes
{
    public const string NotFound = "projects.not-found";
    public const string LifetimeChanged = "projects.lifetime-changed";
    public const string ReservedIdConflict = "projects.reserved-id-conflict";
    public const string ReservationClosed = "projects.reservation-closed";
}

/// <summary>
/// Status of a project phase, as a JSON integer: 0 Planned, 1 Active, 2 Blocked, 3 Completed.
/// </summary>
public enum ProjectPhaseStatus
{
    Planned,
    Active,
    Blocked,
    Completed
}

/// <summary>
/// Category of a project technology option, as a JSON integer: 0 Language, 1 Database, 2 Ui, 3 ExternalApi, 4 Storage,
/// 5 Deployment, 6 Testing, 7 Other.
/// </summary>
public enum ProjectOptionCategory
{
    Language,
    Database,
    Ui,
    ExternalApi,
    Storage,
    Deployment,
    Testing,
    Other
}

/// <summary>
/// Summary of one project, as returned by project lists, project saves and hierarchy reads of the Projects and Project
/// Structure APIs.
/// </summary>
/// <param name="Id">
/// Identifier of the project; use it with the other <c>/api/projects</c> operations and with
/// <c>/api/project-structure</c>.
/// </param>
/// <param name="Name">Name of the project.</param>
/// <param name="Status">
/// Lifecycle status of the project, as a JSON integer: 0 Draft, 1 Active, 2 OnHold, 3 Completed, 4 Archived.
/// </param>
/// <param name="CurrentPhase">Name of the current phase as free text; empty when not set.</param>
/// <param name="PhaseCount">Number of phases of the project.</param>
/// <param name="ParentCount">Number of parent projects.</param>
/// <param name="ChildCount">Number of direct subprojects.</param>
/// <param name="UpdatedAtUtc">When the project record was last saved, in UTC.</param>
/// <param name="PrimaryCustomerName">
/// Display name of the primary customer among the project's participants, or of the first customer by name when none
/// is primary; empty when the project has no customer participant.
/// </param>
/// <param name="PrimaryDeliveryUnitName">
/// Display name of the primary delivery unit among the project's participants, or of the first one by name; empty when
/// there is none.
/// </param>
/// <param name="PrimaryOwnerName">
/// Display name of the primary owner (a participating manager, team member or reviewer), or of the first one by name;
/// empty when there is none.
/// </param>
/// <param name="RelatedParties">
/// Parties that participate in the project as a whole in its current lifetime (task assignments are not included),
/// primary ones first, then by name; an empty array when there are none.
/// </param>
/// <param name="RelatedPartySearchText">
/// One line per related party in the form label:display name, joined by line breaks, for client-side filtering.
/// </param>
public sealed record ProjectSummary(
    Guid Id,
    string Name,
    ProjectStatus Status,
    string CurrentPhase,
    int PhaseCount,
    int ParentCount,
    int ChildCount,
    DateTimeOffset UpdatedAtUtc,
    string PrimaryCustomerName = "",
    string PrimaryDeliveryUnitName = "",
    string PrimaryOwnerName = "",
    IReadOnlyList<ProjectPortfolioPartyItem>? RelatedParties = null,
    string RelatedPartySearchText = "") {
    [JsonIgnore]
    public ProjectWriteAdmission? ExpectedProjectAdmission { get; init; }
}

/// <summary>
/// One project in the lightweight project list of <c>GET /api/projects/access-list</c>.
/// </summary>
/// <param name="Id">Identifier of the project.</param>
/// <param name="Name">Name of the project.</param>
public sealed record ProjectAccessListItem(
    Guid Id,
    string Name);

/// <summary>
/// A parent-child link between two projects in the project hierarchy.
/// </summary>
/// <param name="ParentProjectId">Identifier of the parent project.</param>
/// <param name="ChildProjectId">Identifier of the subproject.</param>
/// <param name="CreatedAtUtc">When the link was created, in UTC.</param>
public sealed record ProjectHierarchyLinkSummary(
    Guid ParentProjectId,
    Guid ChildProjectId,
    DateTimeOffset CreatedAtUtc);

/// <summary>
/// The direct parents and direct subprojects of one project, as returned by the project hierarchy reads.
/// </summary>
/// <param name="ProjectId">Identifier of the requested project.</param>
/// <param name="ParentProjects">Summaries of the direct parent projects, most recently updated first.</param>
/// <param name="ChildProjects">Summaries of the direct subprojects, most recently updated first.</param>
public sealed record ProjectHierarchySnapshot(
    Guid ProjectId,
    IReadOnlyList<ProjectSummary> ParentProjects,
    IReadOnlyList<ProjectSummary> ChildProjects);

/// <summary>
/// A phase of a project in the project read and save. Phases are stored in array order.
/// </summary>
public sealed class ProjectPhaseEditorModel
{
    /// <summary>
    /// Identifier of the phase; null for a phase to add. On save, an entry whose identifier matches a stored phase of
    /// the project updates it, and stored phases missing from the request are deleted.
    /// </summary>
    public Guid? Id { get; set; }

    /// <summary>
    /// Name of the phase; trimmed.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// What the phase should achieve, as free text; trimmed.
    /// </summary>
    public string Goal { get; set; } = string.Empty;

    /// <summary>
    /// Status of the phase, as a JSON integer: 0 Planned, 1 Active, 2 Blocked, 3 Completed. Omitted means Planned.
    /// </summary>
    public ProjectPhaseStatus Status { get; set; } = ProjectPhaseStatus.Planned;

    /// <summary>
    /// Start of the phase as a UTC date and time. A sent value with an offset is converted to UTC and a value without
    /// an offset is taken as UTC. Null means not set.
    /// </summary>
    public DateTime? StartDateUtc { get; set; }

    /// <summary>
    /// End of the phase as a UTC date and time, converted like <c>startDateUtc</c>; not checked against the start. Null
    /// means not set.
    /// </summary>
    public DateTime? EndDateUtc { get; set; }
}

/// <summary>
/// A technology option of a project, such as its programming language or database, in the project read and save.
/// </summary>
public sealed class ProjectOptionEditorModel
{
    /// <summary>
    /// Identifier of the option; null for an option to add and for the empty category entries of a read. On save,
    /// stored options missing from the request are deleted.
    /// </summary>
    public Guid? Id { get; set; }

    /// <summary>
    /// Category of the option, as a JSON integer: 0 Language, 1 Database, 2 Ui, 3 ExternalApi, 4 Storage, 5 Deployment,
    /// 6 Testing, 7 Other.
    /// </summary>
    public ProjectOptionCategory Category { get; set; }

    /// <summary>
    /// The chosen option, for example <c>PostgreSQL</c>; trimmed. An entry with an empty name and empty notes is
    /// skipped on save.
    /// </summary>
    public string OptionName { get; set; } = string.Empty;

    /// <summary>
    /// Notes about the choice; trimmed.
    /// </summary>
    public string Notes { get; set; } = string.Empty;
}

/// <summary>
/// The editable fields of a project, returned by <c>GET /api/projects/{projectId}</c> and sent to
/// <c>POST /api/projects</c>. Without <c>id</c> it describes a new project. Changes to subprojects, tasks and project
/// participants use other operations.
/// </summary>
public sealed class ProjectEditorModel
{
    [JsonIgnore]
    public ProjectWriteAdmission? ExpectedProjectAdmission { get; set; }

    /// <summary>
    /// Identifier of the project; null for a new project, including the blank template that the read returns for an
    /// unknown identifier. A save with an identifier that does not exist is rejected (<c>projects.not-found</c>).
    /// </summary>
    public Guid? Id { get; set; }

    /// <summary>
    /// Identifier of the project lifetime that the read returned. Send it back unchanged: a save is rejected
    /// (<c>projects.lifetime-changed</c>) when the project was deleted and recreated with the same identifier since the
    /// read. Null skips the check; ignored when creating a project.
    /// </summary>
    public Guid? ExpectedLifetimeId { get; set; }

    /// <summary>
    /// Name of the project; required on save and trimmed.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of the project; trimmed.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Goal of the project; trimmed.
    /// </summary>
    public string Objective { get; set; } = string.Empty;

    /// <summary>
    /// Lifecycle status, as a JSON integer: 0 Draft, 1 Active, 2 OnHold, 3 Completed, 4 Archived. Omitted means Draft.
    /// </summary>
    public ProjectStatus Status { get; set; } = ProjectStatus.Draft;

    /// <summary>
    /// Name of the current phase as free text; trimmed. It is not linked to the <c>phases</c> list.
    /// </summary>
    public string CurrentPhase { get; set; } = string.Empty;

    /// <summary>
    /// Target completion date and time in UTC. A sent value with an offset is converted to UTC and a value without an
    /// offset is taken as UTC. Null means none.
    /// </summary>
    public DateTime? TargetDateUtc { get; set; }

    /// <summary>
    /// The project's phases in order. On save the list replaces the stored phases: phases missing from it are deleted.
    /// </summary>
    public List<ProjectPhaseEditorModel> Phases { get; set; } = [];

    /// <summary>
    /// The project's technology options. On save the list replaces the stored options: options missing from it are
    /// deleted. A read always includes an entry for each of the categories Language through Testing.
    /// </summary>
    public List<ProjectOptionEditorModel> Options { get; set; } = [];
}
