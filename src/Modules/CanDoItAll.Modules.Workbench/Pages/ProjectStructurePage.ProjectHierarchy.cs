using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Projects.Pages;
using CanDoItAll.Modules.Projects.Pages.Components;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.Workbench.Pages;

public partial class ProjectStructurePage
{
    private const string ProjectChildNodePrefix = "project-child:";

    private ProjectStructureProjectHierarchyDialogState? projectHierarchyDialog;
    private ProjectStructureAuthoringOpening? hierarchyOpening;
    [Inject] private ProjectNativeLifecycleOperations NativeHierarchy { get; set; } = default!;
    [Inject] private IProjectLifecyclePendingStore HierarchyPendingStore { get; set; } = default!;
    [Inject] private IProjectLifecycleRecoveryCodec HierarchyCodec { get; set; } = default!;
    [Inject] private ProjectLifecyclePendingStoreLimits HierarchyPendingLimits { get; set; } = default!;

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
        if (dialog is null || opening is null || dialog.IsBusy || dialog.RequiresObservation ||
                dialog.LifecycleContext is not { } context || !IsCurrentAuthoring(opening, hierarchyOpening)) {
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
        ProjectLifecyclePendingSession? pending = null;
        ProjectLifecyclePendingRecord? reconciledRecord = null;
        var apply = false;
        try {
            ProjectLifecycleCommand command = dialog.Mode switch {
                ProjectStructureProjectHierarchyDialogMode.AddSubproject => new ProjectLifecycleCommand.EnsureLink(
                    dialog.Admissions[dialog.SubjectProjectId], dialog.Admissions[selectedId]),
                ProjectStructureProjectHierarchyDialogMode.ReconnectSubproject when dialog.OriginalEdge is { } edge =>
                    new ProjectLifecycleCommand.ReconnectLink(edge, dialog.Admissions[selectedId]),
                _ => throw new InvalidOperationException("The original hierarchy edge was not captured for this opening.")
            };
            var frozen = NativeHierarchy.Freeze(command, ProjectNativeLifecycleOperations.Scope(context), context, submissionId);
            pending = new(HierarchyPendingStore, HierarchyCodec, frozen);
            await ProjectLifecyclePendingSession.RequireUnblockedAsync(HierarchyPendingStore,
                ProjectNativeLifecycleOperations.Scope(context), HierarchyPendingLimits,
                ProjectLifecyclePendingTargets.Capture(command).ToArray(), deferredCompletionCts.Token);
            if (!IsCurrentAuthoring(opening, hierarchyOpening)) {
                return;
            }
            await pending.PrepareAsync(deferredCompletionCts.Token);
            if (!IsCurrentAuthoring(opening, hierarchyOpening)) {
                return;
            }
            await pending.DispatchAsync(context, DateTimeOffset.UtcNow, false, NativeHierarchy.DispatchAsync, deferredCompletionCts.Token);
            opening.RequiresObservation = true;
            if (pending.KnownAccepted is not null) {
                var acceptedChange = dialog.Mode == ProjectStructureProjectHierarchyDialogMode.AddSubproject
                    ? $"The link from {dialog.SubjectProjectTitle} to {selected.Name} was accepted."
                    : $"The move of {dialog.SubjectProjectTitle} to {selected.Name} was accepted.";
                outcome = outcome with { Kind = ProjectStructureAuthoringResultKind.Committed,
                    Message = $"{acceptedChange} Original operation: {submissionId:D}." };
                apply = pending.CanReconcile && IsCurrentAuthoring(opening, hierarchyOpening);
                if (apply) {
                    pending.Reconcile(_ => opening.RequiresObservation = false);
                    reconciledRecord = pending.Record;
                }
            } else {
                outcome = outcome with { Message = $"The hierarchy operation {submissionId:D} remains unresolved. Check its original outcome in Projects recovery before another write." };
            }
        } catch (Exception exception) {
            opening.RequiresObservation = true;
            outcome = outcome with { Failure = exception,
                Message = $"Hierarchy operation {submissionId:D} could not finish safely. {exception.Message} Review the original reference in Projects recovery." };
        } finally {
            pending?.Dispose();
            opening.IsBusy = false;
        }
        RecordAuthoringOutcome(opening, outcome);
        if (IsCurrentAuthoring(opening, hierarchyOpening)) {
            if (apply) {
                projectHierarchyDialog = null;
                var selection = BuildProjectChildNodeKey(dialog.Mode == ProjectStructureProjectHierarchyDialogMode.AddSubproject
                    ? selectedId : dialog.SubjectProjectId);
                var refreshed = await RefreshAuthoringSurfaceAsync(opening, outcome, selection, () => ReferenceEquals(opening, hierarchyOpening));
                if (refreshed && reconciledRecord is not null) {
                    try {
                        var cleared = await HierarchyPendingStore.ClearResolvedAsync(reconciledRecord, deferredCompletionCts.Token);
                        if (cleared.Status != ProjectPendingStoreStatus.Applied || cleared.Record is not null) {
                            throw new InvalidOperationException("The retained hierarchy evidence changed before acknowledgement cleanup.");
                        }
                    } catch (Exception exception) {
                        RecordAuthoringOutcome(opening, outcome with { Failure = exception,
                            Message = $"{outcome.Message} Its recovery record could not be cleared. Review this operation in Projects recovery before another hierarchy write." });
                    }
                }
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
        if (!IsCurrentAction(opening.Context)) {
            ReportActionResult(opening.Context, "The displayed workspace changed. Reload the project before changing its hierarchy.", "warn");
            await RenderAuthoringOutcomeAsync();
            return;
        }
        hierarchyOpening = opening;
        RetireInactiveAuthoring();
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
        Guid? selectedProjectId,
        ProjectStructureProjectHierarchyDialogState? original = null)
    {
        var before = await NativeHierarchy.GetContextAsync(deferredCompletionCts.Token);
        var context = original?.LifecycleContext ?? before;
        if (context.DatabaseProfileId != opening.Context.Admission.DatabaseProfileId ||
                ProjectNativeLifecycleOperations.Scope(context) != opening.Context.LifecycleScope ||
                ProjectNativeLifecycleOperations.Scope(context) != ProjectNativeLifecycleOperations.Scope(before)) {
            throw new InvalidOperationException("The hierarchy opening belongs to another original workspace.");
        }
        var projects = await ProjectsService.ListAsync();
        var hierarchyLinks = await ProjectsService.ListHierarchyLinksAsync();
        var admissions = projects.Where(project => project.ExpectedProjectAdmission is not null)
            .ToDictionary(project => project.Id, project => project.ExpectedProjectAdmission!);
        foreach (var admission in opening.Context.Surface.HierarchyAdmissions.Values.Append(opening.Context.Admission)) {
            admissions[admission.ProjectId] = admission;
        }
        if (original is not null) {
            foreach (var admission in original.Admissions.Values) {
                admissions[admission.ProjectId] = admission;
            }
        }
        var edge = original?.OriginalEdge;
        if (mode == ProjectStructureProjectHierarchyDialogMode.ReconnectSubproject && edge is null) {
            edge = await NativeHierarchy.ReadEdgeAsync(context, admissions[currentParentProjectId!.Value],
                admissions[subjectProjectId], deferredCompletionCts.Token);
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

        var after = await NativeHierarchy.GetContextAsync(deferredCompletionCts.Token);
        if (ProjectNativeLifecycleOperations.Scope(context) != ProjectNativeLifecycleOperations.Scope(after)) {
            throw new InvalidOperationException("The workspace changed while the original hierarchy was being captured.");
        }

        return new ProjectStructureProjectHierarchyDialogState(
            mode,
            subjectProjectId,
            subjectProjectTitle,
            currentParentProjectId,
            currentParentProjectTitle,
            availableProjects,
            selectedProjectId,
            string.Empty) { OpeningId = opening.Id, Admissions = admissions, LifecycleContext = context, OriginalEdge = edge };
    }

    private async Task<ProjectStructureProjectHierarchyDialogState> RefreshProjectHierarchyDialogAsync(
        ProjectStructureAuthoringOpening opening,
        ProjectStructureProjectHierarchyDialogState dialogState,
        Guid? selectedProjectId = null) {
        return await BuildProjectHierarchyDialogStateAsync(opening,
            dialogState.Mode, dialogState.SubjectProjectId, dialogState.SubjectProjectTitle,
            dialogState.CurrentParentProjectId, dialogState.CurrentParentProjectTitle,
            selectedProjectId ?? dialogState.SelectedProjectId, dialogState);
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
