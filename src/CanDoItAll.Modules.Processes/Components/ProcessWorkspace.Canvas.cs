using CanDoItAll.Components.CanvasLib;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.Processes;

public partial class ProcessWorkspace
{
    private const string NoCanvasSelection = "__processes:none__";
    private const string CanvasToolboxWindowId = "processes.canvas.toolbox";
    private const string CanvasSelectionWindowId = "processes.canvas.selection";
    private const string CanvasEditorWindowId = "processes.canvas.editor";

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

    private bool IsDefinitionCanvasActive => string.Equals(detailTab, "steps", StringComparison.Ordinal);

    private bool IsRuntimeCanvasActive => string.Equals(detailTab, "runs", StringComparison.Ordinal) && SelectedRun is not null;

    private bool IsCanvasEditorOpen => canvasEditorWindowState.IsVisible && (canvasRoleDraft is not null || canvasStepDraft is not null);

    private bool IsCanvasEditorCreateMode => canvasEditedRoleTarget is null && canvasEditedStepTarget is null;

    private ProcessStepEditorModel? SelectedCanvasDefinitionStep => ResolveDefinitionStep(selectedCanvasNodeId);

    private ProcessStepRunViewModel? SelectedCanvasRuntimeStep => ResolveRuntimeStep(selectedCanvasNodeId);

    private IReadOnlyList<ProcessCanvasToolboxGroup> DefinitionToolboxGroups
        => ProcessCanvasTemplateCatalog.BuildDefinitionToolboxGroups()
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
            : SelectedCanvasDefinitionStep?.Title ?? "Definition selection";

    private string CanvasSelectionWindowSummary
        => IsRuntimeCanvasActive
            ? "Track runtime status, executor context, and proof-oriented next actions."
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

    private IReadOnlyList<ProcessCanvasRoleTemplate> CanvasRoleTemplates => ProcessCanvasTemplateCatalog.RoleTemplates;

