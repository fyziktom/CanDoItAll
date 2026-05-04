using CanDoItAll.Components.CanvasLib;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.Processes;

public partial class ProcessWorkspace
{
    private async Task ExecuteCanvasActionAsync(string actionId, string? nodeId, double x, double y)
    {
        if (!string.IsNullOrWhiteSpace(nodeId))
        {
            selectedCanvasNodeId = nodeId;
        }

        if (IsDefinitionCanvasAction(actionId))
        {
            await ExecuteDefinitionCanvasActionAsync(actionId, x, y);
            return;
        }

        if (IsRuntimeCanvasAction(actionId))
        {
            await ExecuteRuntimeCanvasActionAsync(actionId);
        }
    }

    private bool IsDefinitionCanvasAction(string actionId)
        => actionId.StartsWith("process-role.", StringComparison.Ordinal) ||
           actionId.StartsWith("process-step.", StringComparison.Ordinal) ||
           actionId.StartsWith("process-definition.", StringComparison.Ordinal);

    private bool IsRuntimeCanvasAction(string actionId)
        => actionId.StartsWith("process-runtime.", StringComparison.Ordinal);

    private async Task ExecuteDefinitionCanvasActionAsync(string actionId, double x, double y)
    {
        switch (actionId)
        {
            case ProcessCanvasActionIds.OpenDefinitionToolbox:
                await OpenCanvasToolboxAsync();
                break;
            case ProcessCanvasActionIds.EditDefinitionRole:
                OpenDefinitionRoleEditor();
                break;
            case ProcessCanvasActionIds.EditDefinitionStep:
                OpenDefinitionStepEditor();
                break;
            case ProcessCanvasActionIds.AddDependentStep:
                OpenCanvasStepEditor(actionId, SelectedCanvasDefinitionStep, x, y);
                break;
            case ProcessCanvasActionIds.AddSubprocessStep:
                OpenCanvasStepEditor(ProcessCanvasActionIds.CreateStepSubprocess, SelectedCanvasDefinitionStep, x, y);
                break;
            case ProcessCanvasActionIds.OpenSubprocessDefinition:
                await OpenSelectedSubprocessDefinitionAsync();
                break;
            case ProcessCanvasActionIds.AddBranchOutcome:
                await AddBranchOutcomeToSelectedStepAsync();
                break;
            case ProcessCanvasActionIds.AddRoleBinding:
                AddRoleBindingToSelectedStep();
                break;
            case ProcessCanvasActionIds.AddArtifactExpectation:
                AddArtifactExpectationToSelectedStep();
                break;
            case ProcessCanvasActionIds.RemoveDefinitionStep:
                await RemoveSelectedDefinitionStepAsync();
                break;
            default:
                if (actionId.StartsWith("process-role.", StringComparison.Ordinal))
                {
                    OpenCanvasRoleEditor(actionId);
                }
                else if (actionId.StartsWith("process-step.", StringComparison.Ordinal))
                {
                    OpenCanvasStepEditor(actionId, SelectedCanvasDefinitionStep, x, y);
                }
                break;
        }

        await InvokeAsync(StateHasChanged);
    }

    private async Task ExecuteRuntimeCanvasActionAsync(string actionId)
    {
        switch (actionId)
        {
            case ProcessCanvasActionIds.RuntimeStart:
                await ApplySelectedRuntimeStatusAsync(ProcessStepRunStatus.InProgress);
                break;
            case ProcessCanvasActionIds.RuntimeComplete:
                await ApplySelectedRuntimeStatusAsync(ProcessStepRunStatus.Completed);
                break;
            case ProcessCanvasActionIds.RuntimeBlock:
                await ApplySelectedRuntimeStatusAsync(ProcessStepRunStatus.Blocked);
                break;
            case ProcessCanvasActionIds.RuntimeApproval:
                await ApplySelectedRuntimeStatusAsync(ProcessStepRunStatus.WaitingApproval);
                break;
            case ProcessCanvasActionIds.RuntimeRefuse:
                await ApplySelectedRuntimeStatusAsync(ProcessStepRunStatus.Refused);
                break;
            case ProcessCanvasActionIds.RuntimeFail:
                await ApplySelectedRuntimeStatusAsync(ProcessStepRunStatus.Failed);
                break;
            case ProcessCanvasActionIds.RuntimeRecordArtifact:
                PrepareSelectedRuntimeArtifactCapture();
                break;
            case ProcessCanvasActionIds.RuntimeOpenSubprocessRun:
                await OpenSelectedSubprocessRunAsync();
                break;
        }
    }

    private async Task ApplySelectedRuntimeStatusAsync(ProcessStepRunStatus status)
    {
        if (SelectedCanvasRuntimeStep is null)
        {
            SetError("Select a runtime step on the canvas first.");
            return;
        }

        await ApplyStepStatusAsync(SelectedCanvasRuntimeStep.Id, status);
        selectedCanvasNodeId = BuildRunNodeId(SelectedCanvasRuntimeStep.Id);
    }

