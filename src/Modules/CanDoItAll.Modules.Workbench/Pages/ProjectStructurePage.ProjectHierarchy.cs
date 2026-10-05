using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.Workbench.Pages;

public partial class ProjectStructurePage
{
    private const string ProjectChildNodePrefix = "project-child:";

    private ProjectStructureProjectHierarchyDialogState? projectHierarchyDialog;
    private ProjectStructureAuthoringOpening? hierarchyOpening;

    private async Task OpenAddSubprojectDialogAsync(ProjectStructureNode node)
        => await OpenProjectHierarchyDialogAsync(node, ProjectStructureProjectHierarchyDialogMode.AddSubproject);

    private async Task OpenReconnectSubprojectDialogAsync(ProjectStructureNode node)
        => await OpenProjectHierarchyDialogAsync(node, ProjectStructureProjectHierarchyDialogMode.ReconnectSubproject);

    private void CloseProjectHierarchyDialog() {
        hierarchyOpening = null;
        projectHierarchyDialog = null;
    }

    private void HandleProjectHierarchySelectionChanged(ChangeEventArgs args)
    {
        if (projectHierarchyDialog is null || projectHierarchyDialog.IsBusy || projectHierarchyDialog.RequiresObservation)
        {
            return;
        }

        var selectedProjectId = Guid.TryParse(args.Value?.ToString(), out var parsedProjectId)
            ? parsedProjectId
            : (Guid?)null;
        projectHierarchyDialog = projectHierarchyDialog with
        {
            SelectedProjectId = selectedProjectId,
            Error = string.Empty
        };
    }

    private async Task ExecuteProjectHierarchyCommandAsync() {
        var dialog = projectHierarchyDialog;
        var opening = hierarchyOpening;
        if (dialog is null || opening is null || !IsCurrentAuthoring(opening, hierarchyOpening)) {
            return;
        }
        if (dialog.SelectedProjectId is not { } selectedId ||
            dialog.AvailableProjects.FirstOrDefault(project => project.Id == selectedId) is not { } selected) {
            projectHierarchyDialog = dialog with { Error = "Select a project before continuing." };
            return;
        }
        if (!TryBeginAuthoring(opening, hierarchyOpening)) {
            return;
        }
        projectHierarchyDialog = dialog with { IsBusy = true, Error = string.Empty };
        var submissionId = Guid.NewGuid();
        var operation = dialog.Mode == ProjectStructureProjectHierarchyDialogMode.AddSubproject
            ? ProjectStructureAuthoringOperation.AddSubproject : ProjectStructureAuthoringOperation.ReconnectSubproject;
        var participants = new[] { dialog.SubjectProjectId, dialog.CurrentParentProjectId, selectedId }
            .OfType<Guid>().Distinct().Select(id => dialog.Admissions[id]).ToArray();
        var outcome = new ProjectStructureAuthoringOutcome(opening.Id, submissionId, opening.Context.Admission,
            operation, ProjectStructureAuthoringResultKind.Unconfirmed,
            $"The hierarchy result for project {dialog.SubjectProjectId:D} and project {selectedId:D} was not confirmed. Inspect the original hierarchy before retrying.") {
                HierarchyProjects = participants, TargetProjectId = selectedId
            };
        try {
            var result = dialog.Mode switch {
                ProjectStructureProjectHierarchyDialogMode.AddSubproject => await ProjectsService.AddSubprojectAsync(
                    dialog.SubjectProjectId, selectedId, expectedProjectAdmissions: participants),
                ProjectStructureProjectHierarchyDialogMode.ReconnectSubproject when dialog.CurrentParentProjectId is { } parentId =>
                    await ProjectsService.ReconnectSubprojectAsync(dialog.SubjectProjectId, parentId, selectedId,
                        expectedProjectAdmissions: participants),
                _ => Result.Failure(Error.Validation("The selected hierarchy action is no longer valid."))
            };
            if (result.IsFailure) {
                outcome = outcome with { Kind = ProjectStructureAuthoringResultKind.Rejected,
                    Message = string.Join(" ", result.Errors.Select(error => error.Message)) };
            } else {
                outcome = outcome with { Kind = ProjectStructureAuthoringResultKind.Committed,
                    Message = dialog.Mode == ProjectStructureProjectHierarchyDialogMode.AddSubproject
                        ? $"{selected.Name} is now visible under {dialog.SubjectProjectTitle}."
                        : $"{dialog.SubjectProjectTitle} now belongs to {selected.Name}." };
            }
        } catch (ProjectWriteAdmissionRejectedException exception) {
            outcome = outcome with { Kind = ProjectStructureAuthoringResultKind.Rejected, Message = exception.Message, Failure = exception };
        } catch (Exception exception) {
            opening.RequiresObservation = true;
            outcome = outcome with { Failure = exception };
        } finally {
            opening.IsBusy = false;
        }
        RecordAuthoringOutcome(opening, outcome);
        if (IsCurrentAuthoring(opening, hierarchyOpening)) {
            if (outcome.Kind == ProjectStructureAuthoringResultKind.Committed) {
                projectHierarchyDialog = null;
                var selection = BuildProjectChildNodeKey(dialog.Mode == ProjectStructureProjectHierarchyDialogMode.AddSubproject
                    ? selectedId : dialog.SubjectProjectId);
                await RefreshAuthoringSurfaceAsync(opening, outcome, selection, () => ReferenceEquals(opening, hierarchyOpening));
                if (ReferenceEquals(opening, hierarchyOpening)) {
                    hierarchyOpening = null;
                }
            } else {
                projectHierarchyDialog = dialog with { Error = outcome.Message, RequiresObservation = opening.RequiresObservation };
            }
        }
        await RenderAuthoringOutcomeAsync();
    }

