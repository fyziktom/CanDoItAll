namespace CanDoItAll.Modules.Workbench.Pages;

public partial class ProjectStructurePage
{
    private bool isSubtreeRecompositionInProgress;

    private bool CanRecomposeSelectedBranch
        => !isSubtreeRecompositionInProgress &&
           selectedNodes.Count == 1 &&
           selectedNode is not null &&
           CountDescendants(selectedNode.Id) > 0;

    private async Task RecomposeSelectedBranchAsync()
    {
        if (selectedNodes.Count != 1 || selectedNode is null)
        {
            workflowFeedback = "Select exactly one node before recomposing a branch.";
            workflowFeedbackTone = "warn";
            return;
        }

        var targetNode = selectedNode;
        var descendantCount = CountDescendants(targetNode.Id);
        if (descendantCount == 0)
        {
            workflowFeedback = "The selected node has no descendants to recompose.";
            workflowFeedbackTone = "warn";
            return;
        }

        isSubtreeRecompositionInProgress = true;
        await InvokeAsync(StateHasChanged);

        try
        {
            var result = await ProjectWorkbenchService.RecomposeSubtreeAsync(ProjectId, targetNode.Id);
            if (result is null)
            {
                workflowFeedback = "The selected branch could not be recomposed.";
                workflowFeedbackTone = "warn";
                return;
            }

            await ReloadSurfaceAsync(targetNode.Id);
            if (result.RepositionedNodeCount == 0)
            {
                workflowFeedback = $"{targetNode.Title} already fits the current layout.";
                workflowFeedbackTone = "neutral";
                return;
            }

            workflowFeedback = $"Recomposed {result.RepositionedNodeCount} nodes under {targetNode.Title}.";
            workflowFeedbackTone = "mint";
        }
        finally
        {
            isSubtreeRecompositionInProgress = false;
        }
    }
}
