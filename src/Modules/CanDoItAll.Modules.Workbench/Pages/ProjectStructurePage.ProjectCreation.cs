using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Projects.Pages.Components;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging;

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
    private ProjectStructureAuthoringOpening? projectCreateOpening;
    private ProjectStructureAuthoringOpening? projectCreateParentOpening;
    private ProjectStructureProjectHierarchyDialogState? projectHierarchyDialogBeforeCreate;
    private string? projectCreateMessage;
    private int projectCreateWizardStep;
    private bool showProjectCreateModal;

    private IReadOnlyList<SecondaryTabItem> ProjectCreateWizardTabs => ProjectCreateWizardSteps
        .Select((step, index) => new SecondaryTabItem(index.ToString(), $"{index + 1}. {step}"))
        .ToList();

    private bool IsProjectCreateErrorMessage
        => projectCreateDraft.IsError;

    private async Task OpenProjectCreateDialogAsync() {
        var parent = hierarchyOpening;
        var dialog = projectHierarchyDialog;
        if (parent is null || dialog is null || parent.IsBusy || parent.RequiresObservation || !IsCurrentAuthoring(parent, hierarchyOpening)) {
            return;
        }
        var opening = new ProjectStructureAuthoringOpening(parent.Context, parent.Node);
        projectCreateOpening = opening;
        projectCreateParentOpening = parent;
        projectHierarchyDialogBeforeCreate = dialog;
        projectHierarchyDialog = null;
        try {
            var acquired = await ProjectCreateDefinitions.AcquireEditorAsync(null, deferredCompletionCts.Token);
            var (context, pending) = await ReadProjectCreatePendingAsync(acquired.Scope);
            var summaries = await ProjectsService.ListAsync();
            if (!IsCurrentAuthoring(opening, projectCreateOpening) || !ReferenceEquals(parent, hierarchyOpening)) {
                return;
            }
            projectCreateDraft = new(acquired.Model, acquired.Scope);
            projectCreateContext = context;
            projectCreatePending = pending;
            projectCreateProjectSummaries = summaries;
            projectCreateWizardStep = 0;
            projectCreateMessage = null;
            showProjectCreateModal = true;
        } catch (Exception exception) {
            if (IsCurrentAuthoring(opening, projectCreateOpening)) {
                projectCreateRecoveryNotice = "Project creation is unavailable because its original context or retained saves could not be read. Open Projects recovery before trying again.";
                Logger.LogWarning(exception, "Workbench root-create editor could not acquire its original dataset context.");
                projectCreateOpening = null;
                await RestoreProjectHierarchyAsync(opening, parent, dialog);
            }
        }
        await RenderAuthoringOutcomeAsync();
    }

    private async Task CloseProjectCreateDialogAsync(ProjectStructureAuthoringOpening? opening) {
        if (opening is null || !ReferenceEquals(opening, projectCreateOpening)) {
            return;
        }
        RetireProjectCreateSession();
        projectCreateOpening = null;
        showProjectCreateModal = false;
        projectCreateMessage = null;
        await RestoreProjectHierarchyAsync(opening, projectCreateParentOpening, projectHierarchyDialogBeforeCreate);
        await RenderAuthoringOutcomeAsync();
    }

    private async Task RestoreProjectHierarchyAsync(ProjectStructureAuthoringOpening creation,
        ProjectStructureAuthoringOpening? parent, ProjectStructureProjectHierarchyDialogState? dialog, Guid? selectedId = null) {
        if (parent is null || dialog is null || !IsCurrentAuthoring(parent, hierarchyOpening)) {
            return;
        }
        var refreshed = await RefreshProjectHierarchyDialogAsync(parent, dialog, selectedId);
        if (IsCurrentAuthoring(parent, hierarchyOpening) &&
            (projectCreateOpening is null || ReferenceEquals(projectCreateOpening, creation))) {
            projectHierarchyDialog = refreshed;
            projectHierarchyDialogBeforeCreate = null;
        }
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
