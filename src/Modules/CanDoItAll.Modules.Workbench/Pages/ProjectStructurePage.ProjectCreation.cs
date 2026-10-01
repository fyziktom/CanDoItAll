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

    private ProjectEditorDraft projectCreateDraft = new(new());
    private List<StarterObjectDraft> projectCreateStarterObjects => projectCreateDraft.StarterObjects;
    private ProjectEditorModel projectCreateEditor => projectCreateDraft.Model;
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
        projectCreateDraft = new(await ProjectsService.GetAsync(null));
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

    private async Task SaveProjectCreateAsync(ProjectEditorSubmission submitted)
    {
        var result = await ProjectsService.SaveAsync(submitted.Model);
        projectCreateMessage = result.IsSuccess
            ? "Project saved."
            : string.Join(" ", result.Errors.Select(error => error.Message));
        projectCreateProjectSummaries = await ProjectsService.ListAsync();

        if (!result.IsSuccess)
        {
            await InvokeAsync(StateHasChanged);
            return;
        }

        if (submitted.Seeds.Count > 0)
        {
            await ProjectWorkbenchSeedService.SeedProjectObjectsAsync(
                result.Value,
                submitted.Seeds.Select(item => item.Value)
                    .ToList());
            projectCreateStarterObjects.Clear();
        }

        projectCreateDraft = new(await ProjectsService.GetAsync(result.Value));
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

    private async Task SaveProjectCreateAndOpenStructureAsync(ProjectEditorSubmission submitted)
    {
        var pendingHierarchyDialog = projectHierarchyDialogBeforeCreate;
        await SaveProjectCreateAsync(submitted);

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
