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
        var draft = new ProjectEditorDraft(await ProjectsService.GetAsync(null));
        var summaries = await ProjectsService.ListAsync();
        if (!IsCurrentAuthoring(opening, projectCreateOpening) || !ReferenceEquals(parent, hierarchyOpening)) {
            return;
        }
        projectCreateDraft = draft;
        projectCreateProjectSummaries = summaries;
        projectCreateWizardStep = 0;
        projectCreateMessage = null;
        showProjectCreateModal = true;
        await RenderAuthoringOutcomeAsync();
    }

    private async Task CloseProjectCreateDialogAsync(ProjectStructureAuthoringOpening? opening) {
        if (opening is null || !ReferenceEquals(opening, projectCreateOpening)) {
            return;
        }
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

    private async Task SaveProjectCreateAsync(ProjectStructureAuthoringOpening? opening, ProjectEditorDraft target,
        ProjectEditorSubmission submitted, bool openStructure = false) {
        if (opening is null || !showProjectCreateModal || !ReferenceEquals(target, projectCreateDraft) ||
            !target.CanMutate || !target.Validate() || !TryBeginAuthoring(opening, projectCreateOpening)) {
            return;
        }
        var parent = projectCreateParentOpening;
        var parentDialog = projectHierarchyDialogBeforeCreate;
        var targetId = Guid.NewGuid();
        var submissionId = Guid.NewGuid();
        target.Mutation = ProjectEditorMutationState.Saving;
        var outcome = new ProjectStructureAuthoringOutcome(opening.Id, submissionId, opening.Context.Admission,
            ProjectStructureAuthoringOperation.CreateProject, ProjectStructureAuthoringResultKind.Unconfirmed,
            $"Creation of project {targetId:D} was not confirmed. Inspect the project inventory before another create.") { TargetProjectId = targetId };
        try {
            var reservation = await InsightsAdmissions.ReserveCreationAsync(targetId, opening.Id, submissionId);
            var result = await ProjectsService.CreateEditorAsync(reservation, submitted.Model);
            if (result.IsFailure) {
                target.Mutation = ProjectEditorMutationState.Ready;
                outcome = outcome with { Kind = ProjectStructureAuthoringResultKind.Rejected,
                    Message = string.Join(" ", result.Errors.Select(error => error.Message)) };
            } else {
                target.Acknowledge(submitted, result.Value!);
                outcome = outcome with { Kind = ProjectStructureAuthoringResultKind.PartialCommit, Editor = result.Value,
                    Message = $"Project {targetId:D} was saved. Starter-object completion is unconfirmed; inspect Structure before another write." };
                if (submitted.Seeds.Count > 0) {
                    target.Mutation = ProjectEditorMutationState.Seeding;
                    if (ProjectWorkbenchSeedService is not IAdmittedProjectWorkbenchSeedService seeds) {
                        throw new InvalidOperationException("The original project lifetime requires the admitted starter-object owner.");
                    }
                    await seeds.SeedProjectObjectsAsync(result.Value!.Project, submitted.Seeds.Select(seed => seed.Value).ToArray());
                    target.AcknowledgeSeeds(submitted);
                }
                target.Mutation = ProjectEditorMutationState.Ready;
                outcome = outcome with { Kind = ProjectStructureAuthoringResultKind.Committed,
                    Message = $"{result.Value!.Name} was created. Select it below to connect it." };
            }
        } catch (Exception exception) {
            target.Mutation = outcome.Editor is null ? ProjectEditorMutationState.SaveOutcomeUnknown : ProjectEditorMutationState.SeedOutcomeUnknown;
            opening.RequiresObservation = true;
            outcome = outcome with { Failure = exception };
        } finally {
            opening.IsBusy = false;
        }
        target.Message = outcome.Message;
        target.IsError = outcome.Kind != ProjectStructureAuthoringResultKind.Committed;
        RecordAuthoringOutcome(opening, outcome);
        if (IsCurrentAuthoring(opening, projectCreateOpening) && ReferenceEquals(target, projectCreateDraft)) {
            projectCreateMessage = outcome.Message;
            if (outcome.Kind == ProjectStructureAuthoringResultKind.Committed && target.EditRevision == submitted.EditRevision) {
                showProjectCreateModal = false;
                if (openStructure) {
                    projectCreateOpening = null;
                    projectHierarchyDialogBeforeCreate = null;
                    CloseProjectHierarchyDialog();
                    OpenStructure(targetId);
                } else {
                    await RestoreProjectHierarchyAsync(opening, parent, parentDialog, targetId);
                    if (ReferenceEquals(projectCreateOpening, opening)) {
                        projectCreateOpening = null;
                        projectCreateMessage = null;
                    }
                }
            }
        }
        await RenderAuthoringOutcomeAsync();
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
