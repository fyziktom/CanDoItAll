using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Projects;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace CanDoItAll.Modules.Processes;

public partial class ProcessWorkspace
{
    private ProcessWorkspaceStepsTabPresenter StepsTabPresenter => new(this);

    private ProcessWorkspaceRunsTabPresenter RunsTabPresenter => new(this);

    public sealed class ProcessWorkspaceStepsTabPresenter
    {
        private readonly ProcessWorkspace workspace;

        internal ProcessWorkspaceStepsTabPresenter(ProcessWorkspace workspace)
        {
            this.workspace = workspace;
        }

        public ProcessDefinitionEditorModel Editor => workspace.editor;

        public CanvasWorkbenchSurface? CanvasSurface => workspace.canvasSurface;

        public string DefinitionCanvasTool => workspace.definitionCanvasTool;

        public bool CanRecomposeCanvas => workspace.CanRecomposeDefinitionCanvas;

        public bool IsDefinitionCanvasRecompositionInProgress => workspace.isDefinitionCanvasRecompositionInProgress;

        public CanvasWorkbenchWindowState CanvasToolboxWindowState => workspace.canvasToolboxWindowState;

        public CanvasWorkbenchWindowState CanvasSelectionWindowState => workspace.canvasSelectionWindowState;

        public CanvasWorkbenchWindowState CanvasEditorWindowState => workspace.canvasEditorWindowState;

        public string CanvasToolboxWindowId => ProcessWorkspace.CanvasToolboxWindowId;

        public string CanvasSelectionWindowId => ProcessWorkspace.CanvasSelectionWindowId;

        public string CanvasEditorWindowId => ProcessWorkspace.CanvasEditorWindowId;

        public IReadOnlyList<ProcessCanvasToolboxGroup> DefinitionToolboxGroups => workspace.DefinitionToolboxGroups;

        public string CanvasToolboxSearchText
        {
            get => workspace.canvasToolboxSearchText;
            set => workspace.canvasToolboxSearchText = value ?? string.Empty;
        }

        public string CanvasSelectionWindowTitle => workspace.CanvasSelectionWindowTitle;

        public string CanvasSelectionWindowSummary => workspace.CanvasSelectionWindowSummary;

        public ProcessStepEditorModel? SelectedCanvasDefinitionStep => workspace.SelectedCanvasDefinitionStep;

        public ProcessRoleEditorModel? SelectedCanvasDefinitionRole => workspace.SelectedCanvasDefinitionRole;

        public bool IsCanvasEditorOpen => workspace.IsCanvasEditorOpen;

        public bool IsCanvasEditorCreateMode => workspace.IsCanvasEditorCreateMode;

        public string CanvasEditorWindowTitle => workspace.CanvasEditorWindowTitle;

        public string CanvasEditorWindowSummary => workspace.CanvasEditorWindowSummary;

        public ProcessRoleEditorModel? CanvasRoleDraft => workspace.canvasRoleDraft;

        public ProcessStepEditorModel? CanvasStepDraft => workspace.canvasStepDraft;

        public string CanvasTemplateActionId => workspace.canvasTemplateActionId;

        public IReadOnlyList<ProcessCanvasRoleTemplate> CanvasRoleTemplates => workspace.CanvasRoleTemplates;

        public IReadOnlyList<ProcessCanvasStepTemplate> CanvasStepTemplates => workspace.CanvasStepTemplates;

        public IReadOnlyList<ProcessStepEditorModel> CanvasEditorDependencyOptions => workspace.CanvasEditorDependencyOptions;

        public void CaptureWorkbench(CanvasWorkbench? workbench)
        {
            workspace.workbenchRef = workbench;
        }

        public Task HandleCanvasStateChangedAsync(string stateJson)
        {
            return workspace.HandleCanvasStateChangedAsync(stateJson);
        }

        public Task HandleCanvasSelectionChangedAsync(CanvasWorkbenchSelectionChangedEventArgs args)
        {
            return workspace.HandleCanvasSelectionChangedAsync(args);
        }

        public Task HandleCanvasNodesMovedAsync(CanvasWorkbenchNodesMovedEventArgs args)
        {
            return workspace.HandleCanvasNodesMovedAsync(args);
        }

