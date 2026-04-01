using System.Text;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench.Pages;

public partial class ProjectStructurePage
{
    private const string AdvancedDetailsHelpBody = "This section keeps artifact metadata, canvas coordinates, and the few node-specific facts that only matter when you need to inspect or troubleshoot the selected item.";
    private const string AdvancedDetailsHelpTip = "The main card above stays focused on actions, status, and the badges already visible.";

    private IReadOnlyList<ProjectStructureSelectionBadgePresentation> SelectedNodeBadgePresentations
        => selectedNode is null
            ? []
            : BuildSelectedNodeBadgePresentations(selectedNode);

    private static IReadOnlyList<ProjectStructureSelectionBadgePresentation> BuildSelectedNodeBadgePresentations(ProjectStructureNode node)
        => node.Badges
            .Select(badge => BuildSelectedNodeBadgePresentation(node, badge))
            .ToList();

    private static ProjectStructureSelectionBadgePresentation BuildSelectedNodeBadgePresentation(ProjectStructureNode node, string badge)
    {
        var style = ResolveSelectedBadgeStyle(node, badge);
        return new(badge, style, BuildSelectedBadgeTestId(badge));
    }

    private static ProjectStructureSelectionBadgeStyle ResolveSelectedBadgeStyle(ProjectStructureNode node, string badge)
    {
        if (string.Equals(badge, "Uploaded", StringComparison.OrdinalIgnoreCase))
        {
            return ProjectStructureSelectionBadgeStyle.Uploaded;
        }

        if (string.Equals(badge, "Scheduled", StringComparison.OrdinalIgnoreCase))
        {
            return ProjectStructureSelectionBadgeStyle.Scheduled;
        }

        if (string.Equals(badge, "Synced", StringComparison.OrdinalIgnoreCase))
        {
            return ProjectStructureSelectionBadgeStyle.Synced;
        }

        if (node.ObjectType == ProjectObjectType.File &&
            string.Equals(badge, ProjectStructureCanvasCatalog.ResolveNodeLabel(node), StringComparison.OrdinalIgnoreCase))
        {
            return ResolveFileSelectedBadgeStyle(node.ObjectSubtype);
        }

        return ProjectStructureSelectionBadgeStyle.Standard;
    }

    private static ProjectStructureSelectionBadgeStyle ResolveFileSelectedBadgeStyle(string objectSubtype)
        => objectSubtype switch
        {
            "pdf" => ProjectStructureSelectionBadgeStyle.FilePdf,
            "excel" => ProjectStructureSelectionBadgeStyle.FileExcel,
            "docx" => ProjectStructureSelectionBadgeStyle.FileDocx,
            "markdown" => ProjectStructureSelectionBadgeStyle.FileMarkdown,
            "mermaid" => ProjectStructureSelectionBadgeStyle.FileMermaid,
            "screenshot" => ProjectStructureSelectionBadgeStyle.FileScreenshot,
            "log" => ProjectStructureSelectionBadgeStyle.FileLog,
            "archive" => ProjectStructureSelectionBadgeStyle.FileArchive,
            "audio" => ProjectStructureSelectionBadgeStyle.FileAudio,
            "json" => ProjectStructureSelectionBadgeStyle.FileJson,
            "text" => ProjectStructureSelectionBadgeStyle.FileText,
            _ => ProjectStructureSelectionBadgeStyle.FileGeneric
        };

    private static string BuildSelectedBadgeTestId(string badge)
    {
        var sanitized = string.Concat(
            badge
                .Trim()
                .ToLowerInvariant()
                .Select(character => char.IsLetterOrDigit(character) ? character : '-'));

        while (sanitized.Contains("--", StringComparison.Ordinal))
        {
            sanitized = sanitized.Replace("--", "-", StringComparison.Ordinal);
        }

        sanitized = sanitized.Trim('-');
        return string.IsNullOrWhiteSpace(sanitized)
            ? "project-structure-selection-badge"
            : $"project-structure-selection-badge-{sanitized}";
    }

