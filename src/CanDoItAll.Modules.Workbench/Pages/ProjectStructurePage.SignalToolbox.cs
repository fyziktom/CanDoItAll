using CanDoItAll.Components.CanvasLib;

namespace CanDoItAll.Modules.Workbench.Pages;

public partial class ProjectStructurePage
{
    private const string SignalsWindowKey = "project-structure.signals";

    private CanvasWorkbenchWindowState SignalsWindowState => ResolveSignalsWindowState();

    private IReadOnlyList<ProjectStructureSignalSection> SignalsWindowSections
        => BuildSignalsWindowSections();

    private bool CanApplySignals => selectedNodeIds.Count > 0;

    private string SignalsSelectionLabel
        => selectedNodeIds.Count switch
        {
            0 => "No node selected",
            1 => selectedNode?.Title ?? "1 selected node",
            _ => $"{selectedNodeIds.Count} selected nodes"
        };

    private string SignalsSelectionHint
        => selectedNodeIds.Count switch
        {
            0 => "Pick a node first",
            1 => "Applies immediately",
            _ => "Applies to the full selection"
        };

    private Task HandleSignalsWindowStateChangedAsync(CanvasWorkbenchWindowState state)
        => PersistWindowStateAsync(SignalsWindowKey, state);

    private async Task ToggleSignalsWindowAsync()
        => await ToggleWindowAsync(SignalsWindowKey);

    private CanvasWorkbenchWindowState ResolveSignalsWindowState()
    {
        var uiState = ResolveEditableUiState();
        if (!uiState.WindowStates.TryGetValue(SignalsWindowKey, out var state))
        {
            return CanvasWorkbenchWindowState.Normalize(new CanvasWorkbenchWindowState
            {
                IsVisible = false,
                Left = SignalsWindowDefaultLeft
            });
        }

        if (state.HasCustomGeometry)
        {
            return CanvasWorkbenchWindowState.Normalize(state);
        }

        var offsetState = state.Clone();
        offsetState.Left = SignalsWindowDefaultLeft;
        return CanvasWorkbenchWindowState.Normalize(offsetState);
    }

    private async Task HandleSignalsToolboxActionAsync(string actionId)
    {
        var targetNodeIds = selectedNodeIds
            .Where(nodeId => !string.IsNullOrWhiteSpace(nodeId))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (targetNodeIds.Count == 0)
        {
            return;
        }

        if (actionCatalog.TryResolveMarkerAction(actionId, out var markerIcon, out var markerTone, out var markerLabel))
        {
            if (string.IsNullOrWhiteSpace(markerIcon))
            {
                await ApplyMarkerAsync(targetNodeIds, markerIcon, markerTone, markerLabel);
                return;
            }

            var targetNodes = selectedNodes
                .Where(node => targetNodeIds.Contains(node.Id, StringComparer.Ordinal))
                .ToList();
            var allTargetsAlreadyHaveMarker = targetNodes.Count > 0 &&
                targetNodes.All(node => node.Markers.Any(marker => string.Equals(marker.Icon, markerIcon, StringComparison.OrdinalIgnoreCase)));
            var updatedNodes = allTargetsAlreadyHaveMarker
                ? await ProjectWorkbenchService.RemoveObjectMarkerDetailedAsync(ProjectId, targetNodeIds, markerIcon, markerTone, markerLabel)
                : await ProjectWorkbenchService.AddObjectMarkerDetailedAsync(ProjectId, targetNodeIds, markerIcon, markerTone, markerLabel);
            await ApplySurfaceNodeUpdatesAsync(updatedNodes);
            return;
        }

        if (actionCatalog.TryResolveProgressAction(actionId, out var progressMode, out var progressPercent))
        {
            await ApplyProgressAsync(targetNodeIds, progressMode, progressPercent);
            return;
        }

        if (actionCatalog.TryResolvePriorityAction(actionId, out var priority))
        {
            await ApplyPriorityAsync(targetNodeIds, priority);
        }
    }

