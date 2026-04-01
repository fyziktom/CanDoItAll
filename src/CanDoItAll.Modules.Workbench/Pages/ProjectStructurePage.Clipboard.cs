using System.Text.Json;
using CanDoItAll.Components.CanvasLib;
using Microsoft.JSInterop;

namespace CanDoItAll.Modules.Workbench.Pages;

public partial class ProjectStructurePage
{
    private static readonly JsonSerializerOptions ClipboardSerializerOptions = new(JsonSerializerDefaults.Web);

    private sealed record ProjectStructureClipboardPayload(
        string? Operation,
        string? SurfaceId,
        IReadOnlyList<string>? SelectedNodeIds);

    private sealed record ProjectStructureClipboardPasteEnvelope(
        string? PayloadJson,
        ProjectStructureClipboardAnchor? AnchorWorld,
        string? SurfaceId);

    private sealed record ProjectStructureClipboardAnchor(double X, double Y);

    private sealed record ProjectStructureCutClipboardState(
        string SurfaceId,
        IReadOnlyList<string> RootNodeIds,
        IReadOnlyList<CanvasWorkbenchNodePositionChange> Positions,
        double CenterX,
        double CenterY);

    private ProjectStructureCutClipboardState? cutClipboardState;

    private async Task HandleClipboardRequestedAsync(CanvasWorkbenchClipboardRequest request)
    {
        switch (request.ActionId)
        {
            case "cut":
                HandleCutClipboardRequest(request.PayloadJson);
                await InvokeAsync(StateHasChanged);
                break;
            case "paste":
                await PasteCutClipboardAsync(request.PayloadJson);
                break;
            case "copy":
                cutClipboardState = null;
                await InvokeAsync(StateHasChanged);
                break;
            case "duplicate":
                cutClipboardState = null;
                workflowFeedback = "Duplicate is not supported on the project structure canvas. Use cut and paste to move a subtree explicitly.";
                workflowFeedbackTone = "warn";
                await InvokeAsync(StateHasChanged);
                break;
        }
    }

    private async Task<bool> TryHandleCopyActionAsync(string actionId, string? nodeId)
    {
        var targetNode = ResolveNode(nodeId);
        if (targetNode is null)
        {
            return false;
        }

        switch (actionId)
        {
            case "copy-id":
                await CopyToClipboardAsync(targetNode.Id, $"{targetNode.Title} id was copied.");
                return true;
            case "copy-subtree-ids":
                var subtreeText = BuildSubtreeIdCopyText(targetNode.Id);
                if (string.IsNullOrWhiteSpace(subtreeText))
                {
                    workflowFeedback = "The selected node does not expose an id tree to copy.";
                    workflowFeedbackTone = "warn";
                    await InvokeAsync(StateHasChanged);
                    return true;
                }

                await CopyToClipboardAsync(subtreeText, $"{targetNode.Title} id tree was copied.");
                return true;
            default:
                return false;
        }
    }

    private void HandleCutClipboardRequest(string payloadJson)
    {
        var payload = ParseClipboardPayload(payloadJson);
        if (payload?.SelectedNodeIds is null || payload.SelectedNodeIds.Count == 0 || canvasSurface is null)
        {
            cutClipboardState = null;
            workflowFeedback = "Select at least one node before using cut.";
            workflowFeedbackTone = "warn";
            return;
        }

        var rootNodes = ResolveSubtreeRootNodes(payload.SelectedNodeIds, IsMovableCanvasNode);
        if (rootNodes.Count == 0)
        {
            cutClipboardState = null;
            workflowFeedback = "The selected nodes cannot be moved through the clipboard bridge.";
            workflowFeedbackTone = "warn";
            return;
        }

        var includedNodes = new List<ProjectStructureNode>();
        var includedNodeIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var rootNode in rootNodes)
        {
            foreach (var node in ResolveSubtreeNodes(rootNode.Id, includeRoot: true, IsMovableCanvasNode))
            {
                if (includedNodeIds.Add(node.Id))
                {
                    includedNodes.Add(node);
                }
            }
        }