    private IReadOnlyList<ProcessCanvasStepTemplate> CanvasStepTemplates => ProcessCanvasTemplateCatalog.StepTemplates;

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
        await ExecuteCanvasActionAsync(request.ActionId, request.NodeId, request.X, request.Y);
    }

    private async Task HandleCanvasCreateActionAsync(CanvasWorkbenchCreateActionRequest request)
    {
        await ExecuteCanvasActionAsync(request.ActionId, request.SourceNodeId, request.X, request.Y);
    }

    private async Task HandleCanvasNodeOpenedAsync(string nodeId)
    {
        selectedCanvasNodeId = nodeId;
        await OpenCanvasActionDialogAsync();
    }

    private Task HandleCanvasNodeEditedAsync(CanvasWorkbenchNodeEditRequest request)
    {
        if (!IsDefinitionCanvasActive || ResolveDefinitionStep(request.NodeId) is not { } step)
        {
            return Task.CompletedTask;
        }

        step.Title = request.Title.Trim();
        step.Notes = request.Notes.Trim();
        RefreshCanvasSurface();
        return Task.CompletedTask;
    }

    private Task HandleCanvasNodesMovedAsync(CanvasWorkbenchNodesMovedEventArgs args)
    {
        if (!IsDefinitionCanvasActive)
        {
            return Task.CompletedTask;
        }

        foreach (var position in args.Positions)
        {
            var step = ResolveDefinitionStep(position.NodeId);
            if (step is null)
            {
                continue;
            }

            step.CanvasX = position.X;
            step.CanvasY = position.Y;
        }

        RefreshCanvasSurface();
        return Task.CompletedTask;
    }

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
            case ProcessCanvasActionIds.EditDefinitionStep:
                OpenDefinitionStepEditor();
                break;
            case ProcessCanvasActionIds.AddDependentStep:
                OpenCanvasStepEditor(actionId, SelectedCanvasDefinitionStep, x, y);
                break;
            case ProcessCanvasActionIds.AddBranchOutcome:
                AddBranchOutcomeToSelectedStep();
                break;
            case ProcessCanvasActionIds.AddRoleBinding:
                AddRoleBindingToSelectedStep();
                break;
            case ProcessCanvasActionIds.AddArtifactExpectation:
                AddArtifactExpectationToSelectedStep();
                break;
            case ProcessCanvasActionIds.RemoveDefinitionStep:
                RemoveSelectedDefinitionStep();
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
        if (!ProcessCanvasTemplateCatalog.TryCreateRoleDraft(actionId, editor.Roles.Count + 1, out var roleDraft))
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
        var defaultX = x > 0
            ? x
            : sourceStep?.CanvasX + 300 ?? 140 + (editor.Steps.Count * 280);

        if (!ProcessCanvasTemplateCatalog.TryCreateStepDraft(
                actionId.StartsWith("process-step.", StringComparison.Ordinal) ? actionId : ProcessCanvasActionIds.CreateStepImplementation,
                editor.Steps.Count + 1,
                dependencyId,
                defaultX,
                defaultY,
                out var stepDraft))
        {
            return;
        }

        stepDraft.DependsOnBranchOutcomeId = branchOutcomeId;
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

    private Task SaveCanvasEditorAsync()
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
        return Task.CompletedTask;
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

    private void AddBranchOutcomeToSelectedStep()
    {
        if (SelectedCanvasDefinitionStep is null)
        {
            SetError("Select a definition step before adding a branch outcome.");
            return;
        }

        AddBranchOutcome(SelectedCanvasDefinitionStep);
        OpenDefinitionStepEditor();
    }

    private static double ResolveCanvasStepEditorY(ProcessStepEditorModel? sourceStep, Guid? branchOutcomeId, double requestedY)
    {
        if (requestedY > 0)
        {
            return requestedY;
        }

        if (sourceStep is null)
        {
            return 180;
        }

        if (!branchOutcomeId.HasValue || sourceStep.BranchOutcomes.Count == 0)
        {
            return sourceStep.CanvasY;
        }

        var branchIndex = sourceStep.BranchOutcomes.FindIndex(outcome => outcome.Id == branchOutcomeId.Value);
        if (branchIndex < 0)
        {
            return sourceStep.CanvasY;
        }

        var midpoint = (sourceStep.BranchOutcomes.Count - 1) / 2d;
        return sourceStep.CanvasY + ((branchIndex - midpoint) * 220d);
    }

    private void RemoveSelectedDefinitionStep()
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
        SetMessage($"{removedTitle} was removed from the process definition.");
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
        canvasStepDraft?.BranchOutcomes.Remove(branchOutcome);
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

    private ProcessStepEditorModel? ResolveDefinitionStep(string? nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId) || !nodeId.StartsWith("step:", StringComparison.Ordinal))
        {
            return null;
        }

        var rawId = nodeId[5..];
        if (Guid.TryParse(rawId, out var stepId))
        {
            return editor.Steps.FirstOrDefault(step => step.Id == stepId);
        }

        return editor.Steps.FirstOrDefault(step =>
            string.Equals(step.Key, rawId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(step.Title.Replace(' ', '-'), rawId, StringComparison.OrdinalIgnoreCase));
    }

    private ProcessStepEditorModel? ResolveDefinitionStep(Guid? stepId)
        => stepId.HasValue
            ? editor.Steps.FirstOrDefault(step => step.Id == stepId.Value)
            : null;

    private ProcessStepRunViewModel? ResolveRuntimeStep(string? nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId) || !nodeId.StartsWith("run-step:", StringComparison.Ordinal))
        {
            return null;
        }

        var rawId = nodeId[9..];
        return Guid.TryParse(rawId, out var stepRunId)
            ? stepRuns.FirstOrDefault(stepRun => stepRun.Id == stepRunId)
            : null;
    }

    private static string BuildDefinitionNodeId(ProcessStepEditorModel step)
        => step.Id.HasValue
            ? $"step:{step.Id.Value:D}"
            : $"step:{step.Key}";

    private static string BuildRunNodeId(Guid stepRunId)
        => $"run-step:{stepRunId:D}";

    private static ProcessRoleEditorModel CloneRole(ProcessRoleEditorModel source)
    {
        return new ProcessRoleEditorModel
        {
            Id = source.Id,
            Key = source.Key,
            DisplayName = source.DisplayName,
            Purpose = source.Purpose,
            StaffingIntent = source.StaffingIntent,
            PreferredExecutorKind = source.PreferredExecutorKind,
            PreferredProjectAssignmentRole = source.PreferredProjectAssignmentRole,
            IsRequired = source.IsRequired,
            AllowsFallback = source.AllowsFallback,
            RequiresExplicitApproval = source.RequiresExplicitApproval,
            DefaultAllocationPercent = source.DefaultAllocationPercent,
            RoleTemplateSourceKey = source.RoleTemplateSourceKey,
            RoleTemplateSnapshotName = source.RoleTemplateSnapshotName,
            SnapshotSummary = source.SnapshotSummary,
            RequiredSkillIds = source.RequiredSkillIds.ToList()
        };
    }

    private static void CopyRole(ProcessRoleEditorModel source, ProcessRoleEditorModel target)
    {
        target.Id = source.Id;
        target.Key = source.Key;
        target.DisplayName = source.DisplayName;
        target.Purpose = source.Purpose;
        target.StaffingIntent = source.StaffingIntent;
        target.PreferredExecutorKind = source.PreferredExecutorKind;
        target.PreferredProjectAssignmentRole = source.PreferredProjectAssignmentRole;
        target.IsRequired = source.IsRequired;
        target.AllowsFallback = source.AllowsFallback;
        target.RequiresExplicitApproval = source.RequiresExplicitApproval;
        target.DefaultAllocationPercent = source.DefaultAllocationPercent;
        target.RoleTemplateSourceKey = source.RoleTemplateSourceKey;
        target.RoleTemplateSnapshotName = source.RoleTemplateSnapshotName;
        target.SnapshotSummary = source.SnapshotSummary;
        target.RequiredSkillIds = source.RequiredSkillIds.ToList();
    }

    private static ProcessStepEditorModel CloneStep(ProcessStepEditorModel source)
    {
        return new ProcessStepEditorModel
        {
            Id = source.Id,
            Key = source.Key,
            Title = source.Title,
            Subtitle = source.Subtitle,
            Notes = source.Notes,
            StepKind = source.StepKind,
            AllowsManualSkip = source.AllowsManualSkip,
            AllowsSafeRefusal = source.AllowsSafeRefusal,
            RequiresApproval = source.RequiresApproval,
            RequiresDecisionRecord = source.RequiresDecisionRecord,
            InputContractSummary = source.InputContractSummary,
            OutputContractSummary = source.OutputContractSummary,
            EvidenceContractSummary = source.EvidenceContractSummary,
            DecisionRightsSummary = source.DecisionRightsSummary,
            ExceptionPolicySummary = source.ExceptionPolicySummary,
            TargetLeadHours = source.TargetLeadHours,
            DependsOnStepId = source.DependsOnStepId,
            DependsOnBranchOutcomeId = source.DependsOnBranchOutcomeId,
            DecisionRoleRequirementId = source.DecisionRoleRequirementId,
            CanvasX = source.CanvasX,
            CanvasY = source.CanvasY,
            BranchOutcomes = source.BranchOutcomes
                .Select(outcome => new ProcessStepBranchOutcomeEditorModel
                {
                    Id = outcome.Id,
                    Key = outcome.Key,
                    Title = outcome.Title,
                    Description = outcome.Description
                })
                .ToList(),
            RoleAssignments = source.RoleAssignments
                .Select(assignment => new ProcessStepRoleRequirementEditorModel
                {
                    Id = assignment.Id,
                    RoleRequirementId = assignment.RoleRequirementId,
                    ResponsibilityKind = assignment.ResponsibilityKind,
                    IsRequired = assignment.IsRequired,
                    FallbackOrder = assignment.FallbackOrder,
                    RebindPolicySummary = assignment.RebindPolicySummary
                })
                .ToList(),
            ArtifactExpectations = source.ArtifactExpectations
                .Select(artifact => new ProcessArtifactExpectationEditorModel
                {
                    Id = artifact.Id,
                    ArtifactKind = artifact.ArtifactKind,
                    Title = artifact.Title,
                    IsRequired = artifact.IsRequired,
                    TrustRequirement = artifact.TrustRequirement,
                    SensitivityLevel = artifact.SensitivityLevel,
                    RetentionDays = artifact.RetentionDays,
                    AllowedFutureUsageSummary = artifact.AllowedFutureUsageSummary,
                    ValidationRequirementSummary = artifact.ValidationRequirementSummary
                })
                .ToList()
        };
    }

    private static void CopyStep(ProcessStepEditorModel source, ProcessStepEditorModel target)
    {
        target.Id = source.Id;
        target.Key = source.Key;
        target.Title = source.Title;
        target.Subtitle = source.Subtitle;
        target.Notes = source.Notes;
        target.StepKind = source.StepKind;
        target.AllowsManualSkip = source.AllowsManualSkip;
        target.AllowsSafeRefusal = source.AllowsSafeRefusal;
        target.RequiresApproval = source.RequiresApproval;
        target.RequiresDecisionRecord = source.RequiresDecisionRecord;
        target.InputContractSummary = source.InputContractSummary;
        target.OutputContractSummary = source.OutputContractSummary;
        target.EvidenceContractSummary = source.EvidenceContractSummary;
        target.DecisionRightsSummary = source.DecisionRightsSummary;
        target.ExceptionPolicySummary = source.ExceptionPolicySummary;
        target.TargetLeadHours = source.TargetLeadHours;
        target.DependsOnStepId = source.DependsOnStepId;
        target.DependsOnBranchOutcomeId = source.DependsOnBranchOutcomeId;
        target.DecisionRoleRequirementId = source.DecisionRoleRequirementId;
        target.CanvasX = source.CanvasX;
        target.CanvasY = source.CanvasY;
        target.BranchOutcomes = CloneStep(source).BranchOutcomes;
        target.RoleAssignments = CloneStep(source).RoleAssignments;
        target.ArtifactExpectations = CloneStep(source).ArtifactExpectations;
    }

    private sealed record ProcessCanvasNodeActionDialogState(
        string Title,
        string Summary,
        bool IsRuntime);
}
