using CanDoItAll.Components.CanvasLib;
using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench.Pages;

public partial class ProjectStructurePage
{
    private const string ToolboxWindowKey = "project-structure.toolbox";
    private const string ObjectIndexWindowKey = "project-structure.objectIndex";
    private string structureToolboxSearchText = string.Empty;
    private string objectIndexSearchText = string.Empty;
    private string? expandedToolboxGroupKey;
    private ProjectStructureFileCollectionRequest? fileBrowserRequest;

    private CanvasWorkbenchWindowState ToolboxWindowState => ResolveToolboxWindowState();

    private CanvasWorkbenchWindowState ObjectIndexWindowState => ResolveObjectIndexWindowState();

    private string FileBrowserWindowKey => ProjectStructureFileBrowserWindowKey.Persisted.Value;

    private CanvasWorkbenchWindowState FileBrowserWindowState => ResolveFileBrowserWindowState();

    private ProjectStructureFileCollectionRequest CurrentFileBrowserRequest
        => fileBrowserRequest ?? new ProjectStructureProjectFileCollectionRequest(
            ProjectId,
            surface?.ProjectName ?? "Project files");

    private ProjectStructureNode? CurrentFileBrowserNode
        => CurrentFileBrowserRequest is ProjectStructureNodeFileCollectionRequest nodeRequest
            ? ResolveNode(nodeRequest.NodeId)
            : null;

    private bool CanOpenCurrentFileBrowserNodeInExplorer
        => CurrentFileBrowserNode is { } node && CanShowLocalOpen(node);

    private bool IsObjectIndexWindowLoaded
        => ObjectIndexWindowState is { IsVisible: true, IsMinimized: false };

    private IReadOnlyList<ProjectStructureNode> ObjectIndexWindowNodes
        => IsObjectIndexWindowLoaded ? outlineNodes : [];

    private string ObjectIndexWindowSummary
        => IsObjectIndexWindowLoaded
            ? $"{surface?.Nodes.Count ?? 0} nodes - {selectedNodeIds.Count} selected"
            : "Paused until expanded";

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

    private Task HandleObjectIndexWindowStateChangedAsync(CanvasWorkbenchWindowState state)
        => PersistWindowStateAsync(ObjectIndexWindowKey, state);

    private Task HandleFileBrowserWindowStateChangedAsync(CanvasWorkbenchWindowState state)
        => PersistWindowStateAsync(FileBrowserWindowKey, state);

    private Task OpenCurrentFileBrowserNodeInExplorerAsync()
        => CurrentFileBrowserNode is { } node
            ? OpenAttachmentLocallyAsync(node)
            : Task.CompletedTask;

    private Task HandleObjectIndexSearchTextChangedAsync(string value)
    {
        objectIndexSearchText = value;
        return Task.CompletedTask;
    }

    private async Task OpenToolboxAsync()
    {
        var state = ResolveToolboxWindowState();
        state.IsVisible = true;
        state.IsMinimized = false;
        await PersistWindowStateAsync(ToolboxWindowKey, state);
    }

    private Task OpenProjectFileBrowserAsync()
        => TryHandleFileBrowserActionAsync(ProjectStructureFileActions.BrowseFilesId, nodeId: null);

    private async Task<bool> TryHandleFileBrowserActionAsync(string actionId, string? nodeId)
    {
        if (!ProjectStructureFileActions.IsBrowseFiles(actionId) || surface is null)
        {
            return false;
        }

        ProjectStructureNode? node = string.IsNullOrWhiteSpace(nodeId) ? null : ResolveNode(nodeId);
        if (!string.IsNullOrWhiteSpace(nodeId) && node is null)
        {
            workflowFeedback = "The selected file collection no longer exists.";
            workflowFeedbackTone = "warn";
            await InvokeAsync(StateHasChanged);
            return true;
        }

        try
        {
            fileBrowserRequest = FileActionCoordinator.CreateRequest(
                ProjectId,
                surface.ProjectName,
                node);
            CanvasWorkbenchWindowState state = ResolveFileBrowserWindowState();
            state.IsVisible = true;
            state.IsMinimized = false;
            await PersistWindowStateAsync(FileBrowserWindowKey, state);
        }
        catch (FileBrowserProviderException exception)
        {
            workflowFeedback = exception.Error.Message;
            workflowFeedbackTone = "warn";
            await InvokeAsync(StateHasChanged);
        }

        return true;
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
            IsVisible = false
        });
    }

    private CanvasWorkbenchWindowState ResolveObjectIndexWindowState()
    {
        var uiState = ResolveEditableUiState();
        return uiState.WindowStates.TryGetValue(ObjectIndexWindowKey, out var state)
            ? CanvasWorkbenchWindowState.Normalize(state)
            : CanvasWorkbenchWindowState.Normalize(new CanvasWorkbenchWindowState
            {
                IsVisible = false
            });
    }

    private CanvasWorkbenchWindowState ResolveFileBrowserWindowState()
    {
        var uiState = ResolveEditableUiState();
        return uiState.WindowStates.TryGetValue(FileBrowserWindowKey, out var state)
            ? CanvasWorkbenchWindowState.Normalize(state)
            : CanvasWorkbenchWindowState.Normalize(new CanvasWorkbenchWindowState
            {
                IsVisible = false
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

    private void EnsureSelectionScopedToolboxWindowState(CanvasWorkbenchUiState uiState)
    {
        if (surface is null ||
            uiState.WindowStates.ContainsKey(ToolboxWindowKey) ||
            selectedNodeIds.Count != 1)
        {
            return;
        }

        var selectedNodeId = selectedNodeIds[0];
        var sourceNode = surface.Nodes.FirstOrDefault(node => string.Equals(node.Id, selectedNodeId, StringComparison.Ordinal));
        if (sourceNode is null || sourceNode.ObjectType == ProjectObjectType.ProjectRoot)
        {
            return;
        }

        uiState.WindowStates[ToolboxWindowKey] = CanvasWorkbenchWindowState.Normalize(new CanvasWorkbenchWindowState
        {
            IsVisible = true
        });
    }
}
