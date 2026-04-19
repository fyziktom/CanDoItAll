using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench.Pages;

public enum ProjectStructureProjectHierarchyDialogMode
{
    AddSubproject,
    ReconnectSubproject
}

public sealed record ProjectStructureProjectHierarchyDialogState(
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

public enum ProjectStructureBlockMutationDialogMode
{
    ChangeBlockType,
    ConvertNoteToBlock
}

public sealed record ProjectStructureBlockMutationDialogState(
    ProjectStructureBlockMutationDialogMode Mode,
    string NodeId,
    string NodeTitle,
    IReadOnlyList<ProjectStructureMutationTypeOption> Options,
    string SelectedActionId,
    string Error)
{
    public string Title => Mode switch
    {
        ProjectStructureBlockMutationDialogMode.ChangeBlockType => $"Change block type for {NodeTitle}",
        _ => $"Convert {NodeTitle}"
    };

    public string Copy => Mode switch
    {
        ProjectStructureBlockMutationDialogMode.ChangeBlockType =>
            "Choose the common block type that should replace the current block subtype.",
        _ =>
            "Choose the typed node kind that should replace this note while keeping its text."
    };

    public string SubmitLabel => Mode switch
    {
        ProjectStructureBlockMutationDialogMode.ChangeBlockType => "Change block type",
        _ => "Convert"
    };

    public string SelectionLabel => Mode switch
    {
        ProjectStructureBlockMutationDialogMode.ChangeBlockType => "Block type",
        _ => "Target kind"
    };
}

public sealed record ProjectStructureSubprojectTransferDialogState(
    string SourceNodeId,
    string SourceNodeTitle,
    int DescendantCount,
    string ProjectName,
    string Error)
{
    public string Title => $"Move descendants from {SourceNodeTitle}";

    public string Copy => DescendantCount == 1
        ? "Create a new subproject and move the single descendant under this node into it."
        : $"Create a new subproject and move {DescendantCount} descendants under this node into it.";

    public string SubmitLabel => "Create subproject";
}

public sealed record ProjectStructureProcessLinkOption(
    Guid DefinitionId,
    string DisplayName,
    string ScopeLabel,
    string Status,
    bool HasPublishedVersion);

public sealed record ProjectStructureProcessLinkDialogState(
    string SourceNodeId,
    string SourceNodeTitle,
    IReadOnlyList<ProjectStructureProcessLinkOption> Options,
    Guid? SelectedDefinitionId,
    string Error)
{
    public string Title => $"Add process for {SourceNodeTitle}";

    public string Copy => "Choose an existing process definition to link to this node. The link stays explicit in the project structure and does not create a new process.";

    public string SubmitLabel => "Add process";
}

public sealed record ProjectStructureQuickActionDialogState(
    string NodeId,
    string Title,
    string NodeLabel,
    string Copy,
    ProjectStructureQuickActionButton EditAction,
    ProjectStructureQuickActionButton PrimaryAction);

public sealed record ProjectStructureQuickActionButton(
    ProjectStructureQuickActionExecutionKind ExecutionKind,
    string Label,
    string Description,
    string Icon,
    string Tone,
    string ActionId = "",
    ProjectStructureCommandKind? CommandKind = null,
    bool IsDisabled = false);

public enum ProjectStructureQuickActionExecutionKind
{
    Edit,
    InspectorAction,
    CommandInNewTab
}

public sealed record ProjectStructureDeletePrompt(
    string NodeId,
    string Title,
    int DescendantCount,
    bool RequiresConfirmation,
    string ImpactCopy);

public sealed record ProjectStructureSummaryDialogState(
    string RootNodeId,
    string RootTitle,
    ProjectStructureSummary Summary);

public sealed record ProjectStructureTranscriptActionDialogState(
    string NodeId,
    string NodeTitle,
    ProjectLlmActionKind ActionKind,
    Guid? SelectedProviderId,
    string LastProviderName,
    IReadOnlyList<ProviderProfileSummary> Providers,
    string Error);