    private void PrepareSelectedRuntimeArtifactCapture()
    {
        if (SelectedCanvasRuntimeStep is null)
        {
            SetError("Select a runtime step before preparing artifact capture.");
            return;
        }

        artifactStepRunId = SelectedCanvasRuntimeStep.Id;
        artifactTitle = string.IsNullOrWhiteSpace(artifactTitle)
            ? $"{SelectedCanvasRuntimeStep.Title} evidence"
            : artifactTitle;
        SetMessage("Artifact capture is prepared for the selected runtime step in the evidence panel below.");
    }

    private async Task OpenCanvasActionDialogAsync()
    {
        if (IsRuntimeCanvasActive && SelectedCanvasRuntimeStep is not null)
        {
            canvasActionDialog = new ProcessCanvasNodeActionDialogState(
                SelectedCanvasRuntimeStep.Title,
                "Run step actions",
                true);
        }
        else if (SelectedCanvasDefinitionStep is not null)
        {
            canvasActionDialog = new ProcessCanvasNodeActionDialogState(
                SelectedCanvasDefinitionStep.Title,
                "Definition step actions",
                false);
        }

        await InvokeAsync(StateHasChanged);
    }

    private Task CloseCanvasActionDialogAsync()
    {
        canvasActionDialog = null;
        return Task.CompletedTask;
    }

    private async Task ExecuteCanvasDialogActionAsync(string actionId)
    {
        canvasActionDialog = null;
        await ExecuteCanvasActionAsync(actionId, selectedCanvasNodeId, 0, 0);
    }

    private void OpenCanvasRoleEditor(string actionId)
    {
        if (!ProcessTemplateCatalogService.TryCreateRoleDraft(actionId, editor.Roles.Count + 1, out var roleDraft))
        {
            return;
        }

        canvasRoleDraft = roleDraft;
        canvasStepDraft = null;
        canvasEditedRoleTarget = null;
        canvasEditedStepTarget = null;
        canvasInsertAfterStepId = null;
        canvasTemplateActionId = actionId;
        canvasEditorWindowState.IsVisible = true;
        canvasEditorWindowState.IsMinimized = false;
    }

    private void OpenCanvasStepEditor(
        string actionId,
        ProcessStepEditorModel? sourceStep,
        double x,
        double y,
        Guid? branchOutcomeId = null)
    {
        var dependencyId = sourceStep?.Id;
        var defaultY = ResolveCanvasStepEditorY(sourceStep, branchOutcomeId, y);
        var defaultX = ResolveCanvasStepEditorX(sourceStep, branchOutcomeId, x);

        if (!ProcessTemplateCatalogService.TryCreateStepDraft(
                actionId.StartsWith("process-step.", StringComparison.Ordinal) ? actionId : ProcessCanvasActionIds.CreateStepImplementation,
                editor.Steps.Count + 1,
                dependencyId,
                defaultX,
                defaultY,
                out var stepDraft))
        {
            return;
        }

        if (dependencyId.HasValue)
        {
            SetStepDependencies(
                stepDraft,
                [
                    ProcessStepDependencyCollection.CreateEditorDependency(
                        dependencyId.Value,
                        branchOutcomeId ??
                            (sourceStep is not null && ProcessCanvasBranching.ShouldRenderBranchRouter(sourceStep)
                                ? ProcessCanvasBranching.GetDefaultOutcomeId(sourceStep)
                                : null))
                ]);
        }
        canvasRoleDraft = null;
        canvasStepDraft = stepDraft;
        canvasEditedRoleTarget = null;
        canvasEditedStepTarget = null;
        canvasInsertAfterStepId = sourceStep?.Id;
        canvasTemplateActionId = actionId.StartsWith("process-step.", StringComparison.Ordinal)
            ? actionId
            : ProcessCanvasActionIds.CreateStepImplementation;
        canvasEditorWindowState.IsVisible = true;
        canvasEditorWindowState.IsMinimized = false;
    }

    private void OpenDefinitionRoleEditor()
    {
        if (SelectedCanvasDefinitionRole is null)
        {
            return;
        }

        canvasRoleDraft = CloneRole(SelectedCanvasDefinitionRole);
        canvasStepDraft = null;
        canvasEditedRoleTarget = SelectedCanvasDefinitionRole;
        canvasEditedStepTarget = null;
        canvasInsertAfterStepId = null;
        canvasTemplateActionId = string.Empty;
        canvasEditorWindowState.IsVisible = true;
        canvasEditorWindowState.IsMinimized = false;
    }

