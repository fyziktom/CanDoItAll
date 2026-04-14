using CanDoItAll.Components.CanvasLib;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.Processes;

public partial class ProcessWorkspace
{
    private const string NoCanvasSelection = "__processes:none__";
    private const string CanvasToolboxWindowId = "processes.canvas.toolbox";
    private const string CanvasSelectionWindowId = "processes.canvas.selection";
    private const string CanvasEditorWindowId = "processes.canvas.editor";
    private const string CanvasToolSelectActionId = "tool-mode:select";
    private const string CanvasDeleteActionId = "delete";
    private const string CanvasDeleteLinkActionId = "delete-link";
    private const string CanvasConnectionCreateActionId = "connection:create";
    private const int DefinitionCanvasPersistDelayMs = 300;

    private CanvasWorkbench? workbenchRef;
    private string? selectedCanvasNodeId;
    private string canvasToolboxSearchText = string.Empty;
    private CanvasWorkbenchWindowState canvasToolboxWindowState = new() { IsVisible = false };
    private CanvasWorkbenchWindowState canvasSelectionWindowState = new();
    private CanvasWorkbenchWindowState canvasEditorWindowState = new() { IsVisible = false };
    private ProcessRoleEditorModel? canvasRoleDraft;
    private ProcessStepEditorModel? canvasStepDraft;
    private ProcessRoleEditorModel? canvasEditedRoleTarget;
    private ProcessStepEditorModel? canvasEditedStepTarget;
    private Guid? canvasInsertAfterStepId;
    private string canvasTemplateActionId = string.Empty;
    private ProcessCanvasNodeActionDialogState? canvasActionDialog;
    private readonly SemaphoreSlim definitionCanvasPersistGate = new(1, 1);
    private CancellationTokenSource? pendingDefinitionCanvasPersistCts;
    private Task pendingDefinitionCanvasPersistTask = Task.CompletedTask;
    private Task definitionCanvasPersistDrainTask = Task.CompletedTask;

    private bool IsDefinitionCanvasActive => string.Equals(detailTab, "steps", StringComparison.Ordinal);

    private bool IsRuntimeCanvasActive => string.Equals(detailTab, "runs", StringComparison.Ordinal) && SelectedRun is not null;

    private bool IsCanvasEditorOpen => canvasEditorWindowState.IsVisible && (canvasRoleDraft is not null || canvasStepDraft is not null);

    private bool IsCanvasEditorCreateMode => canvasEditedRoleTarget is null && canvasEditedStepTarget is null;

    private ProcessStepEditorModel? SelectedCanvasDefinitionStep => ResolveDefinitionStep(selectedCanvasNodeId);

    private ProcessRoleEditorModel? SelectedCanvasDefinitionRole => ResolveDefinitionRole(selectedCanvasNodeId);

    private ProcessStepRunViewModel? SelectedCanvasRuntimeStep => ResolveRuntimeStep(selectedCanvasNodeId);