    private IReadOnlyList<ProjectStructureSignalSection> BuildSignalsWindowSections()
    {
        var groupActions = actionCatalog.BuildGroupContextActions();
        var progressActions = groupActions.FirstOrDefault(action => string.Equals(action.ActionId, "progress", StringComparison.Ordinal))?.Children ?? [];
        var markerActions = groupActions.FirstOrDefault(action => string.Equals(action.ActionId, "marker", StringComparison.Ordinal))?.Children ?? [];
        var priorityActions = groupActions.FirstOrDefault(action => string.Equals(action.ActionId, "priority", StringComparison.Ordinal))?.Children ?? [];

        return
        [
            new ProjectStructureSignalSection(
                "markers",
                "Markers",
                "Stack more than one visual marker on the current node or selection.",
                markerActions
                    .Select(BuildMarkerSignalTile)
                    .ToList()),
            new ProjectStructureSignalSection(
                "progress",
                "Progress",
                "Keep the visible progress ring aligned with the current work state.",
                progressActions
                    .Select(BuildProgressSignalTile)
                    .ToList()),
            new ProjectStructureSignalSection(
                "priority",
                "Priority",
                "Set or clear the numbered priority badge.",
                priorityActions
                    .Select(BuildPrioritySignalTile)
                    .ToList())
        ];
    }

    private ProjectStructureSignalActionTile BuildMarkerSignalTile(CanvasWorkbenchAction action)
    {
        var markerIcon = action.ActionId.StartsWith("marker:", StringComparison.OrdinalIgnoreCase)
            ? action.ActionId["marker:".Length..]
            : string.Empty;
        var isActive = !string.IsNullOrWhiteSpace(markerIcon) &&
            selectedNodes.Count > 0 &&
            selectedNodes.All(node => node.Markers.Any(marker => string.Equals(marker.Icon, markerIcon, StringComparison.OrdinalIgnoreCase)));
        return new ProjectStructureSignalActionTile(
            action.ActionId,
            action.MenuLabel,
            ResolveMarkerGlyph(markerIcon),
            action.Tone,
            isActive,
            action.Description);
    }

    private ProjectStructureSignalActionTile BuildProgressSignalTile(CanvasWorkbenchAction action)
    {
        actionCatalog.TryResolveProgressAction(action.ActionId, out var progressMode, out var progressPercent);
        var isActive = selectedNodes.Count > 0 &&
            selectedNodes.All(node => MatchesProgressPreset(node, progressMode, progressPercent));
        return new ProjectStructureSignalActionTile(
            action.ActionId,
            action.MenuLabel,
            ResolveProgressGlyph(action.ActionId, action.MenuLabel),
            action.Tone,
            isActive,
            action.Description);
    }

    private ProjectStructureSignalActionTile BuildPrioritySignalTile(CanvasWorkbenchAction action)
    {
        actionCatalog.TryResolvePriorityAction(action.ActionId, out var priority);
        var isActive = selectedNodes.Count > 0 && selectedNodes.All(node => node.Priority == priority);
        return new ProjectStructureSignalActionTile(
            action.ActionId,
            action.MenuLabel,
            action.MenuLabel,
            action.Tone,
            isActive,
            action.Description);
    }

    private static bool MatchesProgressPreset(ProjectStructureNode node, string progressMode, int progressPercent)
        => progressMode switch
        {
            "na" => string.Equals(node.ProgressMode, "na", StringComparison.OrdinalIgnoreCase),
            "started" => string.Equals(node.ProgressMode, "started", StringComparison.OrdinalIgnoreCase),
            "complete" => string.Equals(node.ProgressMode, "complete", StringComparison.OrdinalIgnoreCase) || node.ProgressPercent >= 100,
            "progress" => string.Equals(node.ProgressMode, "progress", StringComparison.OrdinalIgnoreCase) && node.ProgressPercent == progressPercent,
            _ => false
        };

    private static string ResolveProgressGlyph(string actionId, string fallbackLabel)
    {
        var token = actionId.StartsWith("progress:", StringComparison.OrdinalIgnoreCase)
            ? actionId["progress:".Length..]
            : string.Empty;
        return token switch
        {
            "started" => "▶",
            "na" => "N/A",
            _ => fallbackLabel
        };
    }

    private static string ResolveMarkerGlyph(string markerIcon)
        => markerIcon.ToLowerInvariant() switch
        {
            "question" => "?",
            "alert" => "!",
            "thumbs-up" => "👍",
            "thumbs-down" => "👎",
            "pause" => "⏸",
            "stop" => "■",
            "money" => "$",
            "car" => "🚗",
            "idea" => "✦",
            "risk" => "⚠",
            "none" => "×",
            _ => "•"
        };
}
