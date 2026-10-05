using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;

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
