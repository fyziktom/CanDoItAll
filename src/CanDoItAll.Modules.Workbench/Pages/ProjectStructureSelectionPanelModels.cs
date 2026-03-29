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
    ProjectStructureAttachmentPreviewCardState? AttachmentPreview,
    bool HasMermaidViewer,
    IReadOnlyList<ProjectStructureInspectorActionItem> Actions,
    string? WorkflowFeedback,
    string WorkflowFeedbackTone,
    bool ShowAdvancedDetails,
    string ArtifactKind,
    string LocationLabel,
    IReadOnlyList<ProjectStructureNodeFact> Facts);

public sealed record ProjectStructureAttachmentPreviewCardState(
    AttachmentPreviewKind Kind,
    string DisplayName,
    string LeadCopy,
    string Route,
    string PreviewSource,
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
    Document
}
