using CanDoItAll.AgentFramework.Components;
using CanDoItAll.Components.CanvasLib;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace CanDoItAll.Modules.Processes;

public partial class ProcessWorkspace
{
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

        public CanvasWorkbenchWindowState CanvasAgentWindowState => workspace.canvasAgentWindowState;

        public string CanvasToolboxWindowId => ProcessWorkspace.CanvasToolboxWindowId;

        public string CanvasSelectionWindowId => ProcessWorkspace.CanvasSelectionWindowId;

        public string CanvasEditorWindowId => ProcessWorkspace.CanvasEditorWindowId;

        public string CanvasAgentWindowId => ProcessWorkspace.CanvasAgentWindowId;

        public string CanvasAgentChatWindowId => ProcessWorkspace.CanvasAgentChatWindowId;

        public Guid? SelectedProcessId => workspace.selectedProcessId;

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

        public Task ToggleCanvasAgentWindowAsync()
        {
            return workspace.ToggleCanvasAgentWindowAsync();
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

        public Task HandleCanvasAgentWindowStateChangedAsync(CanvasWorkbenchWindowState state)
        {
            return workspace.HandleCanvasAgentWindowStateChangedAsync(state);
        }

        public Task HandleAgentWorkspaceRefreshRequestedAsync(ContextualAgentWorkspaceRefreshRequest request)
        {
            return workspace.HandleAgentWorkspaceRefreshRequestedAsync(request);
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
}
