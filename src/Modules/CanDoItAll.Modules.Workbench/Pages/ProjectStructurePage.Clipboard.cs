using CanDoItAll.Components.CanvasLib;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace CanDoItAll.Modules.Workbench.Pages;

public partial class ProjectStructurePage
{
    private sealed record ProjectStructureClipboardState(
        CanvasWorkbenchClipboardAction Operation,
        Guid ProjectId,
        string SurfaceId,
        IReadOnlyList<string> RootNodeIds);

    private ProjectStructureClipboardState? clipboardState;
    private bool isClipboardPasteInProgress;

    private async Task HandleClipboardRequestedAsync(CanvasWorkbenchClipboardRequest request)
    {
        InvalidateClipboardStateForCurrentSurface();

        switch (request.Action)
        {
            case CanvasWorkbenchClipboardAction.Copy:
            case CanvasWorkbenchClipboardAction.Cut:
                CaptureClipboardSelection(request);
                await InvokeAsync(StateHasChanged);
                break;
            case CanvasWorkbenchClipboardAction.Paste:
                await PasteClipboardAsync(request);
                break;
            case CanvasWorkbenchClipboardAction.Duplicate:
                SetClipboardFeedback(
                    "Duplicate is not supported on the project structure canvas. Use copy and paste to duplicate a subtree explicitly.",
                    "warn");
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
            case "copy-info":
                await CopyToClipboardAsync(BuildNodeInfoCopyText(targetNode), $"{targetNode.Title} info was copied.");
                return true;
            case "copy-subtree-ids":
                var subtreeText = BuildSubtreeIdCopyText(targetNode.Id);
                if (string.IsNullOrWhiteSpace(subtreeText))
                {
                    SetClipboardFeedback("The selected node does not expose a tree to copy.", "warn");
                    await InvokeAsync(StateHasChanged);
                    return true;
                }

                await CopyToClipboardAsync(subtreeText, $"{targetNode.Title} tree info was copied.");
                return true;
            default:
                return false;
        }
    }

    private void CaptureClipboardSelection(CanvasWorkbenchClipboardRequest request)
    {
        clipboardState = null;

        if (!IsActiveClipboardSurface(request.SurfaceId))
        {
            SetClipboardFeedback("Copy and cut are only available on the active project structure canvas.", "warn");
            return;
        }

        if (request.SelectedNodeIds is not { Count: > 0 } ||
            request.SelectedNodeIds.Any(string.IsNullOrWhiteSpace))
        {
            SetClipboardFeedback("Select one or more editable nodes before using copy or cut.", "warn");
            return;
        }

        var sourceNodeIds = request.SelectedNodeIds
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var nodesById = surface!.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        var missingNodeIds = sourceNodeIds
            .Where(nodeId => !nodesById.ContainsKey(nodeId))
            .ToList();
        if (missingNodeIds.Count > 0)
        {
            Logger.LogWarning(
                "Project structure clipboard capture rejected stale nodes. ProjectId={ProjectId} SurfaceId={SurfaceId} NodeIds={NodeIds}.",
                ProjectId,
                request.SurfaceId,
                missingNodeIds);
            SetClipboardFeedback("The clipboard selection is stale. Reload the project structure and select the nodes again.", "warn");
            return;
        }

        var projectedNodeIds = sourceNodeIds
            .Where(nodeId => nodesById[nodeId].IsSystemManaged)
            .ToList();
        if (projectedNodeIds.Count > 0)
        {
            Logger.LogWarning(
                "Project structure clipboard capture rejected projected nodes. ProjectId={ProjectId} SurfaceId={SurfaceId} NodeIds={NodeIds}.",
                ProjectId,
                request.SurfaceId,
                projectedNodeIds);
            SetClipboardFeedback("Projected and system-managed nodes cannot be copied or cut.", "warn");
            return;
        }

        var rootNodes = ResolveSubtreeRootNodes(sourceNodeIds, IsClipboardSourceNode);
        if (rootNodes.Count == 0)
        {
            Logger.LogWarning(
                "Project structure clipboard capture could not normalize an editable forest. ProjectId={ProjectId} SurfaceId={SurfaceId} NodeIds={NodeIds}.",
                ProjectId,
                request.SurfaceId,
                sourceNodeIds);
            SetClipboardFeedback("The selected nodes do not form an editable project subtree.", "warn");
            return;
        }

        clipboardState = new ProjectStructureClipboardState(
            request.Action,
            ProjectId,
            request.SurfaceId,
            rootNodes.Select(node => node.Id).ToArray());

        var operationLabel = request.Action == CanvasWorkbenchClipboardAction.Copy ? "Copy" : "Cut";
        SetClipboardFeedback(
            rootNodes.Count == 1
                ? $"{operationLabel} buffer is ready. Select a destination node and paste."
                : $"{operationLabel} buffer is ready with {rootNodes.Count} branches. Select one destination node and paste.",
            "accent");
    }