    private async Task OpenProjectStructureInNewTabAsync(ProjectStructureNode node)
    {
        if (!node.RelatedProjectId.HasValue || node.ProjectRole == ProjectStructureProjectRole.ActiveProject)
        {
            workflowFeedback = "The selected node does not point to another project structure.";
            workflowFeedbackTone = "warn";
            await InvokeAsync(StateHasChanged);
            return;
        }

        await OpenArtifactInNewTabAsync($"/projects/{node.RelatedProjectId.Value}/structure");
    }

    private async Task OpenProjectHierarchyDialogAsync(
        ProjectStructureNode node,
        ProjectStructureProjectHierarchyDialogMode mode)
    {
        CloseQuickActionDialog();
        var subjectProjectId = node.RelatedProjectId ?? ProjectId;
        Guid? currentParentProjectId = null;
        var currentParentProjectTitle = string.Empty;

        if (mode == ProjectStructureProjectHierarchyDialogMode.ReconnectSubproject)
        {
            currentParentProjectId = ResolveVisibleProjectParentId(node);
            if (!currentParentProjectId.HasValue)
            {
                workflowFeedback = "The selected project does not expose a visible parent to reconnect from this canvas.";
                workflowFeedbackTone = "warn";
                await InvokeAsync(StateHasChanged);
                return;
            }

            currentParentProjectTitle = ResolveProjectTitle(currentParentProjectId.Value);
        }

        var opening = new ProjectStructureAuthoringOpening(CaptureActionContext(), node);
        hierarchyOpening = opening;
        projectHierarchyDialog = new(mode, subjectProjectId, node.Title, currentParentProjectId,
            currentParentProjectTitle, [], null, string.Empty) { OpeningId = opening.Id, IsBusy = true };
        await RenderAuthoringOutcomeAsync();
        try {
            var dialog = await BuildProjectHierarchyDialogStateAsync(opening, mode, subjectProjectId,
                node.Title, currentParentProjectId, currentParentProjectTitle, null);
            if (IsCurrentAuthoring(opening, hierarchyOpening)) {
                projectHierarchyDialog = dialog;
                await RenderAuthoringOutcomeAsync();
            }
        } catch (Exception exception) {
            if (IsCurrentAuthoring(opening, hierarchyOpening)) {
                if (exception is ProjectWriteAdmissionRejectedException) {
                    CloseProjectHierarchyDialog();
                } else {
                    projectHierarchyDialog = projectHierarchyDialog! with { IsBusy = false, Error = exception.Message };
                }
                ReportActionFailure(opening.Context, exception);
                await RenderAuthoringOutcomeAsync();
            }
        }
    }