    private ProjectStructureSelectionPanelState SelectionPanelState
        => BuildSelectionPanelState();

    private string CanvasOverlayRenderKey
        => BuildCanvasOverlayRenderKey();

    private string SupportDialogRenderKey
        => BuildSupportDialogRenderKey();

    private ProjectStructureSelectionPanelState BuildSelectionPanelState()
    {
        var selectedItems = selectedNodes
            .Select(node => new ProjectStructureSelectionListItem(
                node.Id,
                node.Title,
                ProjectStructureCanvasCatalog.ResolveNodeLabel(node),
                node.Status))
            .ToList();

        var selectedDetail = selectedNode is null
            ? null
            : BuildSelectionDetailState(selectedNode);

        return new ProjectStructureSelectionPanelState(
            BuildSelectionPanelRenderKey(selectedItems, selectedDetail),
            selectedItems,
            selectedDetail,
            CanApplySelectionStatus,
            selectionBorderName,
            !string.IsNullOrWhiteSpace(linkModeSourceId),
            !string.IsNullOrWhiteSpace(reconnectNodeId));
    }

    private ProjectStructureSelectionDetailState BuildSelectionDetailState(ProjectStructureNode node)
    {
        var attachmentPreview = BuildAttachmentPreviewCardState(node);
        var actions = ResolveInspectorActions(node)
            .Select(action => new ProjectStructureInspectorActionItem(
                action.ActionId,
                action.Label,
                action.Icon,
                action.Tone))
            .ToList();

        return new ProjectStructureSelectionDetailState(
            node.Id,
            node.Title,
            ProjectStructureCanvasCatalog.ResolveNodeLabel(node),
            SelectedNodeLeadText,
            node.Status,
            ResolveProgressLabel(node),
            ResolvePriorityLabel(node),
            ResolveMarkerLabel(node),
            SelectedNodeBadgePresentations,
            attachmentPreview,
            HasMermaidViewer(node),
            actions,
            workflowFeedback,
            workflowFeedbackTone,
            CanShowAdvancedDetails(node),
            string.IsNullOrWhiteSpace(node.ArtifactKind) ? "None" : node.ArtifactKind,
            $"{Math.Round(node.X)}, {Math.Round(node.Y)}",
            ProjectStructureNodeDetailFactory.BuildSections(node),
            SelectedNodeFacts);
    }

    private ProjectStructureAttachmentPreviewCardState? BuildAttachmentPreviewCardState(ProjectStructureNode node)
    {
        if (!HasManagedAttachment(node))
        {
            return null;
        }

        return new ProjectStructureAttachmentPreviewCardState(
            ResolveAttachmentPreviewKind(node),
            ResolveAttachmentDisplayName(node),
            ResolveAttachmentLeadCopy(node),
            node.Route ?? string.Empty,
            ResolveAttachmentPreviewSource(node),
            node.MediaOriginalFileName ?? string.Empty,
            ResolveAttachmentContentType(node),
            CanShowLocalOpen(node),
            CanRenderAttachmentPreview(node),
            IsLocalOpenFeedbackVisible(node) ? localOpenFeedback : null,
            localOpenFeedbackTone);
    }

