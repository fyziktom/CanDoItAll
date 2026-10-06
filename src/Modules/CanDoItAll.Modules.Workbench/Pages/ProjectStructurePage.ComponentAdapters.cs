using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;
using CanDoItAll.Workbench.Structure.UI;

namespace CanDoItAll.Modules.Workbench.Pages;

internal sealed class ProjectStructureNodeCreatedWithFollowUpFailureException(
    ProjectStructureNode createdNode,
    Exception innerException)
    : Exception("The project structure node was created, but follow-up work failed.", innerException)
{
    public ProjectStructureNode CreatedNode { get; } = createdNode;
}

public partial class ProjectStructurePage
{
    private Guid structureContextId = Guid.NewGuid();
    private ProjectStructureActionContext? structureContext;
    private string? structureLoadError;

    private Guid StructureContextId {
        get {
            if (surface?.ProjectId == ProjectId && surface.ExpectedProjectAdmission is not null) {
                if (structureContext is null || !IsCurrentAction(structureContext)) {
                    structureContext = CaptureActionContext();
                    structureContextId = Guid.NewGuid();
                }
            } else if (structureContext is not null) {
                structureContext = null;
                structureContextId = Guid.NewGuid();
            }
            return structureContextId;
        }
    }

    private StructurePresentation BuildStructurePresentation() => new(StructureContextId, LoadingSkeletonSnapshot) {
        Canvas = surface?.ProjectId == ProjectId ? canvasSurface : null,
        UnavailableTitle = unavailableState?.Title,
        UnavailableDescription = unavailableState?.Description,
        Error = structureLoadError,
        Toolbar = new() {
            SelectionVisible = SelectionWindowState.IsVisible,
            HealthVisible = HealthWindowState.IsVisible,
            ObjectIndexVisible = ObjectIndexWindowState.IsVisible,
            FilesVisible = FileBrowserWindowState.IsVisible,
            SignalsVisible = SignalsWindowState.IsVisible,
            AgentsVisible = ConversationShellCoordinator.Snapshot().IsCatalogVisible,
            Mode = canvasToolMode,
            CanConnect = selectedNode is not null,
            CanRecompose = CanRecomposeSelectedBranch,
            IsRecomposing = isSubtreeRecompositionInProgress
        },
        Toolbox = new() {
            WindowId = ToolboxWindowKey,
            State = ToolboxWindowState,
            Groups = ToolboxCreateGroups,
            Search = structureToolboxSearchText,
            SourceLabel = ToolboxSourceLabel,
            DefaultWidth = ToolboxWindowDefaultWidth
        },
        SelectionCount = selectedNodeIds.Count,
        ShowFeedback = !SelectionWindowState.IsVisible || SelectionWindowState.IsMinimized,
        Feedback = workflowFeedback,
        FeedbackTone = workflowFeedbackTone,
        HasPendingCleanup = pendingDeletionRecoveries.Count > 0,
        IsRetryingCleanup = isRetryingDeletionCleanup
    };

