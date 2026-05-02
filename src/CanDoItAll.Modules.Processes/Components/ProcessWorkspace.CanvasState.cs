using CanDoItAll.AgentFramework.Components;
using CanDoItAll.Components.CanvasLib;

namespace CanDoItAll.Modules.Processes;

public partial class ProcessWorkspace
{
    private void RefreshCanvasSurface()
    {
        NormalizeEditorForAuthoring();
        canvasSurface = detailTab == "runs" && SelectedRun is not null
            ? CanvasSurfaceFactory.BuildRunSurface(SelectedRun, stepRuns, selectedCanvasNodeId)
            : CanvasSurfaceFactory.BuildDefinitionSurface(editor, selectedCanvasNodeId, definitionCanvasTool);

        var uiState = BuildCanvasUiState(canvasSurface, ResolveStoredCanvasUiState());
        canvasSurface.UiState = uiState;
        StoreCanvasUiState(uiState);

        if (string.Equals(selectedCanvasNodeId, NoCanvasSelection, StringComparison.Ordinal))
        {
            return;
        }

        var synchronizedSelection = uiState.SelectedNodeIds.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(synchronizedSelection))
        {
            selectedCanvasNodeId = synchronizedSelection;
            return;
        }

        if (selectedCanvasNodeId is not null)
        {
            selectedCanvasNodeId = null;
        }
    }

    private void NormalizeEditorForAuthoring()
    {
        ProcessCanvasBranching.NormalizeDefinitionEditor(editor);
    }

    private static void NormalizeStepDraftForAuthoring(ProcessStepEditorModel step)
    {
        ProcessCanvasBranching.NormalizeStepDraft(step);
    }

    private CanvasWorkbenchUiState ResolveStoredCanvasUiState()
        => IsRuntimeCanvasActive
            ? runtimeCanvasUiState
            : definitionCanvasUiState;

    private void StoreCanvasUiState(CanvasWorkbenchUiState uiState)
    {
        var storedState = CloneCanvasUiState(uiState);
        if (IsRuntimeCanvasActive)
        {
            runtimeCanvasUiState = storedState;
        }
        else
        {
            definitionCanvasUiState = storedState;
        }
    }

    private async Task HandleAgentWorkspaceRefreshRequestedAsync(ContextualAgentWorkspaceRefreshRequest request)
    {
        if (request.WorkspaceKind != ContextualAgentWorkspaceKind.Processes ||
            request.ProcessDefinitionId != selectedProcessId)
        {
            return;
        }

        await CaptureCurrentProcessCanvasStateAsync();
        await LoadWorkspaceAsync();
    }

    private async Task CaptureCurrentProcessCanvasStateAsync()
    {
        if (workbenchRef is null)
        {
            return;
        }

        try
        {
            StoreCanvasUiState(CanvasWorkbenchUiState.Parse(await workbenchRef.GetStateJsonAsync()));
        }
        catch
        {
            // If the browser surface is not available, the stored UI state is still the best reload source.
        }
    }

    private CanvasWorkbenchUiState BuildCanvasUiState(CanvasWorkbenchSurface surface, CanvasWorkbenchUiState storedUiState)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(storedUiState);

        var uiState = CloneCanvasUiState(storedUiState);
        var availableNodeIds = surface.Nodes
            .Select(node => node.Id)
            .ToHashSet(StringComparer.Ordinal);

        if (string.Equals(selectedCanvasNodeId, NoCanvasSelection, StringComparison.Ordinal))
        {
            uiState.SelectedNodeIds = [];
        }
        else if (!string.IsNullOrWhiteSpace(selectedCanvasNodeId) && availableNodeIds.Contains(selectedCanvasNodeId))
        {
            uiState.SelectedNodeIds = [selectedCanvasNodeId];
        }
        else
        {
            uiState.SelectedNodeIds = uiState.SelectedNodeIds
                .Where(availableNodeIds.Contains)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (uiState.SelectedNodeIds.Count == 0)
            {
                uiState.SelectedNodeIds = surface.UiState.SelectedNodeIds
                    .Where(availableNodeIds.Contains)
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
            }
        }

        if (string.IsNullOrWhiteSpace(uiState.ActiveInspectorTab))
        {
            uiState.ActiveInspectorTab = surface.UiState.ActiveInspectorTab;
        }

        return uiState;
    }

    private void ResetDefinitionCanvasState()
    {
        definitionCanvasTool = DefinitionCanvasSelectTool;
        definitionCanvasUiState = CreateDefaultDefinitionCanvasUiState();
    }

    private void ResetRuntimeCanvasState()
    {
        runtimeCanvasUiState = CreateDefaultRuntimeCanvasUiState();
    }

    private static CanvasWorkbenchUiState CreateDefaultDefinitionCanvasUiState()
        => new()
        {
            ActiveInspectorTab = "definition"
        };

    private Task SelectDefinitionCanvasToolAsync()
    {
        SetDefinitionCanvasTool(DefinitionCanvasSelectTool);
        return Task.CompletedTask;
    }

    private Task DeleteDefinitionCanvasToolAsync()
    {
        SetDefinitionCanvasTool(DefinitionCanvasDeleteTool);
        return Task.CompletedTask;
    }

    private void SetDefinitionCanvasTool(string tool)
    {
        definitionCanvasTool = string.Equals(tool, DefinitionCanvasDeleteTool, StringComparison.Ordinal)
            ? DefinitionCanvasDeleteTool
            : DefinitionCanvasSelectTool;
        if (IsDefinitionCanvasActive)
        {
            RefreshCanvasSurface();
        }
    }

    private static CanvasWorkbenchUiState CreateDefaultRuntimeCanvasUiState()
        => new()
        {
            ActiveInspectorTab = "runtime"
        };

    private static CanvasWorkbenchUiState CloneCanvasUiState(CanvasWorkbenchUiState uiState)
    {
        return CanvasWorkbenchUiState.Parse(uiState.ToJson());
    }
}
