using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.Workbench.Pages;

public partial class ProjectStructurePage
{
    private const string ProjectChildNodePrefix = "project-child:";

    private ProjectStructureProjectHierarchyDialogState? projectHierarchyDialog;

    private enum ProjectStructureProjectHierarchyDialogMode
    {
        AddSubproject,
        ReconnectSubproject
    }

    private async Task OpenAddSubprojectDialogAsync(ProjectStructureNode node)
        => await OpenProjectHierarchyDialogAsync(node, ProjectStructureProjectHierarchyDialogMode.AddSubproject);

    private async Task OpenReconnectSubprojectDialogAsync(ProjectStructureNode node)
        => await OpenProjectHierarchyDialogAsync(node, ProjectStructureProjectHierarchyDialogMode.ReconnectSubproject);

    private void CloseProjectHierarchyDialog()
        => projectHierarchyDialog = null;

    private void HandleProjectHierarchySelectionChanged(ChangeEventArgs args)
    {
        if (projectHierarchyDialog is null)
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

    private async Task ExecuteProjectHierarchyCommandAsync()
    {
        if (projectHierarchyDialog is null)
        {
            return;
        }

        if (!projectHierarchyDialog.SelectedProjectId.HasValue)
        {
            projectHierarchyDialog = projectHierarchyDialog with { Error = "Select a project before continuing." };
            return;
        }

        var selectedProject = projectHierarchyDialog.AvailableProjects
            .FirstOrDefault(project => project.Id == projectHierarchyDialog.SelectedProjectId.Value);
        var result = projectHierarchyDialog.Mode switch
        {
            ProjectStructureProjectHierarchyDialogMode.AddSubproject => await ProjectsService.AddSubprojectAsync(
                projectHierarchyDialog.SubjectProjectId,
                projectHierarchyDialog.SelectedProjectId.Value),
            ProjectStructureProjectHierarchyDialogMode.ReconnectSubproject when projectHierarchyDialog.CurrentParentProjectId.HasValue =>
                await ProjectsService.ReconnectSubprojectAsync(
                    projectHierarchyDialog.SubjectProjectId,
                    projectHierarchyDialog.CurrentParentProjectId.Value,
                    projectHierarchyDialog.SelectedProjectId.Value),
            _ => Result.Failure(Error.Validation("The selected hierarchy action is no longer valid."))
        };
        if (result.IsFailure)
        {
            projectHierarchyDialog = projectHierarchyDialog with
            {
                Error = result.Errors.FirstOrDefault()?.Message ?? "The project hierarchy could not be updated."
            };
            return;
        }

        var selectionNodeId = projectHierarchyDialog.Mode == ProjectStructureProjectHierarchyDialogMode.AddSubproject
            ? BuildProjectChildNodeKey(projectHierarchyDialog.SelectedProjectId.Value)
            : BuildProjectChildNodeKey(projectHierarchyDialog.SubjectProjectId);
        var successMessage = projectHierarchyDialog.Mode switch
        {
            ProjectStructureProjectHierarchyDialogMode.AddSubproject =>
                $"{selectedProject?.Name ?? "The selected project"} is now visible under {projectHierarchyDialog.SubjectProjectTitle}.",
            _ =>
                $"{projectHierarchyDialog.SubjectProjectTitle} now belongs to {selectedProject?.Name ?? "the selected parent project"}."
        };

        projectHierarchyDialog = null;
        await ReloadSurfaceAsync(selectionNodeId);
        workflowFeedback = successMessage;
        workflowFeedbackTone = "mint";
        await InvokeAsync(StateHasChanged);
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
        var subjectProjectId = node.RelatedProjectId ?? ProjectId;
        var availableProjects = (await ProjectsService.ListAsync())
            .Where(project => project.Id != subjectProjectId)
            .OrderBy(project => project.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(project => project.Id)
            .ToList();
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
            availableProjects = availableProjects
                .Where(project => project.Id != currentParentProjectId.Value)
                .ToList();
        }

        projectHierarchyDialog = new ProjectStructureProjectHierarchyDialogState(
            mode,
            subjectProjectId,
            node.Title,
            currentParentProjectId,
            currentParentProjectTitle,
            availableProjects,
            null,
            string.Empty);
        await InvokeAsync(StateHasChanged);
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

    private sealed record ProjectStructureProjectHierarchyDialogState(
        ProjectStructureProjectHierarchyDialogMode Mode,
        Guid SubjectProjectId,
        string SubjectProjectTitle,
        Guid? CurrentParentProjectId,
        string CurrentParentProjectTitle,
        IReadOnlyList<ProjectSummary> AvailableProjects,
        Guid? SelectedProjectId,
        string Error)
    {
        public string Title => Mode switch
        {
            ProjectStructureProjectHierarchyDialogMode.AddSubproject => $"Add subproject under {SubjectProjectTitle}",
            _ => $"Reconnect {SubjectProjectTitle}"
        };

        public string Copy => Mode switch
        {
            ProjectStructureProjectHierarchyDialogMode.AddSubproject =>
                "Choose an existing project to attach beneath the selected project node.",
            _ => $"Choose the new parent project for {SubjectProjectTitle}."
        };

        public string SubmitLabel => Mode switch
        {
            ProjectStructureProjectHierarchyDialogMode.AddSubproject => "Add subproject",
            _ => "Reconnect project"
        };
    }
}