    private async Task HandleStructureIntentAsync(StructureIntent intent) {
        if (intent.ContextId != StructureContextId || deferredCompletionCts.IsCancellationRequested) {
            return;
        }
        if (intent is StructureToolbarIntent { Command: StructureToolbarCommand.Retry }) {
            await ReloadSurfaceAsync();
            return;
        }
        if (intent is StructureToolbarIntent { Command: StructureToolbarCommand.Projects }) {
            await OpenUnavailableRouteAsync();
            return;
        }
        if (structureContext is null || !IsCurrentAction(structureContext)) {
            return;
        }
        switch (intent) {
            case StructureViewIntent view when view.Index is >= CanvasViewIndex and <= ManagerSummaryViewIndex:
                structureViewIndex = view.Index;
                break;
            case StructureSelectionIntent selection:
                await HandleSelectionChangedAsync(selection.Selection);
                break;
            case StructureNodesMovedIntent move:
                await HandleNodesMovedAsync(move.Move);
                break;
            case StructureContextIntent context:
                await HandleContextActionAsync(context.Request);
                break;
            case StructureComposerOpenedIntent opened:
                CaptureComposer(opened.Opening);
                break;
            case StructureComposerClosedIntent closed:
                composerOpenings.Remove(closed.OpeningId);
                if (composerOpening?.Ownership.Id == closed.OpeningId) {
                    composerOpening = null;
                }
                break;
            case StructureCreateIntent create:
                await HandleCreateActionAsync(create.Request);
                break;
            case StructureNoteEditIntent edit:
                await HandleNodeEditedAsync(edit.Request);
                break;
            case StructureOpenIntent open:
                await HandleNodeOpenedAsync(open.NodeId);
                break;
            case StructureStateIntent state:
                await HandleCanvasStateChangedAsync(state.StateJson);
                break;
            case StructureClipboardIntent clipboard:
                await HandleClipboardRequestedAsync(clipboard.Request);
                break;
            case StructureToolboxWindowIntent window:
                await HandleToolboxWindowStateChangedAsync(window.State);
                break;
            case StructureToolboxSearchIntent search:
                structureToolboxSearchText = search.Search;
                break;
            case StructureToolboxGroupIntent group:
                ExpandToolboxGroup(group.GroupId);
                break;
            case StructureToolboxActionIntent action:
                await HandleToolboxActionSelectedAsync(action.ActionId);
                break;
            case StructureToolbarIntent toolbar:
                await DispatchStructureToolbarAsync(toolbar.Command);
                break;
        }
    }

    private Task DispatchStructureToolbarAsync(StructureToolbarCommand command) => command switch {
        StructureToolbarCommand.Select => SelectCanvasToolAsync(),
        StructureToolbarCommand.Dependency => EnableLinkModeAsync(),
        StructureToolbarCommand.Delete => EnableDeleteModeAsync(),
        StructureToolbarCommand.Inspector => ToggleSelectionWindowAsync(),
        StructureToolbarCommand.Health => ToggleHealthWindowAsync(),
        StructureToolbarCommand.Toolbox => ToggleToolboxWindowAsync(),
        StructureToolbarCommand.ObjectIndex => ToggleObjectIndexWindowAsync(),
        StructureToolbarCommand.Files => OpenProjectFileBrowserAsync(),
        StructureToolbarCommand.Signals => ToggleSignalsWindowAsync(),
        StructureToolbarCommand.Agents => ToggleAgentWindowAsync(),
        StructureToolbarCommand.Gantt => OpenGanttViewAsync(),
        StructureToolbarCommand.Recompose => RecomposeSelectedBranchAsync(),
        StructureToolbarCommand.RetryCleanup => RetryPendingDeletionCleanupAsync(),
        _ => Task.CompletedTask
    };

    private StructureDialogsPresentation BuildStructureDialogs() => new(
        projectHierarchyDialog is { } hierarchy ? new(hierarchy.OpeningId, hierarchy.Title, hierarchy.Copy, hierarchy.SubmitLabel,
            hierarchy.CurrentParentProjectTitle, hierarchy.AvailableProjects.Select(project => new StructureProjectOption(project.Id, project.Name)).ToArray(),
            hierarchy.SelectedProjectId, hierarchy.Error, hierarchy.IsBusy, hierarchy.RequiresObservation) : null,
        blockMutationDialog is { } block ? new(block.OpeningId, block.NodeId, block.Title, block.Copy, block.SubmitLabel,
            block.SelectionLabel, block.Options, block.SelectedActionId, block.Error, block.IsBusy, block.RequiresObservation) : null,
        subprojectTransferDialog is { } transfer ? new(transfer.OpeningId, transfer.SourceNodeId, transfer.Title, transfer.Copy,
            transfer.SubmitLabel, transfer.DescendantCount, transfer.ProjectName, transfer.Error, transfer.IsBusy, transfer.RequiresObservation) : null);

