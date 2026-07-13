using CanDoItAll.Components.BaseLib;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench.Pages;

public sealed record ProjectStructureSelectionPanelState(
    string RenderKey,
    IReadOnlyList<ProjectStructureSelectionListItem> SelectedNodes,
    ProjectStructureSelectionDetailState? SelectedNode,
    bool CanApplySelectionStatus,
    string SelectionBorderName,
    bool IsConnectMode,
    bool IsReconnectMode);

public sealed record ProjectStructureSelectionListItem(
    string Id,
    string Title,
    string KindLabel,
    string Status);

public sealed record ProjectStructureSelectionDetailState(
    string Id,
    string Title,
    string Kicker,
    string LeadText,
    string Status,
    string ProgressLabel,
    string PriorityLabel,
    string MarkerLabel,
    IReadOnlyList<ProjectStructureSelectionBadgePresentation> BadgePresentations,
    StorageSummaryModel? StorageSummary,
    ProjectStructureAttachmentPreviewCardState? AttachmentPreview,
    ProjectStructureSelectionWorkflowStatusState? WorkflowStatus,
    bool HasMermaidViewer,
    IReadOnlyList<ProjectStructureInspectorActionItem> Actions,
    string? WorkflowFeedback,
    string WorkflowFeedbackTone,
    bool ShowAdvancedDetails,
    string ArtifactKind,
    string LocationLabel,
    IReadOnlyList<ProjectStructureDetailSection> DetailSections,
    IReadOnlyList<ProjectStructureNodeFact> Facts);

public sealed record ProjectStructureSelectionWorkflowStatusState(
    string WorkflowName,
    string State,
    string Status,
    string Message,
    string StepLabel,
    string ProgressLabel,
    string RunId,
    string LastUpdatedLabel,
    IReadOnlyList<string> CreatedNodeIds,
    IReadOnlyList<string> CreatedAssetIds,
    IReadOnlyList<string> CreatedFilePaths);

public sealed record ProjectStructureAttachmentPreviewCardState(
    AttachmentPreviewKind Kind,
    string DisplayName,
    string LeadCopy,
    string OriginalFileName,
    string ContentType,
    bool CanShowLocalOpen,
    bool CanRenderPreview,
    string? LocalOpenFeedback,
    string LocalOpenFeedbackTone)
{
    public bool ShowLocalOpenFeedback => !string.IsNullOrWhiteSpace(LocalOpenFeedback);
}

public sealed record ProjectStructureInspectorActionItem(
    string ActionId,
    string Label,
    string Icon,
    string Tone);

public sealed record ProjectStructureSelectionMarkerRequest(
    string Badge,
    string Tone,
    string Label);

public enum AttachmentPreviewKind
{
    None,
    Image,
    Video,
    Audio,
    Document,
    TextDocument
}