        public Task HandleCanvasContextActionAsync(CanvasWorkbenchContextActionRequest request)
        {
            return workspace.HandleCanvasContextActionAsync(request);
        }

        public Task HandleCanvasCreateActionAsync(CanvasWorkbenchCreateActionRequest request)
        {
            return workspace.HandleCanvasCreateActionAsync(request);
        }

        public Task HandleCanvasNodeEditedAsync(CanvasWorkbenchNodeEditRequest request)
        {
            return workspace.HandleCanvasNodeEditedAsync(request);
        }

        public Task HandleCanvasNodeOpenedAsync(string nodeId)
        {
            return workspace.HandleCanvasNodeOpenedAsync(nodeId);
        }

        public Task SelectDefinitionCanvasToolAsync()
        {
            return workspace.SelectDefinitionCanvasToolAsync();
        }

        public Task DeleteDefinitionCanvasToolAsync()
        {
            return workspace.DeleteDefinitionCanvasToolAsync();
        }

        public Task ToggleCanvasSelectionWindowAsync()
        {
            return workspace.ToggleCanvasSelectionWindowAsync();
        }

        public Task ToggleCanvasToolboxWindowAsync()
        {
            return workspace.ToggleCanvasToolboxWindowAsync();
        }

        public Task ResolveDefinitionCanvasCollisionsAsync()
        {
            return workspace.ResolveDefinitionCanvasCollisionsAsync();
        }

        public Task AddDefinitionCanvasSpaceAroundAsync()
        {
            return workspace.AddDefinitionCanvasSpaceAroundAsync();
        }

        public Task RecomposeDefinitionCanvasAsync()
        {
            return workspace.RecomposeDefinitionCanvasAsync();
        }

        public Task HandleCanvasToolboxWindowStateChangedAsync(CanvasWorkbenchWindowState state)
        {
            return workspace.HandleCanvasToolboxWindowStateChangedAsync(state);
        }

        public Task HandleCanvasSelectionWindowStateChangedAsync(CanvasWorkbenchWindowState state)
        {
            return workspace.HandleCanvasSelectionWindowStateChangedAsync(state);
        }

        public Task HandleCanvasEditorWindowStateChangedAsync(CanvasWorkbenchWindowState state)
        {
            return workspace.HandleCanvasEditorWindowStateChangedAsync(state);
        }

        public Task HandleCanvasToolboxSearchTextChangedAsync(string? value)
        {
            CanvasToolboxSearchText = value ?? string.Empty;
            return Task.CompletedTask;
        }

        public Task OpenToolboxActionAsync(string actionId)
        {
            return workspace.ExecuteCanvasActionAsync(actionId, workspace.selectedCanvasNodeId, 0, 0);
        }

        public Task OpenCanvasToolboxAsync()
        {
            return workspace.OpenCanvasToolboxAsync();
        }

        public Task ClearCanvasSelectionAsync()
        {
            return workspace.ClearCanvasSelectionAsync();
        }

        public Task EditDefinitionStepAsync()
        {
            workspace.OpenDefinitionStepEditor();
            return Task.CompletedTask;
        }

        public Task EditDefinitionRoleAsync()
        {
            workspace.OpenDefinitionRoleEditor();
            return Task.CompletedTask;
        }

        public Task AddDependentStepAsync()
        {
            workspace.OpenCanvasStepEditor(ProcessCanvasActionIds.CreateStepImplementation, workspace.SelectedCanvasDefinitionStep, 0, 0);
            return Task.CompletedTask;
        }

        public Task AddBranchOutcomeToSelectedStepAsync()
        {
            return workspace.AddBranchOutcomeToSelectedStepAsync();
        }

        public Task AddRoutedStepAsync(Guid? branchOutcomeId)
        {
            return workspace.AddRoutedStepFromSelectedStepAsync(branchOutcomeId);
        }

        public Task AddRoleBindingToSelectedStepAsync()
        {
            workspace.AddRoleBindingToSelectedStep();
            return Task.CompletedTask;
        }

        public Task AddArtifactExpectationToSelectedStepAsync()
        {
            workspace.AddArtifactExpectationToSelectedStep();
            return Task.CompletedTask;
        }

        public Task OpenCanvasActionDialogAsync()
        {
            return workspace.OpenCanvasActionDialogAsync();
        }

