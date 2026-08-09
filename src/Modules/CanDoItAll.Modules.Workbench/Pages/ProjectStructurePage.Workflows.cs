using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace CanDoItAll.Modules.Workbench.Pages;

public partial class ProjectStructurePage
{
    [Inject]
    private ProjectAssetCreationService AssetCreationService { get; set; } = default!;

    private static readonly IReadOnlyList<string> SummaryStatusOptions =
    [
        "Draft",
        "Planned",
        "In progress",
        "Review",
        "Blocked",
        "Done",
        "N/A"
    ];

    private string selectionBorderName = string.Empty;
    private string? reconnectNodeId;
    private ProjectStructureDeletePrompt? pendingDeletePrompt;
    private ProjectStructureSummaryDialogState? summaryDialog;
    private ProjectStructureTranscriptActionDialogState? pendingTranscriptAction;
    private ProjectStructureNode? mermaidPreviewNode;
    private string? workflowFeedback;
    private string workflowFeedbackTone = "neutral";
    private readonly List<ProjectStructureDeletionRecovery> pendingDeletionRecoveries = [];
    private readonly List<ProjectStructureDeletionCompletionNotice> deletionCompletionNotices = [];
    private bool isRetryingDeletionCleanup;

    private bool CanOpenSelectedSummary
        => selectedNode is not null &&
           surface is not null &&
           ProjectStructureSummaryBuilder.Build(surface, selectedNode).Rows.Count > 1;

    private bool IsReconnectMode => !string.IsNullOrWhiteSpace(reconnectNodeId);
    private bool IsDependencyMode => string.Equals(canvasToolMode, CanvasAuthoringMode.Dependency, StringComparison.Ordinal);
    private bool IsDeleteMode => string.Equals(canvasToolMode, CanvasAuthoringMode.Delete, StringComparison.Ordinal);
    private ProjectStructureNode? MermaidViewerNode => HasMermaidViewer(mermaidPreviewNode) ? mermaidPreviewNode : null;

    private bool CanCreateTranscript(ProjectStructureNode? node)
        => node?.ObjectType == ProjectObjectType.Recording;

    private bool HasTranscriptActions(ProjectStructureNode? node)
        => node?.ObjectType == ProjectObjectType.Transcript;

    private static bool HasMermaidViewer(ProjectStructureNode? node)
    {
        if (node is null || node.ObjectType != ProjectObjectType.File)
        {
            return false;
        }

        if (ProjectStructureNodeHelpers.CanRenderAttachmentPreview(node))
        {
            return false;
        }

        if (HasMermaidObjectSubtype(node.ObjectSubtype))
        {
            return true;
        }

        ProjectObjectMetadataEnvelope metadata;
        try
        {
            metadata = ProjectObjectMetadataSerializer.Parse(node.MetadataJson);
        }
        catch (InvalidOperationException)
        {
            metadata = new ProjectObjectMetadataEnvelope();
        }

        if (metadata.File?.FileSubtype == ProjectFileSubtype.Mermaid ||
            HasMermaidFileExtension(metadata.File?.ExternalPath))
        {
            return true;
        }

        return false;
    }