    private string BuildSelectionPanelRenderKey(
        IReadOnlyList<ProjectStructureSelectionListItem> selectedItems,
        ProjectStructureSelectionDetailState? selectedDetail)
    {
        var builder = new StringBuilder()
            .Append(selectedItems.Count)
            .Append('|')
            .Append(CanApplySelectionStatus)
            .Append('|')
            .Append(selectionBorderName)
            .Append('|')
            .Append(linkModeSourceId ?? string.Empty)
            .Append('|')
            .Append(reconnectNodeId ?? string.Empty);

        foreach (var item in selectedItems)
        {
            builder.Append('|')
                .Append(item.Id)
                .Append(':')
                .Append(item.Title)
                .Append(':')
                .Append(item.KindLabel)
                .Append(':')
                .Append(item.Status);
        }

        if (selectedDetail is null)
        {
            return builder.ToString();
        }

        builder.Append('|')
            .Append(selectedDetail.Id)
            .Append('|')
            .Append(selectedDetail.Title)
            .Append('|')
            .Append(selectedDetail.Status)
            .Append('|')
            .Append(selectedDetail.ProgressLabel)
            .Append('|')
            .Append(selectedDetail.PriorityLabel)
            .Append('|')
            .Append(selectedDetail.MarkerLabel)
            .Append('|')
            .Append(selectedDetail.WorkflowFeedback ?? string.Empty)
            .Append('|')
            .Append(selectedDetail.WorkflowFeedbackTone)
            .Append('|')
            .Append(selectedDetail.ShowAdvancedDetails)
            .Append('|')
            .Append(selectedDetail.AttachmentPreview?.DisplayName ?? string.Empty)
            .Append('|')
            .Append(selectedDetail.AttachmentPreview?.Kind ?? AttachmentPreviewKind.None)
            .Append('|')
            .Append(selectedDetail.AttachmentPreview?.LocalOpenFeedback ?? string.Empty);

        foreach (var badge in selectedDetail.BadgePresentations)
        {
            builder.Append('|')
                .Append(badge.Text)
                .Append(':')
                .Append(badge.Style);
        }

        foreach (var action in selectedDetail.Actions)
        {
            builder.Append('|')
                .Append(action.ActionId)
                .Append(':')
                .Append(action.Tone);
        }

        foreach (var fact in selectedDetail.Facts)
        {
            builder.Append('|')
                .Append(fact.Label)
                .Append(':')
                .Append(fact.Value);
        }

        return builder.ToString();
    }

    private string BuildCanvasOverlayRenderKey()
    {
        var builder = new StringBuilder();
        AppendProjectHierarchyDialogKey(builder);

        if (blockMutationDialog is not null)
        {
            builder.Append("|block:")
                .Append(blockMutationDialog.Mode)
                .Append(':')
                .Append(blockMutationDialog.NodeId)
                .Append(':')
                .Append(blockMutationDialog.SelectedActionId)
                .Append(':')
                .Append(blockMutationDialog.Error);
        }
        else
        {
            builder.Append("|block:");
        }

        if (subprojectTransferDialog is not null)
        {
            builder.Append("|subproject:")
                .Append(subprojectTransferDialog.SourceNodeId)
                .Append(':')
                .Append(subprojectTransferDialog.ProjectName)
                .Append(':')
                .Append(subprojectTransferDialog.Error);
        }
        else
        {
            builder.Append("|subproject:");
        }

        if (quickActionDialog is not null)
        {
            builder.Append("|quick:")
                .Append(quickActionDialog.NodeId)
                .Append(':')
                .Append(quickActionDialog.Title)
                .Append(':')
                .Append(quickActionDialog.PrimaryAction.Label);
        }
        else
        {
            builder.Append("|quick:");
        }

        if (previewNode is not null)
        {
            builder.Append("|preview:")
                .Append(previewNode.Id)
                .Append(':')
                .Append(ResolveAttachmentDisplayName(previewNode))
                .Append(':')
                .Append(ResolveAttachmentPreviewKind(previewNode));
        }
        else
        {
            builder.Append("|preview:");
        }

        return builder.ToString();
    }