        public Task HandleCanvasTemplateChangedAsync(ChangeEventArgs args)
        {
            return workspace.HandleCanvasTemplateChangedAsync(args);
        }

        public Task SaveCanvasEditorAsync()
        {
            return workspace.SaveCanvasEditorAsync();
        }

        public Task CloseCanvasEditorAsync()
        {
            return workspace.CloseCanvasEditorAsync();
        }

        public Task AddCanvasBranchOutcomeAsync()
        {
            return workspace.AddCanvasBranchOutcomeAsync();
        }

        public Task RemoveCanvasBranchOutcomeAsync(ProcessStepBranchOutcomeEditorModel branchOutcome)
        {
            return workspace.RemoveCanvasBranchOutcomeAsync(branchOutcome);
        }

        public Task AddCanvasRoleAssignmentAsync()
        {
            return workspace.AddCanvasRoleAssignmentAsync();
        }

        public Task RemoveCanvasRoleAssignmentAsync(ProcessStepRoleRequirementEditorModel assignment)
        {
            return workspace.RemoveCanvasRoleAssignmentAsync(assignment);
        }

        public Task AddCanvasArtifactExpectationAsync()
        {
            return workspace.AddCanvasArtifactExpectationAsync();
        }

        public Task RemoveCanvasArtifactExpectationAsync(ProcessArtifactExpectationEditorModel artifact)
        {
            return workspace.RemoveCanvasArtifactExpectationAsync(artifact);
        }

        public void AddStep()
        {
            workspace.AddStep();
        }

        public void RemoveStep(ProcessStepEditorModel step)
        {
            workspace.RemoveStep(step);
        }

        public void AddBranchOutcome(ProcessStepEditorModel step)
        {
            workspace.AddBranchOutcome(step);
        }

        public void RemoveBranchOutcome(ProcessStepEditorModel step, ProcessStepBranchOutcomeEditorModel branchOutcome)
        {
            workspace.RemoveBranchOutcome(step, branchOutcome);
        }

        public void AddRoleAssignment(ProcessStepEditorModel step)
        {
            workspace.AddRoleAssignment(step);
        }

        public void RemoveRoleAssignment(ProcessStepEditorModel step, ProcessStepRoleRequirementEditorModel assignment)
        {
            workspace.RemoveRoleAssignment(step, assignment);
        }

        public void AddArtifact(ProcessStepEditorModel step)
        {
            workspace.AddArtifact(step);
        }

        public void RemoveArtifact(ProcessStepEditorModel step, ProcessArtifactExpectationEditorModel artifact)
        {
            workspace.RemoveArtifact(step, artifact);
        }

        public Task SaveAsync()
        {
            return workspace.SaveAsync();
        }

        public Task PublishAsync()
        {
            return workspace.PublishAsync();
        }

        public Task OpenTemplateLibraryAsync()
        {
            return workspace.OpenTemplateLibraryAsync();
        }
    }

    public sealed class ProcessWorkspaceRunsTabPresenter
    {
        private readonly ProcessWorkspace workspace;

        internal ProcessWorkspaceRunsTabPresenter(ProcessWorkspace workspace)
        {
            this.workspace = workspace;
        }

        public CanvasWorkbenchSurface? CanvasSurface => workspace.canvasSurface;

        public CanvasWorkbenchWindowState CanvasSelectionWindowState => workspace.canvasSelectionWindowState;

        public string CanvasSelectionWindowId => ProcessWorkspace.CanvasSelectionWindowId;

        public string CanvasSelectionWindowTitle => workspace.CanvasSelectionWindowTitle;

        public string CanvasSelectionWindowSummary => workspace.CanvasSelectionWindowSummary;

        public ProcessStepRunViewModel? SelectedCanvasRuntimeStep => workspace.SelectedCanvasRuntimeStep;

        public bool HasSelectedProcess => workspace.selectedProcessId.HasValue;

        public IReadOnlyList<ProcessRunListItem> Runs => workspace.runs;

        public ProcessRunListItem? SelectedRun => workspace.SelectedRun;

        public IReadOnlyList<ProcessStepRunViewModel> StepRuns => workspace.stepRuns;

        public IReadOnlyList<ProcessDecisionViewModel> Decisions => workspace.decisions;