    private IReadOnlyList<ProcessCanvasToolboxGroup> DefinitionToolboxGroups
        => ProcessTemplateCatalogService.GetDefinitionToolboxGroups()
            .Select(group =>
            {
                if (string.IsNullOrWhiteSpace(canvasToolboxSearchText))
                {
                    return group;
                }

                var filteredActions = group.Actions
                    .Where(action =>
                        action.Label.Contains(canvasToolboxSearchText, StringComparison.OrdinalIgnoreCase) ||
                        action.Summary.Contains(canvasToolboxSearchText, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                return new ProcessCanvasToolboxGroup(group.Key, group.Title, group.Summary, filteredActions);
            })
            .Where(group => group.Actions.Count > 0)
            .ToList();

    private IReadOnlyList<ProcessStepEditorModel> CanvasEditorDependencyOptions
        => canvasEditedStepTarget is null
            ? editor.Steps
            : editor.Steps.Where(step => step != canvasEditedStepTarget).ToList();

    private string CanvasSelectionWindowTitle
        => IsRuntimeCanvasActive
            ? SelectedCanvasRuntimeStep?.Title ?? "Runtime selection"
            : SelectedCanvasDefinitionRole?.DisplayName ??
              SelectedCanvasDefinitionStep?.Title ??
              "Definition selection";

    private string CanvasSelectionWindowSummary
        => IsRuntimeCanvasActive
            ? "Track runtime status, executor context, and proof-oriented next actions."
            : SelectedCanvasDefinitionRole is not null
                ? "Inspect the selected role contract and the routing authority it contributes to the canvas."
                : "Inspect the selected step and keep role bindings plus artifact expectations close to the canvas.";

    private string CanvasEditorWindowTitle
        => canvasRoleDraft is not null
            ? (IsCanvasEditorCreateMode ? "Role requirement" : $"Edit {canvasRoleDraft.DisplayName}")
            : canvasStepDraft is not null
                ? (IsCanvasEditorCreateMode ? "Process step" : $"Edit {canvasStepDraft.Title}")
                : "Canvas editor";

    private string CanvasEditorWindowSummary
        => canvasRoleDraft is not null
            ? "Start from a reusable template or refine the role contract before the process is saved."
            : "Shape the step contract, dependencies, role bindings, and evidence expectations directly from the canvas.";

    private IReadOnlyList<ProcessCanvasRoleTemplate> CanvasRoleTemplates => ProcessTemplateCatalogService.GetRoleTemplates();

    private IReadOnlyList<ProcessCanvasStepTemplate> CanvasStepTemplates => ProcessTemplateCatalogService.GetStepTemplates();

    private async Task HandleCanvasSelectionChangedAsync(CanvasWorkbenchSelectionChangedEventArgs args)
    {
        selectedCanvasNodeId = args.SelectedNodeIds.Count == 0
            ? NoCanvasSelection
            : args.PrimaryNodeId ?? args.SelectedNodeIds.FirstOrDefault();
        var uiState = CloneCanvasUiState(ResolveStoredCanvasUiState());
        uiState.SelectedNodeIds = args.SelectedNodeIds.Count > 0
            ? [.. args.SelectedNodeIds]
            : [];
        StoreCanvasUiState(uiState);
        if (canvasSurface is not null)
        {
            canvasSurface.UiState = uiState;
        }

        if (!canvasSelectionWindowState.IsVisible)
        {
            canvasSelectionWindowState.IsVisible = true;
        }

        await InvokeAsync(StateHasChanged);
    }

    private async Task HandleCanvasStateChangedAsync(string stateJson)
    {
        if (canvasSurface is null)
        {
            return;
        }

        var uiState = CanvasWorkbenchUiState.Parse(stateJson);
        if (string.IsNullOrWhiteSpace(uiState.ActiveInspectorTab))
        {
            uiState.ActiveInspectorTab = IsRuntimeCanvasActive ? "runtime" : "definition";
        }

        selectedCanvasNodeId = uiState.SelectedNodeIds.Count == 0
            ? NoCanvasSelection
            : uiState.SelectedNodeIds.FirstOrDefault();
        StoreCanvasUiState(uiState);
        canvasSurface.UiState = uiState;
        await InvokeAsync(StateHasChanged);
    }

    private Task HandleCanvasToolboxWindowStateChangedAsync(CanvasWorkbenchWindowState state)
    {
        canvasToolboxWindowState = CanvasWorkbenchWindowState.Normalize(state);
        return Task.CompletedTask;
    }

    private Task HandleCanvasSelectionWindowStateChangedAsync(CanvasWorkbenchWindowState state)
    {
        canvasSelectionWindowState = CanvasWorkbenchWindowState.Normalize(state);
        return Task.CompletedTask;
    }

    private Task HandleCanvasEditorWindowStateChangedAsync(CanvasWorkbenchWindowState state)
    {
        canvasEditorWindowState = CanvasWorkbenchWindowState.Normalize(state);
        return Task.CompletedTask;
    }

    private Task ToggleCanvasToolboxWindowAsync()
    {
        canvasToolboxWindowState.IsVisible = !canvasToolboxWindowState.IsVisible;
        if (canvasToolboxWindowState.IsVisible)
        {
            canvasToolboxWindowState.IsMinimized = false;
        }

        return Task.CompletedTask;
    }

    private Task OpenCanvasToolboxAsync()
    {
        canvasToolboxWindowState.IsVisible = true;
        canvasToolboxWindowState.IsMinimized = false;
        return Task.CompletedTask;
    }

    private Task ToggleCanvasSelectionWindowAsync()
    {
        canvasSelectionWindowState.IsVisible = !canvasSelectionWindowState.IsVisible;
        if (canvasSelectionWindowState.IsVisible)
        {
            canvasSelectionWindowState.IsMinimized = false;
        }

        return Task.CompletedTask;
    }

    private Task ClearCanvasSelectionAsync()
    {
        selectedCanvasNodeId = NoCanvasSelection;
        RefreshCanvasSurface();
        return Task.CompletedTask;
    }

    private async Task HandleCanvasContextActionAsync(CanvasWorkbenchContextActionRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.NodeId))
        {
            selectedCanvasNodeId = request.NodeId;
        }
        else if (!string.IsNullOrWhiteSpace(request.LinkTargetId))
        {
            selectedCanvasNodeId = request.LinkTargetId;
        }

        if (IsDefinitionCanvasActive && await HandleDefinitionCanvasWorkbenchActionAsync(request))
        {
            await InvokeAsync(StateHasChanged);
            return;
        }

        await ExecuteCanvasActionAsync(request.ActionId, request.NodeId, request.X, request.Y);
    }

