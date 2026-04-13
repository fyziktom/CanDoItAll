using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.Processes;

public partial class ProcessWorkspace
{
    [Inject]
    private ProcessCanvasRecompositionService ProcessCanvasRecompositionService { get; set; } = default!;

    private bool isDefinitionCanvasRecompositionInProgress;

    private bool CanRecomposeDefinitionCanvas
        => !isDefinitionCanvasRecompositionInProgress &&
           IsDefinitionCanvasActive &&
           (editor.Steps.Count > 0 || editor.Roles.Count > 0);

    private Task ResolveDefinitionCanvasCollisionsAsync()
        => ApplyDefinitionCanvasRecompositionAsync(ProcessCanvasRecompositionMode.ResolveCollisions);

    private Task AddDefinitionCanvasSpaceAroundAsync()
        => ApplyDefinitionCanvasRecompositionAsync(ProcessCanvasRecompositionMode.AddSpaceAround);

    private Task RecomposeDefinitionCanvasAsync()
        => ApplyDefinitionCanvasRecompositionAsync(ProcessCanvasRecompositionMode.Recompose);

    private async Task ApplyDefinitionCanvasRecompositionAsync(ProcessCanvasRecompositionMode mode)
    {
        if (isDefinitionCanvasRecompositionInProgress)
        {
            return;
        }

        if (!IsDefinitionCanvasActive)
        {
            SetError("Open the Steps tab before recomposing the process canvas.");
            return;
        }

        if (editor.Steps.Count == 0 && editor.Roles.Count == 0)
        {
            SetError("Add process steps or roles before recomposing the canvas.");
            return;
        }

        isDefinitionCanvasRecompositionInProgress = true;
        await InvokeAsync(StateHasChanged);

        try
        {
            var result = ProcessCanvasRecompositionService.Apply(editor, mode);
            RefreshCanvasSurface();
            if (result.RepositionedNodeCount == 0)
            {
                SetMessage(mode switch
                {
                    ProcessCanvasRecompositionMode.ResolveCollisions => "Process canvas collisions are already resolved.",
                    ProcessCanvasRecompositionMode.AddSpaceAround => "Process canvas spacing is already expanded.",
                    _ => "Process canvas already matches the current recomposition layout."
                });
                return;
            }

            var successMessage = mode switch
            {
                ProcessCanvasRecompositionMode.ResolveCollisions
                    => $"Resolved collisions for {result.RepositionedNodeCount} canvas node(s).",
                ProcessCanvasRecompositionMode.AddSpaceAround
                    => $"Added space around {result.RepositionedNodeCount} canvas node(s).",
                _ => $"Recomposed {result.RepositionedNodeCount} process canvas node(s)."
            };
            await PersistDefinitionCanvasChangesAsync(successMessage, refreshSurface: false);
            if (workbenchRef is not null)
            {
                await workbenchRef.FitViewToSceneAsync();
            }
        }
        finally
        {
            isDefinitionCanvasRecompositionInProgress = false;
            await InvokeAsync(StateHasChanged);
        }
    }
}