    private Task HandleStructureDialogIntentAsync(StructureDialogIntent intent) {
        var hierarchy = hierarchyOpening;
        var conversion = blockOpening;
        var transfer = transferOpening;
        switch (intent) {
            case StructureHierarchySelection selected when hierarchy?.Id == selected.OpeningId:
                ChangeAuthoringDialog(hierarchy, hierarchyOpening, () => HandleProjectHierarchySelectionChanged(new() { Value = selected.ProjectId?.ToString() }));
                break;
            case StructureConversionSelection selected when conversion?.Id == selected.OpeningId:
                ChangeAuthoringDialog(conversion, blockOpening, () => HandleBlockMutationSelectionChanged(new() { Value = selected.ActionId }));
                break;
            case StructureTransferName name when transfer?.Id == name.OpeningId:
                ChangeAuthoringDialog(transfer, transferOpening, () => HandleSubprojectTransferNameChanged(new() { Value = name.Name }));
                break;
            case StructureDialogCommand { Operation: StructureDialogOperation.CloseHierarchy } when hierarchy?.Id == intent.OpeningId:
                ChangeAuthoringDialog(hierarchy, hierarchyOpening, CloseProjectHierarchyDialog);
                break;
            case StructureDialogCommand { Operation: StructureDialogOperation.CloseConversion } when conversion?.Id == intent.OpeningId:
                ChangeAuthoringDialog(conversion, blockOpening, CloseBlockMutationDialog);
                break;
            case StructureDialogCommand { Operation: StructureDialogOperation.CloseTransfer } when transfer?.Id == intent.OpeningId:
                ChangeAuthoringDialog(transfer, transferOpening, CloseSubprojectTransferDialog);
                break;
            case StructureDialogCommand { Operation: StructureDialogOperation.SubmitHierarchy } when hierarchy?.Id == intent.OpeningId:
                return DispatchAuthoringDialogAsync(hierarchy, hierarchyOpening, ExecuteProjectHierarchyCommandAsync);
            case StructureDialogCommand { Operation: StructureDialogOperation.CreateProject } when hierarchy?.Id == intent.OpeningId:
                return DispatchAuthoringDialogAsync(hierarchy, hierarchyOpening, OpenProjectCreateDialogAsync);
            case StructureDialogCommand { Operation: StructureDialogOperation.SubmitConversion } when conversion?.Id == intent.OpeningId:
                return DispatchAuthoringDialogAsync(conversion, blockOpening, ExecuteBlockMutationAsync);
            case StructureDialogCommand { Operation: StructureDialogOperation.SubmitTransfer } when transfer?.Id == intent.OpeningId:
                return DispatchAuthoringDialogAsync(transfer, transferOpening, ExecuteSubprojectTransferAsync);
        }
        return Task.CompletedTask;
    }

    [Inject]
    private NotificationService NotificationService { get; set; } = default!;

    [Inject]
    private ProjectStructureCanvasTaskDialogCoordinator CanvasTaskDialogCoordinator { get; set; } = default!;

    [Inject]
    private ProjectStructureTextAssetCreationCoordinator TextAssetCreationCoordinator { get; set; } = default!;

    private Task ToggleSelectionWindowAsync()
        => ToggleWindowAsync(SelectionWindowKey);

    private Task ToggleHealthWindowAsync()
        => ToggleWindowAsync(HealthWindowKey);

    private Task ToggleToolboxWindowAsync()
        => ToggleWindowAsync(ToolboxWindowKey);

    private Task ToggleObjectIndexWindowAsync()
        => ToggleWindowAsync(ObjectIndexWindowKey);

    private Task HandleToolboxActionSelectedAsync(string actionId)
    {
        var action = ToolboxCreateGroups
            .SelectMany(group => group.Actions)
            .FirstOrDefault(candidate => string.Equals(candidate.ActionId, actionId, StringComparison.Ordinal));
        return action is null
            ? Task.CompletedTask
            : OpenCreateDialogAsync(action);
    }

    private Task HandleProjectHierarchySelectionChangedAsync(Guid? selectedProjectId)
    {
        HandleProjectHierarchySelectionChanged(new ChangeEventArgs
        {
            Value = selectedProjectId?.ToString()
        });
        return Task.CompletedTask;
    }

