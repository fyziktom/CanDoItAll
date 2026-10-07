using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.Projects.Pages.Components;

public sealed record ProjectHierarchyDialogState(
    Guid ProjectId,
    string ProjectName,
    IReadOnlyList<ProjectSummary> ParentProjects,
    IReadOnlyList<ProjectSummary> Subprojects);
