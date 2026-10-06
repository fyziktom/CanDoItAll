using CanDoItAll.Modules.Workbench;

namespace CanDoItAll.Workbench.Structure.UI;

public sealed record StructureDialogsPresentation(
    StructureHierarchyPresentation? Hierarchy = null,
    StructureConversionPresentation? Conversion = null,
    StructureTransferPresentation? Transfer = null);

public sealed record StructureProjectOption(Guid Id, string Name);

public sealed record StructureHierarchyPresentation(Guid OpeningId, string Title, string Copy, string SubmitLabel,
    string CurrentParentProjectTitle, IReadOnlyList<StructureProjectOption> AvailableProjects, Guid? SelectedProjectId,
    string Error, bool IsBusy, bool RequiresObservation);

public sealed record StructureConversionPresentation(Guid OpeningId, string NodeId, string Title, string Copy,
    string SubmitLabel, string SelectionLabel, IReadOnlyList<ProjectStructureMutationTypeOption> Options,
    string SelectedActionId, string Error, bool IsBusy, bool RequiresObservation);

public sealed record StructureTransferPresentation(Guid OpeningId, string SourceNodeId, string Title, string Copy,
    string SubmitLabel, int DescendantCount, string ProjectName, string Error, bool IsBusy, bool RequiresObservation);

public enum StructureDialogOperation { CloseHierarchy, SubmitHierarchy, CreateProject, CloseConversion, SubmitConversion, CloseTransfer, SubmitTransfer }
public abstract record StructureDialogIntent(Guid OpeningId);
public sealed record StructureDialogCommand(Guid OpeningId, StructureDialogOperation Operation) : StructureDialogIntent(OpeningId);
public sealed record StructureHierarchySelection(Guid OpeningId, Guid? ProjectId) : StructureDialogIntent(OpeningId);
public sealed record StructureConversionSelection(Guid OpeningId, string ActionId) : StructureDialogIntent(OpeningId);
public sealed record StructureTransferName(Guid OpeningId, string Name) : StructureDialogIntent(OpeningId);