    private Task HandleTranscriptProviderChangedAsync(Guid? selectedProviderId)
    {
        HandleTranscriptProviderChanged(new ChangeEventArgs
        {
            Value = selectedProviderId?.ToString()
        });
        return Task.CompletedTask;
    }

    private Task HandleSummaryStatusChangedAsync((string NodeId, string Status) request)
    {
        return ChangeSummaryStatusAsync(
            request.NodeId,
            new ChangeEventArgs
            {
                Value = request.Status
            });
    }

    private Task OpenTaskCreateDialogAsync(
        CanvasWorkbenchCreateActionRequest createRequest)
        => CanvasTaskDialogCoordinator.OpenCreateAsync(
            CreateCanvasTaskDialogContext(),
            createRequest,
            deferredCompletionCts.Token);

    private Task OpenTaskEditDialogAsync(
        ProjectStructureNode taskNode,
        ProjectStructureNodeEditModel editModel)
        => CanvasTaskDialogCoordinator.OpenEditAsync(
            CreateCanvasTaskDialogContext(),
            taskNode,
            editModel.Request,
            deferredCompletionCts.Token);

    private Task CreateTextAssetAsync(
        ProjectStructureCreateLeafDefinition definition,
        CanvasWorkbenchCreateActionRequest createRequest)
        => TextAssetCreationCoordinator.CreateAsync(
            new ProjectStructureTextAssetCreationContext(ProjectId, CreateTextAssetNodeAsync),
            definition,
            createRequest,
            deferredCompletionCts.Token);

    private async Task<ProjectStructureNode?> CreateTextAssetNodeAsync(
        ProjectStructureCreateLeafDefinition definition,
        CanvasWorkbenchCreateActionRequest createRequest,
        ProjectObjectMediaPayload media,
        CancellationToken cancellationToken)
    {
        ProjectStructureNode? committedNode = null;
        try
        {
            return await CreateObjectAsync(
                definition,
                createRequest,
                request => request with { Media = media },
                cancellationToken,
                node => committedNode = node);
        }
        catch (Exception exception) when (committedNode is not null)
        {
            throw new ProjectStructureNodeCreatedWithFollowUpFailureException(
                committedNode,
                exception);
        }
    }

    private ProjectStructureCanvasTaskDialogContext CreateCanvasTaskDialogContext() {
        var action = CaptureActionContext();
        var openedSurface = action.Surface;
        var owner = CreateProjectStructureUiAgentContext() with { ExpectedProjectAdmission = openedSurface.ExpectedProjectAdmission };
        ProjectAssignmentAdmission.Require(openedSurface.ProjectId, owner.ExpectedProjectAdmission);
        return new(openedSurface.ProjectId, BuildNodeOptions(ProjectObjectType.Repository),
            (request, configure) => CreateCanvasTaskNodeAsync(action, request, configure),
            taskId => IsCurrentAction(action) ? ReloadSurfaceAsync(taskId) : Task.CompletedTask, owner) {
            IsCurrent = () => IsCurrentAction(action)
        };
    }

    private async Task<ProjectStructureNode?> CreateCanvasTaskNodeAsync(
        ProjectStructureActionContext action,
        CanvasWorkbenchCreateActionRequest createRequest,
        Func<ProjectObjectCreateRequest, ProjectObjectCreateRequest> configureRequest)
    {
        if (!ProjectStructureCanvasCatalog.TryResolveCreateDefinition(
                ProjectStructureCanvasCatalog.WorkTaskActionId,
                out var definition))
        {
            throw new InvalidOperationException(
                "The canonical task definition is unavailable.");
        }

        if (!IsCurrentAction(action)) {
            throw new InvalidOperationException("The original task editor is no longer current.");
        }
        ProjectStructureNode? committed = null;
        try {
            return await CreateObjectAsync(definition, createRequest, configureRequest,
                cancellationToken: CancellationToken.None, onNodeCommitted: node => committed = node,
                capturedSurface: action.Surface, capturedNavigationRevision: action.NavigationRevision);
        } catch (Exception failure) when (committed is not null) {
            throw new ProjectStructureNodeCreatedWithFollowUpFailureException(committed, failure);
        }
    }
}