    private async Task<ProjectStructureProjectHierarchyDialogState> BuildProjectHierarchyDialogStateAsync(
        ProjectStructureAuthoringOpening opening,
        ProjectStructureProjectHierarchyDialogMode mode,
        Guid subjectProjectId,
        string subjectProjectTitle,
        Guid? currentParentProjectId,
        string currentParentProjectTitle,
        Guid? selectedProjectId)
    {
        var projects = await ProjectsService.ListAsync();
        var hierarchyLinks = await ProjectsService.ListHierarchyLinksAsync();
        var admissions = projects.Where(project => project.ExpectedProjectAdmission is not null)
            .ToDictionary(project => project.Id, project => project.ExpectedProjectAdmission!);
        foreach (var original in opening.Context.Surface.HierarchyAdmissions.Values.Append(opening.Context.Admission)) {
            admissions[original.ProjectId] = original;
        }
        var availableProjects = projects
            .Where(project => CanSelectHierarchyProject(project.Id, mode, subjectProjectId, currentParentProjectId, hierarchyLinks))
            .OrderBy(project => project.Name, StringComparer.OrdinalIgnoreCase).ThenBy(project => project.Id).ToArray();
        await InsightsAdmissions.RequireManyCurrentAsync(admissions.Values.Where(admission =>
            admission.ProjectId == subjectProjectId || admission.ProjectId == currentParentProjectId ||
            admission.ProjectId == opening.Context.Admission.ProjectId).ToArray());
        if (selectedProjectId.HasValue &&
            availableProjects.All(project => project.Id != selectedProjectId.Value))
        {
            selectedProjectId = null;
        }

        return new ProjectStructureProjectHierarchyDialogState(
            mode,
            subjectProjectId,
            subjectProjectTitle,
            currentParentProjectId,
            currentParentProjectTitle,
            availableProjects,
            selectedProjectId,
            string.Empty) { OpeningId = opening.Id, Admissions = admissions };
    }

    private async Task<ProjectStructureProjectHierarchyDialogState> RefreshProjectHierarchyDialogAsync(
        ProjectStructureAuthoringOpening opening,
        ProjectStructureProjectHierarchyDialogState dialogState,
        Guid? selectedProjectId = null) {
        return await BuildProjectHierarchyDialogStateAsync(opening,
            dialogState.Mode, dialogState.SubjectProjectId, dialogState.SubjectProjectTitle,
            dialogState.CurrentParentProjectId, dialogState.CurrentParentProjectTitle,
            selectedProjectId ?? dialogState.SelectedProjectId);
    }

    private static bool CanSelectHierarchyProject(
        Guid candidateProjectId,
        ProjectStructureProjectHierarchyDialogMode mode,
        Guid subjectProjectId,
        Guid? currentParentProjectId,
        IReadOnlyList<ProjectHierarchyLinkSummary> hierarchyLinks)
    {
        return mode switch
        {
            ProjectStructureProjectHierarchyDialogMode.AddSubproject =>
                ProjectStructureProjectHierarchySelectionPolicy.CanAttachProjectAsSubproject(
                    subjectProjectId,
                    candidateProjectId,
                    hierarchyLinks),
            ProjectStructureProjectHierarchyDialogMode.ReconnectSubproject =>
                ProjectStructureProjectHierarchySelectionPolicy.CanReconnectProjectToParent(
                    subjectProjectId,
                    candidateProjectId,
                    currentParentProjectId,
                    hierarchyLinks),
            _ => false
        };
    }

    private Guid? ResolveVisibleProjectParentId(ProjectStructureNode node)
    {
        if (surface is null || string.IsNullOrWhiteSpace(node.ParentId))
        {
            return null;
        }

        return surface.Nodes
            .FirstOrDefault(candidate => string.Equals(candidate.Id, node.ParentId, StringComparison.Ordinal))
            ?.RelatedProjectId;
    }

    private string ResolveProjectTitle(Guid projectId)
        => projectHierarchyDialog?.AvailableProjects.FirstOrDefault(project => project.Id == projectId)?.Name ??
           surface?.Nodes.FirstOrDefault(node => node.RelatedProjectId == projectId)?.Title ??
           "Selected project";

    private static string BuildProjectChildNodeKey(Guid projectId)
        => $"{ProjectChildNodePrefix}{projectId}";
}
