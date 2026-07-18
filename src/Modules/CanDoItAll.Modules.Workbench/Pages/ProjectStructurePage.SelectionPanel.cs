using System.Text;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
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
            workflowFeedback,
            workflowFeedbackTone,
            CanApplySelectionStatus,
            selectionBorderName,
            !string.IsNullOrWhiteSpace(linkModeSourceId),
            !string.IsNullOrWhiteSpace(reconnectNodeId));
    }

    private ProjectStructureSelectionDetailState BuildSelectionDetailState(ProjectStructureNode node)
    {
        var attachmentPreview = BuildAttachmentPreviewCardState(node);
        var storageSummary = BuildSelectionStorageSummary(node);
        var workflowStatus = BuildWorkflowSelectionStatus(node);
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
            ProjectStructureNodeHelpers.ResolveProgressLabel(node),
            ProjectStructureNodeHelpers.ResolvePriorityLabel(node),
            ProjectStructureNodeHelpers.ResolveMarkerLabel(node),
            SelectedNodeBadgePresentations,
            storageSummary,
            attachmentPreview,
            workflowStatus,
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

    private ProjectStructureSelectionWorkflowStatusState? BuildWorkflowSelectionStatus(ProjectStructureNode node)
    {
        if (node.ObjectType != ProjectObjectType.WorkflowDefinition)
        {
            return null;
        }

        var metadata = ProjectObjectMetadataSerializer.Parse(node.MetadataJson).Workflow;
        if (metadata is null)
        {
            return null;
        }

        var cachedStatus = selectedWorkflowStatus is not null &&
                           string.Equals(selectedNode?.Id, node.Id, StringComparison.Ordinal)
            ? selectedWorkflowStatus
            : null;
        var workflowName = cachedStatus?.Summary.WorkflowName ?? metadata.WorkflowName;
        var state = cachedStatus?.State ?? metadata.LastRunState ?? WorkflowRunState.NotStarted;
        var message = ResolveWorkflowSelectionMessage(state, cachedStatus?.Message ?? metadata.LastRunSummary);
        var currentStep = cachedStatus?.CurrentStepIndex ?? metadata.LastStepIndex;
        var stepCount = cachedStatus?.StepCount ?? metadata.LastStepCount;
        var createdNodeIds = cachedStatus?.Summary.CreatedNodeIds ?? metadata.LastCreatedNodeIds;
        var createdAssetIds = cachedStatus?.Summary.CreatedAssetIds ?? metadata.LastCreatedAssetIds;
        var createdFilePaths = cachedStatus?.Summary.CreatedFilePaths ?? metadata.LastCreatedFilePaths;
        var lastUpdatedAtUtc = metadata.LastUpdatedAtUtc ?? metadata.LastStartedAtUtc;

        return new ProjectStructureSelectionWorkflowStatusState(
            string.IsNullOrWhiteSpace(workflowName) ? node.Title : workflowName,
            state.ToString(),
            node.Status,
            message,
            stepCount > 0 ? $"{Math.Clamp(currentStep, 0, stepCount)} / {stepCount}" : "Not started",
            cachedStatus is null ? ProjectStructureNodeHelpers.ResolveProgressLabel(node) : $"{cachedStatus.ProgressPercent}%",
            (cachedStatus?.RunId ?? metadata.LastRunId)?.ToString() ?? string.Empty,
            lastUpdatedAtUtc.HasValue ? lastUpdatedAtUtc.Value.ToLocalTime().ToString("g") : "Not run yet",
            createdNodeIds,
            createdAssetIds,
            createdFilePaths);
    }

    private static string ResolveWorkflowSelectionMessage(WorkflowRunState state, string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "Workflow is ready to start from project structure.";
        }

        return state == WorkflowRunState.Failed
            ? WorkflowFailureDisplayFormatter.ToUserMessage(message)
            : message.Trim();
    }

    private ProjectStructureAttachmentPreviewCardState? BuildAttachmentPreviewCardState(ProjectStructureNode node)
    {
        if (!ProjectStructureNodeHelpers.HasManagedAttachment(node))
        {
            return null;
        }

        return new ProjectStructureAttachmentPreviewCardState(
            ProjectStructureNodeHelpers.ResolveAttachmentPreviewKind(node),
            ProjectStructureNodeHelpers.ResolveAttachmentDisplayName(node),
            ProjectStructureNodeHelpers.ResolveAttachmentLeadCopy(node),
            node.MediaOriginalFileName ?? string.Empty,
            ProjectStructureNodeHelpers.ResolveAttachmentContentType(node),
            CanShowLocalOpen(node),
            ProjectStructureNodeHelpers.CanRenderAttachmentPreview(node),
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
            .Append(reconnectNodeId ?? string.Empty)
            .Append('|')
            .Append(workflowFeedback ?? string.Empty)
            .Append('|')
            .Append(workflowFeedbackTone);

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
            .Append(selectedDetail.StorageSummary?.Title ?? string.Empty)
            .Append('|')
            .Append(selectedDetail.StorageSummary?.Description ?? string.Empty)
            .Append('|')
            .Append(selectedDetail.AttachmentPreview?.DisplayName ?? string.Empty)
            .Append('|')
            .Append(selectedDetail.AttachmentPreview?.Kind ?? AttachmentPreviewKind.None)
            .Append('|')
            .Append(selectedDetail.AttachmentPreview?.LocalOpenFeedback ?? string.Empty)
            .Append('|')
            .Append(selectedDetail.WorkflowStatus?.State ?? string.Empty)
            .Append('|')
            .Append(selectedDetail.WorkflowStatus?.StepLabel ?? string.Empty)
            .Append('|')
            .Append(selectedDetail.WorkflowStatus?.ProgressLabel ?? string.Empty)
            .Append('|')
            .Append(selectedDetail.WorkflowStatus?.Message ?? string.Empty);

        if (selectedDetail.WorkflowStatus is not null)
        {
            foreach (var nodeId in selectedDetail.WorkflowStatus.CreatedNodeIds)
            {
                builder.Append('|')
                    .Append(nodeId);
            }

            foreach (var assetId in selectedDetail.WorkflowStatus.CreatedAssetIds)
            {
                builder.Append('|')
                    .Append(assetId);
            }

            foreach (var filePath in selectedDetail.WorkflowStatus.CreatedFilePaths)
            {
                builder.Append('|')
                    .Append(filePath);
            }
        }

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

        if (processLinkDialog is not null)
        {
            builder.Append("|process-link:")
                .Append(processLinkDialog.SourceNodeId)
                .Append(':')
                .Append(processLinkDialog.SelectedDefinitionId?.ToString() ?? string.Empty)
                .Append(':')
                .Append(processLinkDialog.Error);
        }
        else
        {
            builder.Append("|process-link:");
        }

        if (workflowAddDialog is not null)
        {
            builder.Append("|workflow-add:")
                .Append(workflowAddDialog.ParentNodeId)
                .Append(':')
                .Append(workflowAddDialog.SelectedWorkflowId?.ToString() ?? string.Empty)
                .Append(':')
                .Append(workflowAddDialog.SelectedVersionId?.ToString() ?? string.Empty)
                .Append(':')
                .Append(workflowAddDialog.InputSettings.IncludeParentSubtree)
                .Append(':')
                .Append(workflowAddDialog.InputSettings.IncludeAssets)
                .Append(':')
                .Append(workflowAddDialog.InputSettings.ManualInputJson)
                .Append(':')
                .Append(workflowAddDialog.Error);

            foreach (var source in workflowAddDialog.InputSettings.AdditionalSources)
            {
                builder.Append("|workflow-source:")
                    .Append(source.Kind)
                    .Append(':')
                    .Append(source.Key)
                    .Append(':')
                    .Append(source.Label)
                    .Append(':')
                    .Append(source.Value);
            }

            foreach (var section in workflowAddDialog.Preview.Sections)
            {
                builder.Append("|workflow-preview:")
                    .Append(section.Title)
                    .Append(':')
                    .Append(section.Summary);
            }
        }
        else
        {
            builder.Append("|workflow-add:");
        }

        if (workflowStartDialog is not null)
        {
            builder.Append("|workflow-start:")
                .Append(workflowStartDialog.NodeId)
                .Append(':')
                .Append(workflowStartDialog.IsBusy)
                .Append(':')
                .Append(workflowStartDialog.Status?.State.ToString() ?? string.Empty)
                .Append(':')
                .Append(workflowStartDialog.Status?.CurrentStepIndex ?? 0)
                .Append(':')
                .Append(workflowStartDialog.Status?.StepCount ?? 0)
                .Append(':')
                .Append(workflowStartDialog.SimulatedNodeIds.Count)
                .Append(':')
                .Append(workflowStartDialog.Error);
        }
        else
        {
            builder.Append("|workflow-start:");
        }

        if (processStartDialog is not null)
        {
            builder.Append("|process-start:")
                .Append(processStartDialog.NodeId)
                .Append(':')
                .Append(processStartDialog.TargetNodeId)
                .Append(':')
                .Append(processStartDialog.ProcessDefinitionId)
                .Append(':')
                .Append(processStartDialog.LaunchPlanId?.ToString() ?? string.Empty)
                .Append(':')
                .Append(processStartDialog.Stage)
                .Append(':')
                .Append(processStartDialog.IsBusy)
                .Append(':')
                .Append(processStartDialog.ConfirmHrManagerMatch)
                .Append(':')
                .Append(processStartDialog.AssignmentsReviewed)
                .Append(':')
                .Append(processStartDialog.ResolvedRoleCount)
                .Append(':')
                .Append(processStartDialog.RequiredGapCount)
                .Append(':')
                .Append(processStartDialog.StatusMessage)
                .Append(':')
                .Append(processStartDialog.Error);

            foreach (var role in processStartDialog.Roles)
            {
                builder.Append("|role:")
                    .Append(role.LaunchPlanRoleId)
                    .Append(':')
                    .Append(role.DisplayName)
                    .Append(':')
                    .Append(role.PreferredExecutorKind)
                    .Append(':')
                    .Append(role.IsRequired)
                    .Append(':')
                    .Append(role.IsResolved)
                    .Append(':')
                    .Append(role.RequiresProvisioning)
                    .Append(':')
                    .Append(role.SelectionSummary)
                    .Append(':')
                    .Append(role.ReadinessSummary);

                foreach (var candidate in role.Candidates)
                {
                    builder.Append("|candidate:")
                        .Append(candidate.CandidateId)
                        .Append(':')
                        .Append(candidate.DisplayName)
                        .Append(':')
                        .Append(candidate.ExecutorKind)
                        .Append(':')
                        .Append(candidate.ScoreLabel)
                        .Append(':')
                        .Append(candidate.IsSelected)
                        .Append(':')
                        .Append(candidate.IsRecommended)
                        .Append(':')
                        .Append(candidate.RequiresProvisioning)
                        .Append(':')
                        .Append(candidate.IsResolvable)
                        .Append(':')
                        .Append(candidate.RecommendationSummary)
                        .Append(':')
                        .Append(candidate.AvailabilitySummary);
                }
            }
        }
        else
        {
            builder.Append("|process-start:");
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
                .Append(ProjectStructureNodeHelpers.ResolveAttachmentDisplayName(previewNode))
                .Append(':')
                .Append(ProjectStructureNodeHelpers.ResolveAttachmentPreviewKind(previewNode));
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
            .Append(projectHierarchyDialog.CurrentParentProjectId?.ToString() ?? string.Empty)
            .Append(':')
            .Append(projectHierarchyDialog.SelectedProjectId?.ToString() ?? string.Empty)
            .Append(':')
            .Append(projectHierarchyDialog.Error);

        foreach (var project in projectHierarchyDialog.AvailableProjects)
        {
            builder.Append(':')
                .Append(project.Id);
        }
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

    private async Task OpenSelectedAttachmentPreviewAsync()
    {
        if (selectedNode is null)
        {
            return;
        }

        await OpenAttachmentPreviewAsync(selectedNode);
    }

    private async Task OpenSelectedMermaidViewerAsync()
    {
        if (selectedNode is null)
        {
            return;
        }

        await OpenMermaidViewerAsync(selectedNode);
        await InvokeAsync(StateHasChanged);
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
