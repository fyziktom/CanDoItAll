using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.Workbench.Pages;

public partial class ProjectStructurePage
{
    [Inject]
    private NotificationService NotificationService { get; set; } = default!;

    [Inject]
    private ProjectStructureCanvasTaskDialogCoordinator CanvasTaskDialogCoordinator { get; set; } = default!;

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

    private ProjectStructureCanvasTaskDialogContext CreateCanvasTaskDialogContext()
        => new(
            ProjectId,
            BuildNodeOptions(ProjectObjectType.Repository),
            CreateCanvasTaskNodeAsync,
            ReloadSurfaceAsync);

    private Task<ProjectStructureNode?> CreateCanvasTaskNodeAsync(
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

        return CreateObjectAsync(
            definition,
            createRequest,
            configureRequest);
    }
}