    private static bool HasMermaidObjectSubtype(string? objectSubtype)
        => string.Equals(objectSubtype, "mermaid", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(objectSubtype, "mmd", StringComparison.OrdinalIgnoreCase);

    private static bool HasMermaidFileExtension(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var extension = Path.GetExtension(path);
        return string.Equals(extension, ".mmd", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".mermaid", StringComparison.OrdinalIgnoreCase);
    }

    private async Task BeginReconnectAsync(string? nodeId = null)
    {
        reconnectNodeId = nodeId ?? selectedNode?.Id;
        await SetCanvasToolModeAsync(CanvasAuthoringMode.Select);
        await InvokeAsync(StateHasChanged);
    }

    private async Task DisconnectNodeAsync(string? nodeId = null)
    {
        var targetNode = ResolveNode(nodeId);
        if (targetNode is null)
        {
            return;
        }

        reconnectNodeId = null;
        await ProjectWorkbenchService.ReparentObjectAsync(ProjectId, targetNode.Id, null);
        await ReloadSurfaceAsync(targetNode.Id);
    }

    private async Task DeleteDependencyAsync(string? sourceNodeId, string? targetNodeId, string? linkKind)
    {
        if (string.IsNullOrWhiteSpace(sourceNodeId) ||
            string.IsNullOrWhiteSpace(targetNodeId) ||
            !Enum.TryParse<ProjectObjectLinkKind>(linkKind, ignoreCase: true, out var parsedKind))
        {
            return;
        }

        var removed = await ProjectWorkbenchService.UnlinkObjectsAsync(ProjectId, sourceNodeId, targetNodeId, parsedKind);
        if (!removed)
        {
            workflowFeedback = "The selected dependency could not be deleted.";
            workflowFeedbackTone = "warn";
            return;
        }

        workflowFeedback = "The dependency link was deleted.";
        workflowFeedbackTone = "mint";
        await ReloadSurfaceAsync(sourceNodeId);
    }

    private async Task DeleteNodeAsync(string? nodeId = null)
    {
        var targetNode = ResolveNode(nodeId);
        if (targetNode is null)
        {
            return;
        }

        var prompt = BuildDeletePrompt(targetNode);
        if (!prompt.RequiresConfirmation)
        {
            var deleted = await DeleteSelectedNodesAsync(
                [targetNode],
                $"{targetNode.Title} was deleted.",
                "The selected node could not be deleted.");
            if (!deleted)
            {
                return;
            }

            return;
        }

        pendingDeletePrompt = prompt;
        await InvokeAsync(StateHasChanged);
    }

    private async Task DeleteNodesAsync(IReadOnlyCollection<string> nodeIds)
    {
        var targetNodes = ResolveDeleteTargetNodes(nodeIds);
        if (targetNodes.Count == 0)
        {
            return;
        }

        if (targetNodes.Count == 1)
        {
            await DeleteNodeAsync(targetNodes[0].Id);
            return;
        }

        pendingDeletePrompt = BuildDeletePrompt(targetNodes);
        await InvokeAsync(StateHasChanged);
    }

    private async Task ConfirmDeleteAsync()
    {
        if (pendingDeletePrompt is null)
        {
            return;
        }

        var nodeIds = ResolvePendingDeleteNodeIds(pendingDeletePrompt);
        pendingDeletePrompt = null;
        reconnectNodeId = null;
        var targetNodes = ResolveDeleteTargetNodes(nodeIds);
        if (targetNodes.Count == 0)
        {
            workflowFeedback = "The selected node could not be found anymore.";
            workflowFeedbackTone = "warn";
            await ReloadSurfaceAsync();
            return;
        }

        var isBulk = targetNodes.Count > 1;
        await DeleteSelectedNodesAsync(
            targetNodes,
            isBulk ? $"{targetNodes.Count} selected branches were deleted." : "The selected branch was deleted.",
            isBulk ? "The selected branches could not be deleted." : "The selected branch could not be deleted.");
    }

    private void CancelDelete()
        => pendingDeletePrompt = null;

    private async Task<bool> DeleteSelectedNodesAsync(
        IReadOnlyList<ProjectStructureNode> targetNodes,
        string successMessage,
        string failureMessage)
    {
        var deletedAny = false;
        var failedAny = false;
        var deletionWarnings = new List<ProjectStructureDeletionWarning>();
        foreach (var requestedNode in targetNodes)
        {
            try
            {
                var targetNode = ResolveNode(requestedNode.Id) ?? requestedNode;
                var deletion = await ProjectWorkbenchService.DeleteObjectDetailedAsync(
                    ProjectId,
                    targetNode.Id);
                deletionWarnings.AddRange(deletion.DeletionWarnings);
                if (deletion.DeletedNodeCount > 0)
                {
                    deletedAny = true;
                    continue;
                }

                if (await TryDetachProjectedNodeAsync(targetNode))
                {
                    deletedAny = true;
                }
            }
            catch (ProjectStructureDeletionPartialCommitException exception)
            {
                deletedAny = true;
                failedAny = true;
                AddOrReplacePendingDeletionRecovery(exception.Recovery);
            }
            catch (Exception exception)
            {
                failedAny = true;
                Logger.LogWarning(
                    "Project structure deletion failed before durable completion. ProjectId={ProjectId} RootNodeId={RootNodeId} FailureType={FailureType}.",
                    ProjectId,
                    requestedNode.Id,
                    exception.GetType().Name);
            }
        }

        if (failedAny)
        {
            await ReloadSurfaceAsync();
            workflowFeedback = pendingDeletionRecoveries.Count > 0
                ? AppendDeletionWarningFeedback(
                    $"{pendingDeletionRecoveries.Count} deleted branch cleanup operation(s) remain pending. Retry cleanup to finish managed-storage and assignment reconciliation.",
                    deletionWarnings)
                : "One or more selected branches could not be deleted. No unsafe cleanup was attempted.";
            workflowFeedbackTone = "warn";
            return false;
        }

        if (deletedAny)
        {
            await ReloadSurfaceAsync();
            if (pendingDeletionRecoveries.Count > 0)
            {
                workflowFeedback =
                    $"The selected branch deletion completed, but {pendingDeletionRecoveries.Count} earlier cleanup operation(s) remain pending.";
                workflowFeedbackTone = "warn";
                return false;
            }

            workflowFeedback = AppendDeletionWarningFeedback(
                successMessage,
                deletionWarnings);
            workflowFeedbackTone = deletionWarnings.Count > 0 ? "warn" : "mint";
            return true;
        }

        workflowFeedback = failureMessage;
        workflowFeedbackTone = "warn";
        await InvokeAsync(StateHasChanged);
        return false;
    }

    private async Task RetryPendingDeletionCleanupAsync()
    {
        if (pendingDeletionRecoveries.Count == 0 || isRetryingDeletionCleanup)
        {
            return;
        }

        isRetryingDeletionCleanup = true;
        var pending = pendingDeletionRecoveries.ToArray();
        pendingDeletionRecoveries.Clear();
        var deletionWarnings = new List<ProjectStructureDeletionWarning>();
        try
        {
            foreach (var recovery in pending)
            {
                try
                {
                    var deletion = await ProjectWorkbenchService.RetryDeletionCleanupDetailedAsync(
                        recovery.ProjectId,
                        recovery.RootNodeId,
                        recovery.DurableMutationId,
                        deferredCompletionCts.Token);
                    deletionWarnings.AddRange(deletion.DeletionWarnings);
                }
                catch (ProjectStructureDeletionPartialCommitException exception)
                {
                    AddOrReplacePendingDeletionRecovery(exception.Recovery);
                }
                catch (Exception exception)
                {
                    AddOrReplacePendingDeletionRecovery(recovery);
                    Logger.LogWarning(
                        "Project structure durable deletion retry failed. ProjectId={ProjectId} MutationId={MutationId} FailureType={FailureType}.",
                        recovery.ProjectId,
                        recovery.DurableMutationId,
                        exception.GetType().Name);
                }
            }

            await ReloadSurfaceAsync();
            if (pendingDeletionRecoveries.Count == 0)
            {
                workflowFeedback = AppendDeletionWarningFeedback(
                    "Deleted branch cleanup completed.",
                    deletionWarnings);
                workflowFeedbackTone = deletionWarnings.Count > 0 ? "warn" : "mint";
            }
            else
            {
                workflowFeedback =
                    $"{pendingDeletionRecoveries.Count} deleted branch cleanup operation(s) are still pending. Retry cleanup again after resolving the storage or assignment failure.";
                workflowFeedbackTone = "warn";
            }
        }
        finally
        {
            isRetryingDeletionCleanup = false;
        }
    }

    private static string AppendDeletionWarningFeedback(
        string message,
        IReadOnlyCollection<ProjectStructureDeletionWarning> warnings)
    {
        if (warnings.Count == 0)
        {
            return message;
        }

        return $"{message} {string.Join(" ", warnings.Select(warning => $"{warning.Message} {warning.Remediation}"))}";
    }

    private static string BuildDeletionCompletionNoticeFeedback(
        IReadOnlyCollection<ProjectStructureDeletionCompletionNotice> notices)
    {
        return string.Join(" ", notices.SelectMany(notice => notice.Warnings.Select(warning =>
            $"Completed deletion {notice.DurableMutationId:D} for {notice.RootNodeId} retained {warning.RetainedObject.Provider} object {warning.RetainedObject.Locator} on storage {warning.RetainedObject.StorageId?.ToString("D") ?? "bootstrap"}: {warning.RetainedObject.Reason} {warning.Remediation}")));
    }

    private void AddOrReplacePendingDeletionRecovery(
        ProjectStructureDeletionRecovery recovery)
    {
        pendingDeletionRecoveries.RemoveAll(item =>
            item.DurableMutationId == recovery.DurableMutationId);
        pendingDeletionRecoveries.Add(recovery);
    }

    private IReadOnlyList<ProjectStructureNode> ResolveDeleteTargetNodes(IReadOnlyCollection<string> nodeIds)
    {
        if (surface is null || nodeIds.Count == 0)
        {
            return [];
        }

        var nodesById = surface.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        var targets = nodeIds
            .Where(nodeId => !string.IsNullOrWhiteSpace(nodeId))
            .Distinct(StringComparer.Ordinal)
            .Select(nodeId => nodesById.GetValueOrDefault(nodeId))
            .Where(node => node is not null)
            .Cast<ProjectStructureNode>()
            .ToList();
        if (targets.Count < 2)
        {
            return targets;
        }

        var selectedIds = targets
            .Select(node => node.Id)
            .ToHashSet(StringComparer.Ordinal);
        return targets
            .Where(node => !HasSelectedDeleteAncestor(node, selectedIds, nodesById))
            .ToList();
    }

    private static bool HasSelectedDeleteAncestor(
        ProjectStructureNode node,
        IReadOnlySet<string> selectedIds,
        IReadOnlyDictionary<string, ProjectStructureNode> nodesById)
    {
        var visitedIds = new HashSet<string>(StringComparer.Ordinal);
        var parentId = node.ParentId;
        while (!string.IsNullOrWhiteSpace(parentId) && visitedIds.Add(parentId))
        {
            if (selectedIds.Contains(parentId))
            {
                return true;
            }

            parentId = nodesById.TryGetValue(parentId, out var parent)
                ? parent.ParentId
                : null;
        }

        return false;
    }

    private static IReadOnlyList<string> ResolvePendingDeleteNodeIds(ProjectStructureDeletePrompt prompt)
        => prompt.NodeIds.Count > 0 ? prompt.NodeIds : [prompt.NodeId];

    private async Task<bool> TryDetachProjectedNodeAsync(ProjectStructureNode targetNode)
    {
        if (surface is null)
        {
            return false;
        }

        var removableLink = surface.Links
            .FirstOrDefault(link =>
                link.IsUserAuthored &&
                string.Equals(link.TargetId, targetNode.Id, StringComparison.Ordinal) &&
                (string.IsNullOrWhiteSpace(targetNode.ParentId) ||
                 string.Equals(link.SourceId, targetNode.ParentId, StringComparison.Ordinal)));
        if (removableLink is null)
        {
            removableLink = surface.Links
                .FirstOrDefault(link =>
                    link.IsUserAuthored &&
                    string.Equals(link.TargetId, targetNode.Id, StringComparison.Ordinal));
        }

        if (removableLink is null)
        {
            return false;
        }

        return await ProjectWorkbenchService.UnlinkObjectsAsync(
            ProjectId,
            removableLink.SourceId,
            removableLink.TargetId,
            removableLink.Kind);
    }

    private async Task OpenSummaryAsync(string? nodeId = null)
    {
        var targetNode = ResolveNode(nodeId);
        if (targetNode is null || surface is null)
        {
            return;
        }

        summaryDialog = new ProjectStructureSummaryDialogState(
            targetNode.Id,
            targetNode.Title,
            ProjectStructureSummaryBuilder.Build(surface, targetNode));
        await InvokeAsync(StateHasChanged);
    }

    private void CloseSummary()
        => summaryDialog = null;

    private async Task ChangeSummaryStatusAsync(string nodeId, ChangeEventArgs args)
    {
        var status = args.Value?.ToString()?.Trim();
        if (string.IsNullOrWhiteSpace(status) || summaryDialog is null)
        {
            return;
        }

        var updatedNodes = await ProjectWorkbenchService.UpdateObjectStatusesDetailedAsync(ProjectId, [nodeId], status);
        await ApplySurfaceNodeUpdatesAsync(updatedNodes);
    }

    private async Task ExportSummaryWorkbookAsync()
    {
        if (summaryDialog is null)
        {
            return;
        }

        var payload = ProjectStructureSummaryExporter.BuildWorkbook(summaryDialog.Summary);
        var created = await ProjectWorkbenchService.CreateObjectAsync(
            ProjectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.File,
                $"{summaryDialog.RootTitle} progress workbook",
                "Progress summary export",
                "Generated from the structure progress summary modal.",
                summaryDialog.RootNodeId,
                null,
                null,
                null,
                null,
                "excel",
                new ProjectObjectMediaPayload(
                    $"{SanitizeExportName(summaryDialog.RootTitle)}-progress-summary.xlsx",
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    Convert.ToBase64String(payload))));

        workflowFeedback = $"{created.Title} was exported as an Excel attachment.";
        workflowFeedbackTone = "mint";
        await ReloadSurfaceAsync(created.Id);
        await OpenSummaryAsync(summaryDialog.RootNodeId);
    }