    private async Task HandleCanvasCreateActionAsync(CanvasWorkbenchCreateActionRequest request)
    {
        await ExecuteCanvasActionAsync(request.ActionId, request.SourceNodeId, request.X, request.Y);
    }

    private async Task<bool> HandleDefinitionCanvasWorkbenchActionAsync(CanvasWorkbenchContextActionRequest request)
    {
        switch (request.ActionId)
        {
            case CanvasToolSelectActionId:
                SetDefinitionCanvasTool(DefinitionCanvasSelectTool);
                return true;
            case CanvasDeleteActionId:
                await DeleteDefinitionCanvasNodeAsync(request.NodeId);
                return true;
            case CanvasDeleteLinkActionId:
                await DeleteDefinitionCanvasLinkAsync(request);
                return true;
            case CanvasConnectionCreateActionId:
                await CreateDefinitionCanvasConnectionAsync(request);
                return true;
            default:
                return false;
        }
    }

    private async Task HandleCanvasNodeOpenedAsync(string nodeId)
    {
        selectedCanvasNodeId = nodeId;
        if (IsDefinitionCanvasActive && SelectedCanvasDefinitionRole is not null)
        {
            OpenDefinitionRoleEditor();
            await InvokeAsync(StateHasChanged);
            return;
        }

        await OpenCanvasActionDialogAsync();
    }

    private async Task HandleCanvasNodeEditedAsync(CanvasWorkbenchNodeEditRequest request)
    {
        if (!IsDefinitionCanvasActive || ResolveDefinitionStep(request.NodeId) is not { } step)
        {
            return;
        }

        step.Title = request.Title.Trim();
        step.Notes = request.Notes.Trim();
        RefreshCanvasSurface();
        await PersistDefinitionCanvasChangesAsync();
    }

    private async Task HandleCanvasNodesMovedAsync(CanvasWorkbenchNodesMovedEventArgs args)
    {
        if (!IsDefinitionCanvasActive)
        {
            return;
        }

        var uiState = CloneCanvasUiState(ResolveStoredCanvasUiState());
        foreach (var position in args.Positions)
        {
            if (position.NodeId.StartsWith(ProcessCanvasCatalog.NodePrefixes.DefinitionStep, StringComparison.Ordinal))
            {
                var step = ResolveDefinitionStep(position.NodeId);
                if (step is null)
                {
                    continue;
                }

                step.CanvasX = position.X;
                step.CanvasY = position.Y;
                uiState.ManualPositions.Remove(position.NodeId);
                continue;
            }

            if (position.NodeId.StartsWith(ProcessCanvasCatalog.NodePrefixes.DefinitionBranchRouter, StringComparison.Ordinal))
            {
                var step = ResolveDefinitionStep(position.NodeId);
                if (step is null)
                {
                    continue;
                }

                step.BranchCanvasX = position.X;
                step.BranchCanvasY = position.Y;
                uiState.ManualPositions.Remove(position.NodeId);
                continue;
            }

            if (position.NodeId.StartsWith(ProcessCanvasCatalog.NodePrefixes.DefinitionRole, StringComparison.Ordinal))
            {
                var role = ResolveDefinitionRole(position.NodeId);
                if (role is null)
                {
                    continue;
                }

                role.CanvasX = position.X;
                role.CanvasY = position.Y;
                uiState.ManualPositions.Remove(position.NodeId);
            }
        }

        StoreCanvasUiState(uiState);
        RefreshCanvasSurface();
        ScheduleDefinitionCanvasPersistence();
        await InvokeAsync(StateHasChanged);
    }

}