        public IReadOnlyList<ProcessArtifactViewModel> Artifacts => workspace.artifacts;

        public IReadOnlyList<ProcessRunAssignmentViewModel> Assignments => workspace.assignments;

        public ProcessRunAssignmentViewModel? SelectedAssignment => workspace.SelectedAssignment;

        public IReadOnlyList<ProcessWorkBriefViewModel> WorkBriefs => workspace.workBriefs;

        public IReadOnlyList<ProcessConformanceObservationViewModel> ConformanceObservations => workspace.conformanceObservations;

        public IReadOnlyList<ProjectPartyOption> PartyOptions => workspace.partyOptions;

        public string RunNameDraft
        {
            get => workspace.runNameDraft;
            set => workspace.runNameDraft = value ?? string.Empty;
        }

        public ProcessOperatingMode RunOperatingMode
        {
            get => workspace.runOperatingMode;
            set => workspace.runOperatingMode = value;
        }

        public Guid? ArtifactStepRunId
        {
            get => workspace.artifactStepRunId;
            set => workspace.artifactStepRunId = value;
        }

        public string ArtifactTitle
        {
            get => workspace.artifactTitle;
            set => workspace.artifactTitle = value ?? string.Empty;
        }

        public ProcessArtifactKind ArtifactKind
        {
            get => workspace.artifactKind;
            set => workspace.artifactKind = value;
        }

        public Guid? AssignmentPartyId
        {
            get => workspace.assignmentPartyId;
            set => workspace.assignmentPartyId = value;
        }

        public string AssignmentDisplayName
        {
            get => workspace.assignmentDisplayName;
            set => workspace.assignmentDisplayName = value ?? string.Empty;
        }

        public string AssignmentExecutorKind
        {
            get => workspace.assignmentExecutorKind;
            set => workspace.assignmentExecutorKind = value ?? string.Empty;
        }

        public string AssignmentBindingReason
        {
            get => workspace.assignmentBindingReason;
            set => workspace.assignmentBindingReason = value ?? string.Empty;
        }

        public bool AssignmentIsFallback
        {
            get => workspace.assignmentIsFallback;
            set => workspace.assignmentIsFallback = value;
        }

        public bool AssignmentAllowsDirectMessaging
        {
            get => workspace.assignmentAllowsDirectMessaging;
            set => workspace.assignmentAllowsDirectMessaging = value;
        }

        public Guid? DirectMessageSourceRoleRequirementId
        {
            get => workspace.directMessageSourceRoleRequirementId;
            set => workspace.directMessageSourceRoleRequirementId = value;
        }

        public Guid? DirectMessageTargetRoleRequirementId
        {
            get => workspace.directMessageTargetRoleRequirementId;
            set => workspace.directMessageTargetRoleRequirementId = value;
        }

        public string DirectMessageBody
        {
            get => workspace.directMessageBody;
            set => workspace.directMessageBody = value ?? string.Empty;
        }

        public IReadOnlyList<ProcessRunAssignmentViewModel> DirectMessageAssignments => workspace.DirectMessageAssignments;

        public IReadOnlyList<ProcessDirectMessageThreadViewModel> DirectMessageThreads => workspace.directMessageThreads;

        public bool CanSendDirectMessage
            => workspace.selectedRunId.HasValue &&
               DirectMessageAssignments.Count >= 2 &&
               DirectMessageSourceRoleRequirementId.HasValue &&
               DirectMessageTargetRoleRequirementId.HasValue &&
               DirectMessageSourceRoleRequirementId != DirectMessageTargetRoleRequirementId &&
               !string.IsNullOrWhiteSpace(DirectMessageBody);

        public void CaptureWorkbench(CanvasWorkbench? workbench)
        {
            workspace.workbenchRef = workbench;
        }

        public Task HandleCanvasStateChangedAsync(string stateJson)
        {
            return workspace.HandleCanvasStateChangedAsync(stateJson);
        }

        public Task HandleCanvasSelectionChangedAsync(CanvasWorkbenchSelectionChangedEventArgs args)
        {
            return workspace.HandleCanvasSelectionChangedAsync(args);
        }

        public Task HandleCanvasContextActionAsync(CanvasWorkbenchContextActionRequest request)
        {
            return workspace.HandleCanvasContextActionAsync(request);
        }