    private string BuildSupportDialogRenderKey()
    {
        var builder = new StringBuilder();

        if (summaryDialog is not null)
        {
            builder.Append("summary:")
                .Append(summaryDialog.RootNodeId)
                .Append(':')
                .Append(summaryDialog.RootTitle)
                .Append(':')
                .Append(summaryDialog.Summary.Rows.Count)
                .Append(':')
                .Append(summaryDialog.Summary.CompletedCount)
                .Append(':')
                .Append(summaryDialog.Summary.ActiveCount)
                .Append(':')
                .Append(summaryDialog.Summary.BlockedCount)
                .Append(':')
                .Append(summaryDialog.Summary.ReviewCount)
                .Append(':')
                .Append(summaryDialog.Summary.UndatedCount);
        }
        else
        {
            builder.Append("summary:");
        }

        if (pendingDeletePrompt is not null)
        {
            builder.Append("|delete:")
                .Append(pendingDeletePrompt.NodeId)
                .Append(':')
                .Append(pendingDeletePrompt.Title)
                .Append(':')
                .Append(pendingDeletePrompt.ImpactCopy);
        }
        else
        {
            builder.Append("|delete:");
        }

        if (pendingTranscriptAction is not null)
        {
            builder.Append("|transcript:")
                .Append(pendingTranscriptAction.NodeId)
                .Append(':')
                .Append(pendingTranscriptAction.ActionKind)
                .Append(':')
                .Append(pendingTranscriptAction.SelectedProviderId?.ToString() ?? string.Empty)
                .Append(':')
                .Append(pendingTranscriptAction.Error);
        }
        else
        {
            builder.Append("|transcript:");
        }

        if (mermaidPreviewNode is not null)
        {
            builder.Append("|mermaid:")
                .Append(mermaidPreviewNode.Id)
                .Append(':')
                .Append(mermaidPreviewNode.Title);
        }
        else
        {
            builder.Append("|mermaid:");
        }

        return builder.ToString();
    }

    private void AppendProjectHierarchyDialogKey(StringBuilder builder)
    {
        if (projectHierarchyDialog is null)
        {
            builder.Append("hierarchy:");
            return;
        }

        builder.Append("hierarchy:")
            .Append(projectHierarchyDialog.Mode)
            .Append(':')
            .Append(projectHierarchyDialog.SubjectProjectId)
            .Append(':')
            .Append(projectHierarchyDialog.SelectedProjectId?.ToString() ?? string.Empty)
            .Append(':')
            .Append(projectHierarchyDialog.Error);
    }

    private Task ApplySelectionProgressAsync(int progress)
        => ApplyProgressAsync(selectedNodeIds, "progress", progress);

    private Task ApplySelectionPriorityAsync(int priority)
        => ApplyPriorityAsync(selectedNodeIds, priority);

    private Task ApplySelectionMarkerAsync(ProjectStructureSelectionMarkerRequest request)
        => ApplyMarkerAsync(selectedNodeIds, request.Badge, request.Tone, request.Label);

    private Task UpdateSelectionBorderNameAsync(string value)
    {
        selectionBorderName = value;
        return InvokeAsync(StateHasChanged);
    }

    private Task ExecuteSelectedInspectorActionAsync(string actionId)
        => selectedNode is null
            ? Task.CompletedTask
            : ExecuteInspectorActionAsync(selectedNode, actionId);

    private Task OpenSelectedAttachmentLocallyAsync()
        => selectedNode is null
            ? Task.CompletedTask
            : OpenAttachmentLocallyAsync(selectedNode);

    private Task OpenSelectedAttachmentPreviewAsync()
    {
        if (selectedNode is null)
        {
            return Task.CompletedTask;
        }

        OpenAttachmentPreview(selectedNode);
        return Task.CompletedTask;
    }

    private Task OpenSelectedMermaidViewerAsync()
    {
        if (selectedNode is null)
        {
            return Task.CompletedTask;
        }

        OpenMermaidViewer(selectedNode);
        return Task.CompletedTask;
    }
}

public sealed record ProjectStructureSelectionBadgePresentation(
    string Text,
    ProjectStructureSelectionBadgeStyle Style,
    string TestId);

public enum ProjectStructureSelectionBadgeStyle
{
    Standard,
    Uploaded,
    Scheduled,
    Synced,
    FileGeneric,
    FilePdf,
    FileExcel,
    FileDocx,
    FileMarkdown,
    FileMermaid,
    FileScreenshot,
    FileLog,
    FileArchive,
    FileAudio,
    FileJson,
    FileText
}
