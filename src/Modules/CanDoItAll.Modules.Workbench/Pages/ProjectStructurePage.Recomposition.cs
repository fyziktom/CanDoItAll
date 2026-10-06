namespace CanDoItAll.Modules.Workbench.Pages;

public partial class ProjectStructurePage {
    private ProjectStructureAuthoringOpening? recompositionOpening;
    private bool isSubtreeRecompositionInProgress
        => recompositionOpening is { IsBusy: true } opening && IsCurrentAction(opening.Context);

    private bool CanRecomposeSelectedBranch
        => !isSubtreeRecompositionInProgress &&
           !(recompositionOpening is { RequiresObservation: true } pending && IsCurrentAction(pending.Context)) &&
           selectedNodes.Count == 1 &&
           selectedNode is not null &&
           CountDescendants(selectedNode.Id) > 0;

    private async Task RecomposeSelectedBranchAsync() {
        if (isSubtreeRecompositionInProgress || recompositionOpening is { RequiresObservation: true } pending && IsCurrentAction(pending.Context)) {
            return;
        }
        if (selectedNodes.Count != 1 || selectedNode is null || CountDescendants(selectedNode.Id) == 0) {
            workflowFeedback = "Select one branch with descendants before recomposing.";
            workflowFeedbackTone = "warn";
            return;
        }
        var context = CaptureActionContext();
        var selectionRevision = insightsSelectionRevision;
        var opening = new ProjectStructureAuthoringOpening(context, selectedNode) { IsBusy = true };
        recompositionOpening = opening;
        var outcome = new ProjectStructureAuthoringOutcome(opening.Id, Guid.NewGuid(), context.Admission,
            ProjectStructureAuthoringOperation.RecomposeNodes, ProjectStructureAuthoringResultKind.Rejected, "The selected branch could not be recomposed.") {
            SourceNodeId = opening.Node.Id
        };
        try {
            var result = await ProjectWorkbenchService.RecomposeSubtreeAsync(context.Surface.ProjectId, opening.Node.Id,
                mutationOwner: CreateProjectStructureUiAgentContext(context.Surface.ProjectId) with { ExpectedProjectAdmission = context.Admission },
                expectedNodes: [opening.Node]);
            if (result is not null) {
                outcome = outcome with { Kind = ProjectStructureAuthoringResultKind.Committed, Recomposition = result,
                    Message = result.RepositionedNodeCount == 0 ? $"{opening.Node.Title} already fits the layout." :
                        $"Recomposed {result.RepositionedNodeCount} nodes under {opening.Node.Title}." };
            }
            RecordAuthoringOutcome(opening, outcome);
            if (result is not null && IsCurrentAuthoring(opening, recompositionOpening) && selectionRevision == insightsSelectionRevision) {
                await RefreshAuthoringSurfaceAsync(opening, outcome, opening.Node.Id,
                    () => ReferenceEquals(recompositionOpening, opening) && selectionRevision == insightsSelectionRevision);
            }
        } catch (Exception failure) {
            opening.RequiresObservation = !IsKnownGraphRejection(failure);
            RecordAuthoringOutcome(opening, outcome with { Failure = failure,
                Kind = opening.RequiresObservation ? ProjectStructureAuthoringResultKind.Unconfirmed : ProjectStructureAuthoringResultKind.Rejected,
                Message = opening.RequiresObservation ? "Recomposition is unconfirmed. Observe the original project before repeating it." : failure.Message });
        } finally {
            opening.IsBusy = false;
            await RenderAuthoringOutcomeAsync();
        }
    }
}
