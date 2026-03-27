using CanDoItAll.Components.CanvasLib;

namespace CanDoItAll.Modules.Workbench.Pages;

public partial class ProjectStructurePage
{
    private const string ToolboxWindowKey = "project-structure.toolbox";
    private string structureToolboxSearchText = string.Empty;
    private string? expandedToolboxGroupKey;

    private CanvasWorkbenchWindowState ToolboxWindowState => ResolveToolboxWindowState();

    private IReadOnlyList<ProjectStructureInspectorCreateGroup> ToolboxCreateGroups
        => BuildToolboxCreateGroups();

    private string ToolboxSourceLabel
        => selectedNode is null
            ? "Canvas root"
            : selectedNode.Title;

    private bool HasToolboxSearch
        => !string.IsNullOrWhiteSpace(structureToolboxSearchText);

    private Task HandleToolboxWindowStateChangedAsync(CanvasWorkbenchWindowState state)
        => PersistWindowStateAsync(ToolboxWindowKey, state);

    private async Task OpenToolboxAsync()
    {
        var state = ResolveToolboxWindowState();
        state.IsVisible = true;
        state.IsMinimized = false;
        await PersistWindowStateAsync(ToolboxWindowKey, state);
    }

    private CanvasWorkbenchWindowState ResolveToolboxWindowState()
    {
        var uiState = ResolveEditableUiState();
        if (uiState.WindowStates.TryGetValue(ToolboxWindowKey, out var state))
        {
            return CanvasWorkbenchWindowState.Normalize(state);
        }

        return CanvasWorkbenchWindowState.Normalize(new CanvasWorkbenchWindowState
        {
            IsVisible = true
        });
    }

    private IReadOnlyList<ProjectStructureInspectorCreateGroup> BuildToolboxCreateGroups()
    {
        var groups = BuildInspectorCreateGroups(selectedNode?.ObjectType)
            .Select(group =>
            {
                var actions = group.Actions
                    .Where(MatchesStructureToolboxSearch)
                    .ToList();

                return new ProjectStructureInspectorCreateGroup(group.Key, group.Label, group.Description, group.IsOpen, actions);
            })
            .Where(group => group.Actions.Count > 0)
            .ToList();

        if (groups.Count == 0)
        {
            expandedToolboxGroupKey = null;
            return groups;
        }

        if (HasToolboxSearch)
        {
            return groups
                .Select(group => group with { IsOpen = true })
                .ToList();
        }

        expandedToolboxGroupKey = ResolveExpandedToolboxGroupKey(groups);
        return groups
            .Select(group => group with { IsOpen = string.Equals(group.Key, expandedToolboxGroupKey, StringComparison.Ordinal) })
            .ToList();
    }

    private void ExpandToolboxGroup(string groupKey)
    {
        if (HasToolboxSearch)
        {
            return;
        }

        expandedToolboxGroupKey = groupKey;
    }

    private string ResolveExpandedToolboxGroupKey(IReadOnlyList<ProjectStructureInspectorCreateGroup> groups)
    {
        if (!string.IsNullOrWhiteSpace(expandedToolboxGroupKey) &&
            groups.Any(group => string.Equals(group.Key, expandedToolboxGroupKey, StringComparison.Ordinal)))
        {
            return expandedToolboxGroupKey;
        }

        return groups.FirstOrDefault(group => group.IsOpen)?.Key ?? groups[0].Key;
    }

    private bool MatchesStructureToolboxSearch(CanvasWorkbenchAction action)
    {
        if (!HasToolboxSearch)
        {
            return true;
        }

        var search = structureToolboxSearchText.Trim();
        return action.Label.Contains(search, StringComparison.OrdinalIgnoreCase) ||
               action.Description.Contains(search, StringComparison.OrdinalIgnoreCase) ||
               action.MenuLabel.Contains(search, StringComparison.OrdinalIgnoreCase);
    }
}
