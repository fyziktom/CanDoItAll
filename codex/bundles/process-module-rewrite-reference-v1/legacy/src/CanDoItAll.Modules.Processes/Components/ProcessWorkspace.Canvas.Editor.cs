namespace CanDoItAll.Modules.Processes;

public partial class ProcessWorkspace
{
    private async Task RemoveSelectedDefinitionStepAsync()
    {
        if (SelectedCanvasDefinitionStep is null)
        {
            return;
        }

        var removedTitle = SelectedCanvasDefinitionStep.Title;
        RemoveStep(SelectedCanvasDefinitionStep);
        selectedCanvasNodeId = editor.Steps.FirstOrDefault() is { } nextStep
            ? BuildDefinitionNodeId(nextStep)
            : null;
        await PersistDefinitionCanvasChangesAsync($"{removedTitle} was removed from the process definition.", refreshSurface: false);
    }

    private void SelectFallbackDefinitionNode()
    {
        if (editor.Steps.FirstOrDefault() is { } nextStep)
        {
            selectedCanvasNodeId = BuildDefinitionNodeId(nextStep);
            return;
        }

        if (editor.Roles.FirstOrDefault(role => role.Id.HasValue) is { } nextRole)
        {
            selectedCanvasNodeId = ProcessCanvasBranching.BuildDefinitionRoleNodeId(nextRole);
            return;
        }

        selectedCanvasNodeId = null;
    }

    private Task AddCanvasRoleAssignmentAsync()
    {
        if (canvasStepDraft is null)
        {
            return Task.CompletedTask;
        }

        AddRoleAssignment(canvasStepDraft);
        return Task.CompletedTask;
    }

    private Task AddCanvasBranchOutcomeAsync()
    {
        if (canvasStepDraft is null)
        {
            return Task.CompletedTask;
        }

        AddBranchOutcome(canvasStepDraft);
        return Task.CompletedTask;
    }

    private Task RemoveCanvasBranchOutcomeAsync(ProcessStepBranchOutcomeEditorModel branchOutcome)
    {
        if (canvasStepDraft is null || ProcessCanvasBranching.IsSystemOutcome(branchOutcome))
        {
            return Task.CompletedTask;
        }

        canvasStepDraft.BranchOutcomes.Remove(branchOutcome);
        return Task.CompletedTask;
    }

    private Task RemoveCanvasRoleAssignmentAsync(ProcessStepRoleRequirementEditorModel assignment)
    {
        canvasStepDraft?.RoleAssignments.Remove(assignment);
        return Task.CompletedTask;
    }

    private Task AddCanvasArtifactExpectationAsync()
    {
        if (canvasStepDraft is null)
        {
            return Task.CompletedTask;
        }

        AddArtifact(canvasStepDraft);
        return Task.CompletedTask;
    }

    private Task RemoveCanvasArtifactExpectationAsync(ProcessArtifactExpectationEditorModel artifact)
    {
        canvasStepDraft?.ArtifactExpectations.Remove(artifact);
        return Task.CompletedTask;
    }
}
