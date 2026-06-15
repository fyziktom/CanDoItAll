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

    private Task RecomposeDefinitionCanvasMainPathSpineAsync()
        => ApplyDefinitionCanvasRecompositionAsync(ProcessCanvasRecompositionMode.MainPathSpine);

    private Task RecomposeDefinitionCanvasBranchFanOutAsync()
        => ApplyDefinitionCanvasRecompositionAsync(ProcessCanvasRecompositionMode.BranchFanOut);

    private Task RecomposeDefinitionCanvasFeedbackLanesAsync()
        => ApplyDefinitionCanvasRecompositionAsync(ProcessCanvasRecompositionMode.FeedbackLanes);

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
                    ProcessCanvasRecompositionMode.MainPathSpine => "Process canvas already matches the main-path spine layout.",
                    ProcessCanvasRecompositionMode.BranchFanOut => "Process canvas already matches the branch fan-out layout.",
                    ProcessCanvasRecompositionMode.FeedbackLanes => "Process canvas already matches the feedback-lanes layout.",
                    _ => "Process canvas already matches the balanced recomposition layout."
                });
                return;
            }

            var successMessage = mode switch
            {
                ProcessCanvasRecompositionMode.ResolveCollisions
                    => $"Resolved collisions for {result.RepositionedNodeCount} canvas node(s).",
                ProcessCanvasRecompositionMode.AddSpaceAround
                    => $"Added space around {result.RepositionedNodeCount} canvas node(s).",
                ProcessCanvasRecompositionMode.MainPathSpine
                    => $"Recomposed {result.RepositionedNodeCount} process canvas node(s) with the main-path spine layout.",
                ProcessCanvasRecompositionMode.BranchFanOut
                    => $"Recomposed {result.RepositionedNodeCount} process canvas node(s) with the branch fan-out layout.",
                ProcessCanvasRecompositionMode.FeedbackLanes
                    => $"Recomposed {result.RepositionedNodeCount} process canvas node(s) with the feedback-lanes layout.",
                _ => $"Recomposed {result.RepositionedNodeCount} process canvas node(s) with the balanced layout."
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
