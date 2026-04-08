using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace CanDoItAll.Modules.Workbench.Pages;

public partial class ProjectStructurePage
{
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

    private bool CanOpenSelectedSummary
        => selectedNode is not null &&
           surface is not null &&
           ProjectStructureSummaryBuilder.Build(surface, selectedNode).Rows.Count > 1;

    private bool IsReconnectMode => !string.IsNullOrWhiteSpace(reconnectNodeId);
    private bool IsDependencyMode => string.Equals(canvasToolMode, CanvasAuthoringMode.Dependency, StringComparison.Ordinal);
    private bool IsDeleteMode => string.Equals(canvasToolMode, CanvasAuthoringMode.Delete, StringComparison.Ordinal);

    private bool CanCreateTranscript(ProjectStructureNode? node)
        => node?.ObjectType == ProjectObjectType.Recording;

    private bool HasTranscriptActions(ProjectStructureNode? node)
        => node?.ObjectType == ProjectObjectType.Transcript;

    private static bool HasMermaidViewer(ProjectStructureNode? node)
        => node is not null &&
           node.ObjectType == ProjectObjectType.File &&
           string.Equals(node.ObjectSubtype, "mermaid", StringComparison.OrdinalIgnoreCase);

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
            await ProjectWorkbenchService.DeleteObjectAsync(ProjectId, targetNode.Id);
            await ReloadSurfaceAsync();
            workflowFeedback = $"{targetNode.Title} was deleted.";
            workflowFeedbackTone = "mint";
            return;
        }

        pendingDeletePrompt = prompt;
    }

    private async Task ConfirmDeleteAsync()
    {
        if (pendingDeletePrompt is null)
        {
            return;
        }

        var nodeId = pendingDeletePrompt.NodeId;
        pendingDeletePrompt = null;
        reconnectNodeId = null;
        await ProjectWorkbenchService.DeleteObjectAsync(ProjectId, nodeId);
        await ReloadSurfaceAsync();
        workflowFeedback = "The selected branch was deleted.";
        workflowFeedbackTone = "mint";
    }

    private void CancelDelete()
        => pendingDeletePrompt = null;

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
                mermaidText,
                summaryDialog.RootNodeId,
                null,
                null,
                null,
                null,
                "mermaid",
                null,
                ProjectObjectMetadataSerializer.Serialize(metadata)));

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

    private void OpenMermaidViewer(ProjectStructureNode node)
        => mermaidPreviewNode = node;

    private void CloseMermaidViewer()
        => mermaidPreviewNode = null;

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
        var descendantCount = CountDescendants(node.Id);
        var linkedNodeCount = CountLinkedNodes(node.Id);
        var dependencyLinkCount = CountDependencyLinks(node.Id);
        var requiresConfirmation = node.ObjectType != ProjectObjectType.Note ||
                                   descendantCount > 0 ||
                                   linkedNodeCount > 1 ||
                                   HasManagedAttachment(node) ||
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

        var impactCopy = string.Join(" ", impactParts);

        return new ProjectStructureDeletePrompt(
            node.Id,
            node.Title,
            descendantCount,
            requiresConfirmation,
            impactCopy);
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

        return string.Join(
            "-",
            value.Split(Path.GetInvalidFileNameChars().Concat([' ']).ToArray(), StringSplitOptions.RemoveEmptyEntries))
            .ToLowerInvariant();
    }

}