    private async Task ExportSummaryGanttAsync()
    {
        if (summaryDialog is null)
        {
            return;
        }

        var mermaidText = ProjectStructureSummaryExporter.BuildMermaidGantt(
            summaryDialog.Summary,
            DateOnly.FromDateTime(DateTime.UtcNow));
        ProjectObjectMediaPayload media = await AssetCreationService.CreateTextAsync(
            ProjectFileSubtype.Mermaid,
            $"{SanitizeExportName(summaryDialog.RootTitle)}-progress-summary.mmd",
            mermaidText,
            deferredCompletionCts.Token);
        var metadata = new ProjectObjectMetadataEnvelope
        {
            File = new ProjectFileMetadata
            {
                FileSubtype = ProjectFileSubtype.Mermaid,
                MermaidDiagramKind = MermaidDiagramKind.Gantt
            }
        };

        var created = await ProjectWorkbenchService.CreateObjectAsync(
            ProjectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.File,
                $"{summaryDialog.RootTitle} gantt",
                "Progress summary export",
                "Generated from the structure progress summary modal.",
                summaryDialog.RootNodeId,
                null,
                null,
                null,
                null,
                "mermaid",
                media,
                ProjectObjectMetadataSerializer.Serialize(metadata)),
            deferredCompletionCts.Token);