    private Task AddRoutedStepFromSelectedStepAsync(Guid? branchOutcomeId)
    {
        if (SelectedCanvasDefinitionStep is null)
        {
            SetError("Select a definition step before adding a routed step.");
            return Task.CompletedTask;
        }

        OpenCanvasStepEditor(
            ProcessCanvasActionIds.CreateStepImplementation,
            SelectedCanvasDefinitionStep,
            0,
            0,
            branchOutcomeId);
        return Task.CompletedTask;
    }

    private void OpenDefinitionStepEditor()
    {
        if (SelectedCanvasDefinitionStep is null)
        {
            return;
        }

        canvasRoleDraft = null;
        canvasStepDraft = CloneStep(SelectedCanvasDefinitionStep);
        canvasEditedRoleTarget = null;
        canvasEditedStepTarget = SelectedCanvasDefinitionStep;
        canvasInsertAfterStepId = null;
        canvasTemplateActionId = string.Empty;
        canvasEditorWindowState.IsVisible = true;
        canvasEditorWindowState.IsMinimized = false;
    }

    private Task HandleCanvasTemplateChangedAsync(ChangeEventArgs args)
    {
        var actionId = args.Value?.ToString()?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(actionId) || !IsCanvasEditorCreateMode)
        {
            return Task.CompletedTask;
        }

        canvasTemplateActionId = actionId;
        if (canvasRoleDraft is not null)
        {
            OpenCanvasRoleEditor(actionId);
        }
        else if (canvasStepDraft is not null)
        {
            OpenCanvasStepEditor(actionId, ResolveDefinitionStep(canvasInsertAfterStepId), canvasStepDraft.CanvasX, canvasStepDraft.CanvasY);
        }

        return Task.CompletedTask;
    }

    private async Task SaveCanvasEditorAsync()
    {
        if (canvasRoleDraft is not null)
        {
            if (canvasEditedRoleTarget is null)
            {
                editor.Roles.Add(CloneRole(canvasRoleDraft));
                SetMessage("Role requirement added from the canvas.");
            }
            else
            {
                CopyRole(canvasRoleDraft, canvasEditedRoleTarget);
                SetMessage("Role requirement updated from the canvas.");
            }
        }
        else if (canvasStepDraft is not null)
        {
            if (canvasEditedStepTarget is null)
            {
                var inserted = CloneStep(canvasStepDraft);
                if (canvasInsertAfterStepId.HasValue)
                {
                    var index = editor.Steps.FindIndex(step => step.Id == canvasInsertAfterStepId.Value);
                    if (index >= 0)
                    {
                        editor.Steps.Insert(index + 1, inserted);
                    }
                    else
                    {
                        editor.Steps.Add(inserted);
                    }
                }
                else
                {
                    editor.Steps.Add(inserted);
                }

                selectedCanvasNodeId = BuildDefinitionNodeId(inserted);
                SetMessage("Process step added from the canvas.");
            }
            else
            {
                CopyStep(canvasStepDraft, canvasEditedStepTarget);
                selectedCanvasNodeId = BuildDefinitionNodeId(canvasEditedStepTarget);
                SetMessage("Process step updated from the canvas.");
            }
        }

        CloseCanvasEditor();
        RefreshCanvasSurface();
        await PersistDefinitionCanvasChangesAsync(refreshSurface: false);
    }

    private Task CloseCanvasEditorAsync()
    {
        CloseCanvasEditor();
        return Task.CompletedTask;
    }

    private void CloseCanvasEditor()
    {
        canvasRoleDraft = null;
        canvasStepDraft = null;
        canvasEditedRoleTarget = null;
        canvasEditedStepTarget = null;
        canvasInsertAfterStepId = null;
        canvasTemplateActionId = string.Empty;
        canvasEditorWindowState.IsVisible = false;
        canvasEditorWindowState.IsMinimized = false;
    }

    private void AddRoleBindingToSelectedStep()
    {
        if (SelectedCanvasDefinitionStep is null)
        {
            SetError("Select a definition step before adding a role binding.");
            return;
        }

        AddRoleAssignment(SelectedCanvasDefinitionStep);
        OpenDefinitionStepEditor();
    }

    private void AddArtifactExpectationToSelectedStep()
    {
        if (SelectedCanvasDefinitionStep is null)
        {
            SetError("Select a definition step before adding an artifact expectation.");
            return;
        }

        AddArtifact(SelectedCanvasDefinitionStep);
        OpenDefinitionStepEditor();
    }

    private async Task AddBranchOutcomeToSelectedStepAsync()
    {
        if (SelectedCanvasDefinitionStep is null)
        {
            SetError("Select a definition step before adding a branch outcome.");
            return;
        }

        AddBranchOutcome(SelectedCanvasDefinitionStep);
        selectedCanvasNodeId = ProcessCanvasBranching.BuildDefinitionBranchNodeId(SelectedCanvasDefinitionStep);
        RefreshCanvasSurface();
        await PersistDefinitionCanvasChangesAsync("Branch route added to the canvas.", refreshSurface: false);
    }
}
