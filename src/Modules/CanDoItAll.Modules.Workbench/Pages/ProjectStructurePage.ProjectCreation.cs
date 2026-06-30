using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Projects.Pages.Components;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench.Pages;

public partial class ProjectStructurePage
{
    private static readonly string[] ProjectCreateWizardSteps = ["Identity", "Dates and phases", "Stack profile", "Linked objects", "Review"];

    private static readonly ProjectObjectType[] ProjectCreateStarterObjectKinds =
    [
        ProjectObjectType.Note,
        ProjectObjectType.Decision,
        ProjectObjectType.Milestone,
        ProjectObjectType.Repository,
        ProjectObjectType.Link
    ];

    private readonly List<StarterObjectDraft> projectCreateStarterObjects = [];
    private ProjectEditorModel projectCreateEditor = new();
    private IReadOnlyList<ProjectSummary> projectCreateProjectSummaries = [];
    private ProjectStructureProjectHierarchyDialogState? projectHierarchyDialogBeforeCreate;
    private string? projectCreateMessage;
    private int projectCreateWizardStep;
    private bool showProjectCreateModal;

    private IReadOnlyList<SecondaryTabItem> ProjectCreateWizardTabs => ProjectCreateWizardSteps
        .Select((step, index) => new SecondaryTabItem(index.ToString(), $"{index + 1}. {step}"))
        .ToList();

    private bool IsProjectCreateErrorMessage
        => !string.IsNullOrWhiteSpace(projectCreateMessage) &&
           (projectCreateMessage.StartsWith("Unable", StringComparison.OrdinalIgnoreCase) ||
            projectCreateMessage.StartsWith("No ", StringComparison.OrdinalIgnoreCase));

    private async Task OpenProjectCreateDialogAsync()
    {
        projectHierarchyDialogBeforeCreate = projectHierarchyDialog;
        projectHierarchyDialog = null;
        projectCreateEditor = await ProjectsService.GetAsync(null);
        projectCreateProjectSummaries = await ProjectsService.ListAsync();
        projectCreateStarterObjects.Clear();
        projectCreateWizardStep = 0;
        projectCreateMessage = null;
        showProjectCreateModal = true;
        await InvokeAsync(StateHasChanged);
    }

    private async Task CloseProjectCreateDialogAsync()
    {
        showProjectCreateModal = false;
        projectCreateMessage = null;

        if (projectHierarchyDialogBeforeCreate is { } dialogState)
        {
            projectHierarchyDialog = await RefreshProjectHierarchyDialogAsync(dialogState);
            projectHierarchyDialogBeforeCreate = null;
        }

        await InvokeAsync(StateHasChanged);
    }

    private Task IgnoreProjectCreateModeChangeAsync()
        => Task.CompletedTask;

    private Task HandleProjectCreateWizardTabChangedAsync(string key)
    {
        if (int.TryParse(key, out var parsedStep))
        {
            projectCreateWizardStep = parsedStep;
        }

        return Task.CompletedTask;
    }

    private Task AddProjectCreatePhaseAsync()
    {
        projectCreateEditor.Phases.Add(new ProjectPhaseEditorModel());
        return Task.CompletedTask;
    }

    private Task RemoveProjectCreatePhaseAsync(ProjectPhaseEditorModel phase)
    {
        projectCreateEditor.Phases.Remove(phase);
        return Task.CompletedTask;
    }

    private void AddProjectCreateStarterObject()
        => projectCreateStarterObjects.Add(new StarterObjectDraft());

    private void RemoveProjectCreateStarterObject(StarterObjectDraft starterObject)
        => projectCreateStarterObjects.Remove(starterObject);

    private void PreviousProjectCreateStep()
        => projectCreateWizardStep = Math.Max(0, projectCreateWizardStep - 1);

    private void NextProjectCreateStep()
        => projectCreateWizardStep = Math.Min(ProjectCreateWizardSteps.Length - 1, projectCreateWizardStep + 1);

    private async Task SaveProjectCreateAsync()
    {
        var result = await ProjectsService.SaveAsync(projectCreateEditor);
        projectCreateMessage = result.IsSuccess
            ? "Project saved."
            : string.Join(" ", result.Errors.Select(error => error.Message));
        projectCreateProjectSummaries = await ProjectsService.ListAsync();

        if (!result.IsSuccess)
        {
            await InvokeAsync(StateHasChanged);
            return;
        }

        if (projectCreateStarterObjects.Count > 0)
        {
            await ProjectWorkbenchSeedService.SeedProjectObjectsAsync(
                result.Value,
                projectCreateStarterObjects
                    .Where(item => !string.IsNullOrWhiteSpace(item.Title))
                    .Select(item => new ProjectObjectSeedDraft(item.ObjectType, item.Title, item.Subtitle, item.Subtitle))
                    .ToList());
            projectCreateStarterObjects.Clear();
        }

        projectCreateEditor = await ProjectsService.GetAsync(result.Value);
        showProjectCreateModal = false;

        if (projectHierarchyDialogBeforeCreate is { } dialogState)
        {
            projectHierarchyDialog = await RefreshProjectHierarchyDialogAsync(dialogState, result.Value);
            workflowFeedback = $"{projectCreateEditor.Name} was created. Select it below to connect it.";
            workflowFeedbackTone = "mint";
            projectHierarchyDialogBeforeCreate = null;
        }

        projectCreateMessage = null;
        await InvokeAsync(StateHasChanged);
    }

    private async Task SaveProjectCreateAndOpenStructureAsync()
    {
        var pendingHierarchyDialog = projectHierarchyDialogBeforeCreate;
        await SaveProjectCreateAsync();

        if (projectCreateEditor.Id.HasValue)
        {
            projectHierarchyDialogBeforeCreate = null;
            projectHierarchyDialog = null;
            showProjectCreateModal = false;
            projectCreateMessage = null;
            OpenStructure(projectCreateEditor.Id.Value);
            return;
        }

        projectHierarchyDialogBeforeCreate = pendingHierarchyDialog;
    }

    private Task DeleteProjectCreateAsync()
        => Task.CompletedTask;

    private void OpenProjectCreateDashboardAsync()
    {
    }

    private void OpenStructure(Guid projectId)
    {
        if (projectId != Guid.Empty)
        {
            Navigation.NavigateTo($"/projects/{projectId}/structure");
        }
    }

    private void OpenCalendar(Guid projectId)
    {
        if (projectId != Guid.Empty)
        {
            Navigation.NavigateTo($"/projects/{projectId}/calendar");
        }
    }
}