        workflowFeedback = $"{created.Title} was exported as a Mermaid Gantt node.";
        workflowFeedbackTone = "mint";
        await ReloadSurfaceAsync(created.Id);
        await OpenSummaryAsync(summaryDialog.RootNodeId);
    }

    private async Task ExportMindmapImageAsync()
    {
        if (selectedNode is null || workbenchRef is null)
        {
            return;
        }

        string? base64;
        try
        {
            base64 = await workbenchRef.CaptureImageAsync();
        }
        catch (JSException)
        {
            workflowFeedback = "The canvas image could not be captured.";
            workflowFeedbackTone = "warn";
            return;
        }

        if (string.IsNullOrWhiteSpace(base64))
        {
            workflowFeedback = "The canvas image could not be captured.";
            workflowFeedbackTone = "warn";
            return;
        }

        var created = await ProjectWorkbenchService.CreateObjectAsync(
            ProjectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ImageAsset,
                $"{selectedNode.Title} mindmap image",
                "Canvas export",
                "Generated from the current structure canvas viewport.",
                selectedNode.Id,
                null,
                null,
                null,
                null,
                "png",
                new ProjectObjectMediaPayload(
                    $"{SanitizeExportName(selectedNode.Title)}-mindmap.png",
                    "image/png",
                    base64)));

        workflowFeedback = $"{created.Title} was exported as an image node.";
        workflowFeedbackTone = "mint";
        await ReloadSurfaceAsync(created.Id);
    }

    private async Task CreateTranscriptFromRecordingAsync(ProjectStructureNode? recordingNode = null)
    {
        var targetNode = recordingNode ?? selectedNode;
        if (targetNode is null || targetNode.ObjectType != ProjectObjectType.Recording)
        {
            return;
        }

        var recordingArtifactId = TryParseCustomNodeArtifactId(targetNode.Id);
        var metadata = new ProjectObjectMetadataEnvelope
        {
            Transcript = new ProjectTranscriptMetadata
            {
                TranscriptText = string.Empty
            }
        };
        var nodeReferences = new ProjectNodeReferenceCollection
        {
            TranscriptRecordingNodeId = recordingArtifactId
        };

        var created = await ProjectWorkbenchService.CreateObjectAsync(
            ProjectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Transcript,
                $"{targetNode.Title} transcript",
                "Generated from recording",
                $"Transcript scaffold created from recording '{targetNode.Title}'.",
                targetNode.Id,
                targetNode.X + 280,
                targetNode.Y + 120,
                null,
                null,
                string.Empty,
                null,
                ProjectObjectMetadataSerializer.Serialize(metadata),
                null,
                nodeReferences));

        await ProjectWorkbenchService.LinkObjectsAsync(ProjectId, targetNode.Id, created.Id, ProjectObjectLinkKind.DerivedFrom);
        await ReloadSurfaceAsync(created.Id);
    }

    private async Task OpenTranscriptActionAsync(ProjectLlmActionKind actionKind, string? nodeId = null)
    {
        var transcriptNode = ResolveNode(nodeId);
        if (transcriptNode is null || transcriptNode.ObjectType != ProjectObjectType.Transcript)
        {
            return;
        }

        var metadata = ProjectObjectMetadataSerializer.Parse(transcriptNode.MetadataJson);
        var providers = (await WorkspaceService.ListProviderProfilesAsync())
            .Where(profile => profile.IsEnabled)
            .ToList();
        var selectedProviderId = transcriptNode.NodeReferences?.TranscriptProviderProfileId ?? providers.FirstOrDefault()?.Id;

        pendingTranscriptAction = new ProjectStructureTranscriptActionDialogState(
            transcriptNode.Id,
            transcriptNode.Title,
            actionKind,
            selectedProviderId,
            metadata.Transcript?.LastProviderName ?? string.Empty,
            providers,
            string.Empty);
    }

    private void CancelTranscriptAction()
        => pendingTranscriptAction = null;

    private async Task ExecuteTranscriptActionAsync()
    {
        if (pendingTranscriptAction is null)
        {
            return;
        }

        var transcriptNode = ResolveNode(pendingTranscriptAction.NodeId);
        if (transcriptNode is null)
        {
            pendingTranscriptAction = null;
            return;
        }

        if (!pendingTranscriptAction.SelectedProviderId.HasValue)
        {
            pendingTranscriptAction = pendingTranscriptAction with { Error = "Select a provider profile before sending the transcript action." };
            return;
        }

        var provider = pendingTranscriptAction.Providers
            .FirstOrDefault(item => item.Id == pendingTranscriptAction.SelectedProviderId.Value);
        if (provider is null)
        {
            pendingTranscriptAction = pendingTranscriptAction with { Error = "The selected provider profile is no longer available." };
            return;
        }

        var metadata = ProjectObjectMetadataSerializer.Parse(transcriptNode.MetadataJson);
        var transcriptText = string.IsNullOrWhiteSpace(metadata.Transcript?.TranscriptText)
            ? transcriptNode.Notes
            : metadata.Transcript.TranscriptText;
        if (string.IsNullOrWhiteSpace(transcriptText))
        {
            pendingTranscriptAction = pendingTranscriptAction with { Error = "Transcript text is required before running an LLM action." };
            return;
        }

        var result = await ProviderExecutionService.SendAsync(
            new ProviderExecutionRequest(
                provider.Id,
                BuildTranscriptPrompt(pendingTranscriptAction.ActionKind, transcriptNode.Title, transcriptText),
                OutputFormat: "Markdown"));
        if (result.IsFailure || result.Value is null)
        {
            pendingTranscriptAction = pendingTranscriptAction with
            {
                Error = result.Errors.FirstOrDefault()?.Message ?? "The provider request failed."
            };
            return;
        }

        metadata.Transcript ??= new ProjectTranscriptMetadata();
        metadata.Transcript.TranscriptText = transcriptText;
        metadata.Transcript.LastActionKind = pendingTranscriptAction.ActionKind;
        metadata.Transcript.LastProviderName = provider.Name;
        metadata.Transcript.LastGeneratedAtUtc = DateTimeOffset.UtcNow;
        var updatedReferences = transcriptNode.NodeReferences?.Clone() ?? new ProjectNodeReferenceCollection();
        updatedReferences.TranscriptProviderProfileId = provider.Id;

        switch (pendingTranscriptAction.ActionKind)
        {
            case ProjectLlmActionKind.Summarize:
                metadata.Transcript.SummaryText = result.Value.OutputText.Trim();
                break;
            case ProjectLlmActionKind.FindMyTasks:
                metadata.Transcript.MyTasksText = result.Value.OutputText.Trim();
                break;
            case ProjectLlmActionKind.FindOthersDeliveries:
                metadata.Transcript.OthersDeliveriesText = result.Value.OutputText.Trim();
                break;
        }

        var updatedTranscriptNode = await ProjectWorkbenchService.UpdateObjectMetadataAsync(
            ProjectId,
            transcriptNode.Id,
            ProjectObjectMetadataSerializer.Serialize(metadata),
            status: "Review",
            nodeReferences: updatedReferences);

        workflowFeedback = $"{ResolveTranscriptActionLabel(pendingTranscriptAction.ActionKind)} completed through {provider.Name}.";
        workflowFeedbackTone = result.Value.ContainsWarnings ? "warn" : "mint";
        pendingTranscriptAction = null;
        if (updatedTranscriptNode is not null)
        {
            await ApplySurfaceNodeUpdatesAsync([updatedTranscriptNode]);
        }
    }

    private void HandleTranscriptProviderChanged(ChangeEventArgs args)
    {
        if (pendingTranscriptAction is null)
        {
            return;
        }

        var selectedProviderId = Guid.TryParse(args.Value?.ToString(), out var parsedProviderId)
            ? parsedProviderId
            : (Guid?)null;
        pendingTranscriptAction = pendingTranscriptAction with
        {
            SelectedProviderId = selectedProviderId,
            Error = string.Empty
        };
    }

    private async Task OpenMermaidViewerAsync(ProjectStructureNode node)
    {
        if (!HasMermaidViewer(node))
        {
            mermaidPreviewNode = null;
            await OpenAttachmentPreviewAsync(node);
            return;
        }

        await CloseAttachmentPreviewAsync();
        mermaidPreviewNode = node;
    }

    private void CloseMermaidViewer()
        => mermaidPreviewNode = null;

    private async Task EditMermaidPreviewNodeAsync(ProjectStructureNode node)
    {
        CloseMermaidViewer();
        await OpenEditDialogAsync(node);
    }

    private async Task HandleReconnectSelectionAsync(string? nodeId)
    {
        if (string.IsNullOrWhiteSpace(reconnectNodeId) || string.IsNullOrWhiteSpace(nodeId))
        {
            return;
        }

        if (string.Equals(reconnectNodeId, nodeId, StringComparison.Ordinal))
        {
            reconnectNodeId = null;
            return;
        }

        await ProjectWorkbenchService.ReparentObjectAsync(ProjectId, reconnectNodeId, nodeId);
        reconnectNodeId = null;
        await ReloadSurfaceAsync(nodeId);
    }

    private async Task<bool> TryAdoptMovedNodesIntoBordersAsync(CanvasWorkbenchNodesMovedEventArgs args)
    {
        if (surface is null || args.Positions.Count == 0)
        {
            return false;
        }

        var uiState = ResolveEditableUiState();
        if (uiState.GroupFrames.Count == 0)
        {
            return false;
        }

        var positionsByNodeId = surface.Nodes.ToDictionary(
            node => node.Id,
            node => (node.X, node.Y),
            StringComparer.Ordinal);
        foreach (var position in args.Positions)
        {
            positionsByNodeId[position.NodeId] = (position.X, position.Y);
        }

        var changed = false;
        foreach (var frame in uiState.GroupFrames)
        {
            var frameBounds = ResolveFrameBounds(frame.AnchorNodeIds, positionsByNodeId);
            if (frameBounds is null)
            {
                continue;
            }

            foreach (var position in args.Positions)
            {
                if (frame.AnchorNodeIds.Contains(position.NodeId, StringComparer.Ordinal))
                {
                    continue;
                }

                if (position.X >= frameBounds.Value.Left &&
                    position.X <= frameBounds.Value.Right &&
                    position.Y >= frameBounds.Value.Top &&
                    position.Y <= frameBounds.Value.Bottom)
                {
                    frame.AnchorNodeIds.Add(position.NodeId);
                    changed = true;
                }
            }
        }

        if (changed)
        {
            await PersistCanvasUiStateAsync(uiState);
        }

        return changed;
    }

    private (double Left, double Top, double Right, double Bottom)? ResolveFrameBounds(
        IReadOnlyCollection<string> anchorNodeIds,
        IReadOnlyDictionary<string, (double X, double Y)> positionsByNodeId)
    {
        var anchors = anchorNodeIds
            .Where(anchorId => positionsByNodeId.ContainsKey(anchorId))
            .Select(anchorId => positionsByNodeId[anchorId])
            .ToList();
        if (anchors.Count == 0)
        {
            return null;
        }

        const double nodeHalfWidth = 130;
        const double nodeHalfHeight = 72;
        const double paddingX = 38;
        const double paddingY = 42;

        var left = anchors.Min(anchor => anchor.X - nodeHalfWidth) - paddingX;
        var right = anchors.Max(anchor => anchor.X + nodeHalfWidth) + paddingX;
        var top = anchors.Min(anchor => anchor.Y - nodeHalfHeight) - paddingY;
        var bottom = anchors.Max(anchor => anchor.Y + nodeHalfHeight) + paddingY;
        return (left, top, right, bottom);
    }

    private ProjectStructureDeletePrompt BuildDeletePrompt(ProjectStructureNode node)
    {
        var affectedNodeIds = CollectDeleteSubtreeNodeIds([node.Id]);
        var descendantCount = Math.Max(0, affectedNodeIds.Count - 1);
        var managedAttachmentCount = CountManagedAttachments(affectedNodeIds);
        var linkedNodeCount = CountLinkedNodes(node.Id);
        var dependencyLinkCount = CountDependencyLinks(node.Id);
        var requiresConfirmation = node.ObjectType != ProjectObjectType.Note ||
                                   descendantCount > 0 ||
                                   linkedNodeCount > 1 ||
                                   managedAttachmentCount > 0 ||
                                   node.ArtifactId.HasValue;
        var impactParts = new List<string>();
        if (descendantCount > 0)
        {
            impactParts.Add($"This will delete {descendantCount + 1} nodes including child items.");
        }
        else
        {
            impactParts.Add("This will delete this node.");
        }

        if (dependencyLinkCount > 0)
        {
            impactParts.Add(
                linkedNodeCount > 0
                    ? $"It also removes {dependencyLinkCount} visible dependency link{(dependencyLinkCount == 1 ? string.Empty : "s")} touching {linkedNodeCount} connected node{(linkedNodeCount == 1 ? string.Empty : "s")}."
                    : $"It also removes {dependencyLinkCount} visible dependency link{(dependencyLinkCount == 1 ? string.Empty : "s")}.");
        }

        AppendManagedAttachmentDeletionImpact(impactParts, managedAttachmentCount);

        var impactCopy = string.Join(" ", impactParts);

        return new ProjectStructureDeletePrompt(
            node.Id,
            node.Title,
            descendantCount,
            requiresConfirmation,
            impactCopy);
    }

    private ProjectStructureDeletePrompt BuildDeletePrompt(IReadOnlyList<ProjectStructureNode> nodes)
    {
        if (nodes.Count == 1)
        {
            return BuildDeletePrompt(nodes[0]);
        }

        var nodeIds = nodes
            .Select(node => node.Id)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var affectedNodeIds = CollectDeleteSubtreeNodeIds(nodeIds);
        var descendantCount = Math.Max(0, affectedNodeIds.Count - nodeIds.Count);
        var managedAttachmentCount = CountManagedAttachments(affectedNodeIds);
        var dependencyLinkCount = CountDependencyLinks(affectedNodeIds);
        var linkedNodeCount = CountLinkedNodes(affectedNodeIds);
        var impactParts = new List<string>
        {
            descendantCount > 0
                ? $"This will delete {nodeIds.Count} selected root nodes and {affectedNodeIds.Count} total nodes including child items."
                : $"This will delete {nodeIds.Count} selected nodes."
        };

        if (dependencyLinkCount > 0)
        {
            impactParts.Add(
                linkedNodeCount > 0
                    ? $"It also removes {dependencyLinkCount} visible dependency link{(dependencyLinkCount == 1 ? string.Empty : "s")} touching {linkedNodeCount} connected node{(linkedNodeCount == 1 ? string.Empty : "s")} outside the selection."
                    : $"It also removes {dependencyLinkCount} visible dependency link{(dependencyLinkCount == 1 ? string.Empty : "s")} inside the selected branches.");
        }

        AppendManagedAttachmentDeletionImpact(impactParts, managedAttachmentCount);

        return new ProjectStructureDeletePrompt(
            nodeIds[0],
            $"{nodeIds.Count} selected nodes",
            descendantCount,
            RequiresConfirmation: true,
            string.Join(" ", impactParts))
        {
            NodeIds = nodeIds
        };
    }

    private int CountDescendants(string nodeId)
    {
        if (surface is null)
        {
            return 0;
        }

        var childrenByParent = surface.Nodes
            .Where(node => !string.IsNullOrWhiteSpace(node.ParentId))
            .GroupBy(node => node.ParentId!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(child => child.Id).ToList(), StringComparer.Ordinal);
        var count = 0;
        var queue = new Queue<string>();
        queue.Enqueue(nodeId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!childrenByParent.TryGetValue(current, out var children))
            {
                continue;
            }

            foreach (var child in children)
            {
                count++;
                queue.Enqueue(child);
            }
        }

        return count;
    }

    private int CountManagedAttachments(IReadOnlySet<string> nodeIds)
    {
        if (surface is null || nodeIds.Count == 0)
        {
            return 0;
        }

        return surface.Nodes.Count(node =>
            nodeIds.Contains(node.Id) &&
            !node.IsSystemManaged &&
            ProjectStructureNodeHelpers.HasManagedAttachment(node));
    }

    private static void AppendManagedAttachmentDeletionImpact(
        ICollection<string> impactParts,
        int managedAttachmentCount)
    {
        if (managedAttachmentCount == 0)
        {
            return;
        }

        impactParts.Add(managedAttachmentCount == 1
            ? "The associated managed file attachment will also be removed. Its stored file will be deleted when managed storage owns it and no other node references it; retained content will be reported."
            : $"{managedAttachmentCount} associated managed file attachments will also be removed. Stored files owned by managed storage and not referenced by other nodes will be deleted; retained content will be reported.");
    }

    private int CountLinkedNodes(string nodeId)
    {
        if (surface is null)
        {
            return 0;
        }

        return surface.Links
            .Where(link => string.Equals(link.SourceId, nodeId, StringComparison.Ordinal) ||
                           string.Equals(link.TargetId, nodeId, StringComparison.Ordinal))
            .Select(link => string.Equals(link.SourceId, nodeId, StringComparison.Ordinal) ? link.TargetId : link.SourceId)
            .Where(otherNodeId => !string.IsNullOrWhiteSpace(otherNodeId))
            .Distinct(StringComparer.Ordinal)
            .Count();
    }

    private int CountDependencyLinks(string nodeId)
    {
        if (surface is null)
        {
            return 0;
        }

        return surface.Links.Count(link =>
            link.Kind == ProjectObjectLinkKind.DependsOn &&
            (string.Equals(link.SourceId, nodeId, StringComparison.Ordinal) ||
             string.Equals(link.TargetId, nodeId, StringComparison.Ordinal)));
    }

    private HashSet<string> CollectDeleteSubtreeNodeIds(IReadOnlyCollection<string> rootNodeIds)
    {
        var collectedIds = new HashSet<string>(rootNodeIds, StringComparer.Ordinal);
        if (surface is null || rootNodeIds.Count == 0)
        {
            return collectedIds;
        }

        var childrenByParent = surface.Nodes
            .Where(node => !string.IsNullOrWhiteSpace(node.ParentId))
            .GroupBy(node => node.ParentId!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(child => child.Id).ToList(), StringComparer.Ordinal);
        var queue = new Queue<string>(rootNodeIds);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!childrenByParent.TryGetValue(current, out var children))
            {
                continue;
            }

            foreach (var child in children)
            {
                if (collectedIds.Add(child))
                {
                    queue.Enqueue(child);
                }
            }
        }

        return collectedIds;
    }

    private int CountLinkedNodes(IReadOnlySet<string> nodeIds)
    {
        if (surface is null || nodeIds.Count == 0)
        {
            return 0;
        }

        return surface.Links
            .Where(link => nodeIds.Contains(link.SourceId) || nodeIds.Contains(link.TargetId))
            .Select(link => nodeIds.Contains(link.SourceId) ? link.TargetId : link.SourceId)
            .Where(otherNodeId => !string.IsNullOrWhiteSpace(otherNodeId) && !nodeIds.Contains(otherNodeId))
            .Distinct(StringComparer.Ordinal)
            .Count();
    }

    private int CountDependencyLinks(IReadOnlySet<string> nodeIds)
    {
        if (surface is null || nodeIds.Count == 0)
        {
            return 0;
        }

        return surface.Links.Count(link =>
            link.Kind == ProjectObjectLinkKind.DependsOn &&
            (nodeIds.Contains(link.SourceId) || nodeIds.Contains(link.TargetId)));
    }

    private ProjectStructureNode? ResolveNode(string? nodeId)
        => string.IsNullOrWhiteSpace(nodeId)
            ? selectedNode
            : surface?.Nodes.FirstOrDefault(node => string.Equals(node.Id, nodeId, StringComparison.Ordinal));

    private static Guid? TryParseCustomNodeArtifactId(string nodeId)
        => nodeId.StartsWith("custom:", StringComparison.OrdinalIgnoreCase) &&
           Guid.TryParse(nodeId["custom:".Length..], out var parsed)
            ? parsed
            : null;

    private static string BuildTranscriptPrompt(ProjectLlmActionKind actionKind, string title, string transcriptText)
        => actionKind switch
        {
            ProjectLlmActionKind.Summarize =>
                $"Summarize the transcript '{title}'. Focus on decisions, risks, follow-ups, and unresolved questions.\n\nTranscript:\n{transcriptText}",
            ProjectLlmActionKind.FindMyTasks =>
                $"Read the transcript '{title}' and extract only tasks, commitments, or follow-ups assigned to the requester. Use a concise markdown checklist.\n\nTranscript:\n{transcriptText}",
            _ =>
                $"Read the transcript '{title}' and extract only deliveries, promises, or handoffs that other people owe to the requester. Use a concise markdown checklist.\n\nTranscript:\n{transcriptText}"
        };

    private static string ResolveTranscriptActionLabel(ProjectLlmActionKind actionKind)
        => actionKind switch
        {
            ProjectLlmActionKind.FindMyTasks => "Find my tasks",
            ProjectLlmActionKind.FindOthersDeliveries => "Find others delivery to me",
            _ => "Summarize"
        };

    private static string SanitizeExportName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "project-structure";
        }

        string displayName = string.Join(
            "-",
            value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .ToLowerInvariant();
        return PortablePhysicalFileNamePolicy.Encode(displayName).PhysicalName;
    }

}