        if (includedNodes.Count == 0)
        {
            cutClipboardState = null;
            workflowFeedback = "The selected nodes cannot be moved through the clipboard bridge.";
            workflowFeedbackTone = "warn";
            return;
        }

        var minX = includedNodes.Min(node => node.X);
        var maxX = includedNodes.Max(node => node.X);
        var minY = includedNodes.Min(node => node.Y);
        var maxY = includedNodes.Max(node => node.Y);
        cutClipboardState = new ProjectStructureCutClipboardState(
            canvasSurface.SurfaceId,
            rootNodes.Select(node => node.Id).ToList(),
            includedNodes
                .Select(node => new CanvasWorkbenchNodePositionChange(node.Id, node.X, node.Y))
                .ToList(),
            (minX + maxX) / 2d,
            (minY + maxY) / 2d);

        workflowFeedback = includedNodes.Count == 1
            ? "Cut buffer is ready. Paste will move the selected node."
            : $"Cut buffer is ready. Paste will move {includedNodes.Count} nodes.";
        workflowFeedbackTone = "accent";
    }

    private async Task PasteCutClipboardAsync(string payloadJson)
    {
        if (cutClipboardState is null || canvasSurface is null)
        {
            workflowFeedback = "The cut buffer is empty. Use Ctrl+X on a node first.";
            workflowFeedbackTone = "warn";
            await InvokeAsync(StateHasChanged);
            return;
        }

        var envelope = JsonSerializer.Deserialize<ProjectStructureClipboardPasteEnvelope>(payloadJson, ClipboardSerializerOptions);
        if (envelope?.AnchorWorld is null ||
            !string.Equals(envelope.SurfaceId, cutClipboardState.SurfaceId, StringComparison.Ordinal) ||
            !string.Equals(canvasSurface.SurfaceId, cutClipboardState.SurfaceId, StringComparison.Ordinal))
        {
            workflowFeedback = "Paste is only available on the same canvas surface that captured the cut selection.";
            workflowFeedbackTone = "warn";
            await InvokeAsync(StateHasChanged);
            return;
        }

        var deltaX = envelope.AnchorWorld.X - cutClipboardState.CenterX;
        var deltaY = envelope.AnchorWorld.Y - cutClipboardState.CenterY;
        var requestedPositions = cutClipboardState.Positions
            .Select(position => new ProjectNodeMoveRequest(position.NodeId, position.X + deltaX, position.Y + deltaY))
            .ToList();
        var updatedNodeIds = await ProjectWorkbenchService.MoveObjectsAsync(ProjectId, requestedPositions);
        if (updatedNodeIds.Count == 0)
        {
            workflowFeedback = "The cut selection could not be pasted on this canvas.";
            workflowFeedbackTone = "warn";
            await InvokeAsync(StateHasChanged);
            return;
        }

        var committedPositions = requestedPositions
            .Where(position => updatedNodeIds.Contains(position.NodeId, StringComparer.Ordinal))
            .Select(position => new CanvasWorkbenchNodePositionChange(position.NodeId, position.X, position.Y))
            .ToList();
        TryPatchSurfaceNodePositions(committedPositions);
        selectedNodeIds = cutClipboardState.RootNodeIds
            .Where(nodeId => surface?.Nodes.Any(node => string.Equals(node.Id, nodeId, StringComparison.Ordinal)) == true)
            .ToList();
        cutClipboardState = null;
        RefreshCanvasSurface();
        workflowFeedback = committedPositions.Count == 1
            ? "The cut selection was pasted."
            : $"The cut selection was pasted across {committedPositions.Count} nodes.";
        workflowFeedbackTone = "mint";
        await InvokeAsync(StateHasChanged);
    }

    private async Task CopyToClipboardAsync(string text, string successMessage)
    {
        try
        {
            await JSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", text);
            workflowFeedback = successMessage;
            workflowFeedbackTone = "mint";
        }
        catch (JSException)
        {
            workflowFeedback = "Clipboard access was blocked by the browser.";
            workflowFeedbackTone = "warn";
        }

        await InvokeAsync(StateHasChanged);
    }

    private static ProjectStructureClipboardPayload? ParseClipboardPayload(string payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ProjectStructureClipboardPayload>(payloadJson, ClipboardSerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