        public Task HandleCanvasNodeOpenedAsync(string nodeId)
        {
            return workspace.HandleCanvasNodeOpenedAsync(nodeId);
        }

        public Task ToggleCanvasSelectionWindowAsync()
        {
            return workspace.ToggleCanvasSelectionWindowAsync();
        }

        public Task HandleCanvasSelectionWindowStateChangedAsync(CanvasWorkbenchWindowState state)
        {
            return workspace.HandleCanvasSelectionWindowStateChangedAsync(state);
        }

        public Guid? SelectedCanvasRuntimeBranchOutcomeId
            => workspace.SelectedCanvasRuntimeStep is null
                ? null
                : workspace.ResolveSelectedBranchOutcomeId(workspace.SelectedCanvasRuntimeStep.Id);

        public Task UpdateSelectedCanvasRuntimeBranchOutcomeAsync(Guid? branchOutcomeId)
        {
            return workspace.SelectedCanvasRuntimeStep is null
                ? Task.CompletedTask
                : workspace.UpdateRuntimeBranchOutcomeSelectionAsync(workspace.SelectedCanvasRuntimeStep.Id, branchOutcomeId);
        }

        public Task ClearCanvasSelectionAsync()
        {
            return workspace.ClearCanvasSelectionAsync();
        }

        public Task OpenCanvasActionDialogAsync()
        {
            return workspace.OpenCanvasActionDialogAsync();
        }

        public Task ApplySelectedRuntimeStatusAsync(ProcessStepRunStatus status)
        {
            return workspace.ApplySelectedRuntimeStatusAsync(status);
        }

        public Task PrepareSelectedRuntimeArtifactCaptureAsync()
        {
            workspace.PrepareSelectedRuntimeArtifactCapture();
            return Task.CompletedTask;
        }

        public Task StartRunAsync()
        {
            return workspace.StartRunAsync();
        }

        public Task SelectRunAsync(Guid runId)
        {
            return workspace.SelectRunAsync(runId);
        }

        public string BuildRunSummary(ProcessRunListItem run)
        {
            return ProcessWorkspace.BuildRunSummary(run);
        }

        public string ResolveRunTone(ProcessRunStatus status)
        {
            return ProcessWorkspace.ResolveRunTone(status);
        }

        public Guid? ResolveSelectedBranchOutcomeId(Guid stepRunId)
        {
            return workspace.ResolveSelectedBranchOutcomeId(stepRunId);
        }

        public Task UpdateRuntimeBranchOutcomeSelectionAsync(Guid stepRunId, Guid? branchOutcomeId)
        {
            return workspace.UpdateRuntimeBranchOutcomeSelectionAsync(stepRunId, branchOutcomeId);
        }

        public bool CanApplyRuntimeStatus(ProcessStepRunViewModel? stepRun, ProcessStepRunStatus targetStatus)
        {
            return ProcessWorkspace.CanApplyRuntimeStatus(stepRun, targetStatus);
        }

        public Task ApplyStepStatusAsync(Guid stepRunId, ProcessStepRunStatus targetStatus)
        {
            return workspace.ApplyStepStatusAsync(stepRunId, targetStatus);
        }

        public void SelectAssignment(Guid assignmentId)
        {
            workspace.SelectAssignment(assignmentId);
        }

        public string ResolveRoleName(Guid? roleId)
        {
            return workspace.ResolveRoleName(roleId);
        }

        public Task ResolveSelectedAssignmentAsync()
        {
            return workspace.ResolveSelectedAssignmentAsync();
        }

        public string BuildDirectMessageAssignmentLabel(ProcessRunAssignmentViewModel assignment)
        {
            return workspace.BuildDirectMessageAssignmentLabel(assignment);
        }

        public Task SendDirectMessageAsync()
        {
            return workspace.SendDirectMessageAsync();
        }

        public Task RecordArtifactAsync()
        {
            return workspace.RecordArtifactAsync();
        }

        public string ResolveStepTone(ProcessStepRunStatus status)
        {
            return ProcessWorkspace.ResolveStepTone(status);
        }

        public string ResolveConformanceTone(ProcessConformanceSeverity severity)
        {
            return ProcessWorkspace.ResolveConformanceTone(severity);
        }
    }
}
