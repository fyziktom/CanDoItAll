using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench.Pages;

internal enum ProjectStructureProjectHierarchyDialogMode
{
    AddSubproject,
    ReconnectSubproject
}

internal sealed record ProjectStructureProjectHierarchyDialogState(
    ProjectStructureProjectHierarchyDialogMode Mode,
    Guid SubjectProjectId,
    string SubjectProjectTitle,
    Guid? CurrentParentProjectId,
    string CurrentParentProjectTitle,
    IReadOnlyList<ProjectSummary> AvailableProjects,
    Guid? SelectedProjectId,
    string Error)
{
    public string Title => Mode switch
    {
        ProjectStructureProjectHierarchyDialogMode.AddSubproject => $"Add subproject under {SubjectProjectTitle}",
        _ => $"Reconnect {SubjectProjectTitle}"
    };

    public string Copy => Mode switch
    {
        ProjectStructureProjectHierarchyDialogMode.AddSubproject =>
            "Choose an existing project to attach beneath the selected project node.",
        _ => $"Choose the new parent project for {SubjectProjectTitle}."
    };

    public string SubmitLabel => Mode switch
    {
        ProjectStructureProjectHierarchyDialogMode.AddSubproject => "Add subproject",
        _ => "Reconnect project"
    };
}

internal sealed record ProjectStructureQuickActionDialogState(
    string NodeId,
    string Title,
    string NodeLabel,
    string Copy,
    ProjectStructureQuickActionButton EditAction,
    ProjectStructureQuickActionButton PrimaryAction);

internal sealed record ProjectStructureQuickActionButton(
    ProjectStructureQuickActionExecutionKind ExecutionKind,
    string Label,
    string Description,
    string Icon,
    string Tone,
    string ActionId = "",
    ProjectStructureCommandKind? CommandKind = null,
    bool IsDisabled = false);

internal enum ProjectStructureQuickActionExecutionKind
{
    Edit,
    InspectorAction,
    CommandInNewTab
}

internal sealed record ProjectStructureDeletePrompt(
    string NodeId,
    string Title,
    int DescendantCount,
    bool RequiresConfirmation,
    string ImpactCopy);

internal sealed record ProjectStructureSummaryDialogState(
    string RootNodeId,
    string RootTitle,
    ProjectStructureSummary Summary);

internal sealed record ProjectStructureTranscriptActionDialogState(
    string NodeId,
    string NodeTitle,
    ProjectLlmActionKind ActionKind,
    Guid? SelectedProviderId,
    string LastProviderName,
    IReadOnlyList<ProviderProfileSummary> Providers,
    string Error);