    private async Task PasteClipboardAsync(CanvasWorkbenchClipboardRequest request)
    {
        if (isClipboardPasteInProgress)
        {
            SetClipboardFeedback("A clipboard paste is already in progress.", "warn");
            await InvokeAsync(StateHasChanged);
            return;
        }

        var capturedState = clipboardState;
        if (capturedState is null)
        {
            SetClipboardFeedback("The project structure clipboard is empty. Use copy or cut first.", "warn");
            await InvokeAsync(StateHasChanged);
            return;
        }

        if (!IsActiveClipboardSurface(request.SurfaceId) ||
            !string.Equals(request.SurfaceId, capturedState.SurfaceId, StringComparison.Ordinal))
        {
            SetClipboardFeedback("Paste is only available on the canvas surface where the nodes were copied or cut.", "warn");
            await InvokeAsync(StateHasChanged);
            return;
        }

        if (request.SelectedNodeIds is not { Count: 1 } ||
            string.IsNullOrWhiteSpace(request.SelectedNodeIds[0]))
        {
            SetClipboardFeedback("Select exactly one destination node before pasting.", "warn");
            await InvokeAsync(StateHasChanged);
            return;
        }

        var destinationNodeId = request.SelectedNodeIds[0];
        var destinationNode = surface!.Nodes.FirstOrDefault(node =>
            string.Equals(node.Id, destinationNodeId, StringComparison.Ordinal));
        if (destinationNode is null)
        {
            Logger.LogWarning(
                "Project structure clipboard paste rejected a stale destination. ProjectId={ProjectId} SurfaceId={SurfaceId} DestinationNodeId={DestinationNodeId}.",
                ProjectId,
                request.SurfaceId,
                destinationNodeId);
            SetClipboardFeedback("The selected paste destination is stale. Reload the project structure and try again.", "warn");
            await InvokeAsync(StateHasChanged);
            return;
        }

        if (ProjectWorkbenchGraphConventions.TryResolveProjectHierarchyNode(
                destinationNodeId,
                out var destinationNodeKind,
                out var destinationProjectId) &&
            (destinationNodeKind != ProjectHierarchyNodeKind.ActiveProject ||
                destinationProjectId != ProjectId ||
                !string.Equals(
                    destinationNodeId,
                    ProjectWorkbenchGraphConventions.BuildProjectRootNodeKey(ProjectId),
                    StringComparison.Ordinal)))
        {
            Logger.LogWarning(
                "Project structure clipboard paste rejected a projected project destination. ProjectId={ProjectId} DestinationNodeId={DestinationNodeId} DestinationNodeKind={DestinationNodeKind} DestinationProjectId={DestinationProjectId}.",
                ProjectId,
                destinationNodeId,
                destinationNodeKind,
                destinationProjectId);
            SetClipboardFeedback(
                "Projected parent and subproject nodes cannot receive pasted children. Use the project transfer action instead.",
                "warn");
            await InvokeAsync(StateHasChanged);
            return;
        }

        if (capturedState.Operation == CanvasWorkbenchClipboardAction.Cut &&
            WouldCreateClipboardCycle(capturedState.RootNodeIds, destinationNodeId))
        {
            Logger.LogWarning(
                "Project structure cut paste rejected a hierarchy cycle. ProjectId={ProjectId} SourceRootNodeIds={SourceRootNodeIds} DestinationNodeId={DestinationNodeId}.",
                ProjectId,
                capturedState.RootNodeIds,
                destinationNodeId);
            SetClipboardFeedback("The cut selection cannot be pasted into itself or one of its descendants.", "warn");
            await InvokeAsync(StateHasChanged);
            return;
        }

        isClipboardPasteInProgress = true;
        try
        {
            IReadOnlyList<string> committedRootNodeIds;
            try
            {
                committedRootNodeIds = capturedState.Operation switch
                {
                    CanvasWorkbenchClipboardAction.Copy =>
                        (await ProjectWorkbenchService.CopySubtreesAsync(
                            capturedState.ProjectId,
                            capturedState.RootNodeIds,
                            destinationNodeId)).RootNodeIds,
                    CanvasWorkbenchClipboardAction.Cut =>
                        (await ProjectWorkbenchService.ReparentSubtreesAsync(
                            capturedState.ProjectId,
                            capturedState.RootNodeIds,
                            destinationNodeId))
                        .Select(node => node.Id)
                        .ToList(),
                    _ => throw new InvalidOperationException("The clipboard buffer does not contain a pasteable operation.")
                };
            }
            catch (ArgumentException exception)
            {
                Logger.LogWarning(
                    exception,
                    "Project structure clipboard paste received invalid input. ProjectId={ProjectId} Operation={Operation} SourceRootNodeIds={SourceRootNodeIds} DestinationNodeId={DestinationNodeId}.",
                    capturedState.ProjectId,
                    capturedState.Operation,
                    capturedState.RootNodeIds,
                    destinationNodeId);
                SetClipboardFeedback("The clipboard paste request is invalid. Select the source and destination again.", "warn");
                return;
            }
            catch (InvalidOperationException exception)
            {
                Logger.LogWarning(
                    exception,
                    "Project structure clipboard paste was rejected. ProjectId={ProjectId} Operation={Operation} SourceRootNodeIds={SourceRootNodeIds} DestinationNodeId={DestinationNodeId}.",
                    capturedState.ProjectId,
                    capturedState.Operation,
                    capturedState.RootNodeIds,
                    destinationNodeId);
                SetClipboardFeedback(
                    capturedState.Operation == CanvasWorkbenchClipboardAction.Cut
                        ? "The cut paste was rejected because its source or destination is stale, invalid, or would create a hierarchy cycle."
                        : "The copy paste was rejected because its source or destination is stale, invalid, or no longer editable.",
                    "warn");
                return;
            }
            catch (Exception exception)
            {
                Logger.LogError(
                    exception,
                    "Project structure clipboard paste failed. ProjectId={ProjectId} Operation={Operation} SourceRootNodeIds={SourceRootNodeIds} DestinationNodeId={DestinationNodeId}.",
                    capturedState.ProjectId,
                    capturedState.Operation,
                    capturedState.RootNodeIds,
                    destinationNodeId);
                SetClipboardFeedback("The clipboard paste failed unexpectedly. No local canvas state was changed.", "warn");
                return;
            }

            committedRootNodeIds = committedRootNodeIds
                .Where(nodeId => !string.IsNullOrWhiteSpace(nodeId))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (committedRootNodeIds.Count == 0)
            {
                Logger.LogWarning(
                    "Project structure clipboard paste returned no committed roots. ProjectId={ProjectId} Operation={Operation} SourceRootNodeIds={SourceRootNodeIds} DestinationNodeId={DestinationNodeId}.",
                    capturedState.ProjectId,
                    capturedState.Operation,
                    capturedState.RootNodeIds,
                    destinationNodeId);
                SetClipboardFeedback("The clipboard selection could not be pasted under the selected destination.", "warn");
                return;
            }

            if (capturedState.Operation == CanvasWorkbenchClipboardAction.Cut &&
                ReferenceEquals(clipboardState, capturedState))
            {
                clipboardState = null;
            }

            if (capturedState.ProjectId != ProjectId ||
                !IsActiveClipboardSurface(capturedState.SurfaceId))
            {
                Logger.LogInformation(
                    "Project structure clipboard paste committed after the active surface changed. ProjectId={ProjectId} Operation={Operation} CommittedRootNodeIds={CommittedRootNodeIds} DestinationNodeId={DestinationNodeId}.",
                    capturedState.ProjectId,
                    capturedState.Operation,
                    committedRootNodeIds,
                    destinationNodeId);
                return;
            }

            selectedNodeIds = committedRootNodeIds.ToList();
            try
            {
                await ReloadSurfaceAsync();
            }
            catch (Exception exception)
            {
                Logger.LogError(
                    exception,
                    "Project structure clipboard paste committed but the surface reload failed. ProjectId={ProjectId} Operation={Operation} CommittedRootNodeIds={CommittedRootNodeIds} DestinationNodeId={DestinationNodeId}.",
                    capturedState.ProjectId,
                    capturedState.Operation,
                    committedRootNodeIds,
                    destinationNodeId);
                SetClipboardFeedback("The clipboard paste was saved, but the canvas could not reload. Reload the page before making another change.", "warn");
                return;
            }

            var operationLabel = capturedState.Operation == CanvasWorkbenchClipboardAction.Copy ? "Copied" : "Moved";
            SetClipboardFeedback(
                committedRootNodeIds.Count == 1
                    ? $"{operationLabel} branch was pasted under {destinationNode.Title}."
                    : $"{operationLabel} {committedRootNodeIds.Count} branches were pasted under {destinationNode.Title}.",
                "mint");
        }
        finally
        {
            isClipboardPasteInProgress = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private bool IsActiveClipboardSurface(string surfaceId)
        => surface is not null &&
           canvasSurface is not null &&
           !string.IsNullOrWhiteSpace(surfaceId) &&
           string.Equals(surfaceId, canvasSurface.SurfaceId, StringComparison.Ordinal);

    private void InvalidateClipboardStateForCurrentSurface()
    {
        if (clipboardState is null)
        {
            return;
        }

        if (clipboardState.ProjectId != ProjectId ||
            canvasSurface is null ||
            !string.Equals(clipboardState.SurfaceId, canvasSurface.SurfaceId, StringComparison.Ordinal))
        {
            clipboardState = null;
        }
    }

    private static bool IsClipboardSourceNode(ProjectStructureNode node)
        => !node.IsSystemManaged;

    private bool WouldCreateClipboardCycle(
        IReadOnlyCollection<string> sourceRootNodeIds,
        string destinationNodeId)
    {
        if (surface is null)
        {
            return false;
        }

        var sourceIds = sourceRootNodeIds.ToHashSet(StringComparer.Ordinal);
        var nodesById = surface.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        var visitedIds = new HashSet<string>(StringComparer.Ordinal);
        var currentNodeId = destinationNodeId;

        while (!string.IsNullOrWhiteSpace(currentNodeId) && visitedIds.Add(currentNodeId))
        {
            if (sourceIds.Contains(currentNodeId))
            {
                return true;
            }

            if (!nodesById.TryGetValue(currentNodeId, out var currentNode))
            {
                return false;
            }

            currentNodeId = currentNode.ParentId;
        }

        return false;
    }

    private void SetClipboardFeedback(string message, string tone)
    {
        workflowFeedback = message;
        workflowFeedbackTone = tone;
    }

    private async Task CopyToClipboardAsync(string text, string successMessage)
    {
        try
        {
            await JSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", text);
            SetClipboardFeedback(successMessage, "mint");
        }
        catch (JSException)
        {
            SetClipboardFeedback("Clipboard access was blocked by the browser.", "warn");
        }

        await InvokeAsync(StateHasChanged);
    }
}
