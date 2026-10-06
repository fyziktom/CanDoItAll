using CanDoItAll.SharedKernel;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench.CanvasAdapters;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workbench.Pages;

public partial class ProjectStructurePage {
    private long actionNavigationRevision;
    private ProjectStructureActionContext? secretReferenceActionContext;
    private ProjectStructureAuthoringOpening? previewOpening;
    private readonly Queue<ProjectStructureAuthoringOutcome> authoringOutcomes = new();
    private readonly HashSet<Task> authoringOperations = [];

    internal IReadOnlyList<ProjectStructureAuthoringOutcome> AuthoringOutcomes => authoringOutcomes.ToArray();

    private sealed class ProjectStructureAuthoringOpening(ProjectStructureActionContext context, ProjectStructureNode node, Guid? id = null) {
        public Guid Id { get; } = id ?? Guid.NewGuid();
        public ProjectStructureActionContext Context { get; } = context;
        public ProjectStructureNode Node { get; } = node;
        public bool IsBusy { get; set; }
        public bool RequiresObservation { get; set; }
    }

    private bool IsCurrentAuthoring(ProjectStructureAuthoringOpening opening, ProjectStructureAuthoringOpening? current)
        => ReferenceEquals(opening, current) && IsCurrentAction(opening.Context);

    private async Task DispatchAuthoringDialogAsync(ProjectStructureAuthoringOpening? receiver,
        ProjectStructureAuthoringOpening? current, Func<Task> action) {
        if (receiver is null || !IsCurrentAuthoring(receiver, current)) {
            return;
        }
        await TrackAuthoringOperationAsync(action());
    }

    private async Task TrackAuthoringOperationAsync(Task operation) {
        authoringOperations.Add(operation);
        try {
            await operation;
        } finally {
            authoringOperations.Remove(operation);
        }
    }

    private static bool IsKnownGraphRejection(Exception failure)
        => failure is ArgumentException or InvalidDataException or ProjectWriteAdmissionRejectedException or
            ProjectStructureClipboardMutationInputException or ProjectStructureEditConflictException;

    private void ChangeAuthoringDialog(ProjectStructureAuthoringOpening? receiver,
        ProjectStructureAuthoringOpening? current, Action action) {
        if (receiver is not null && IsCurrentAuthoring(receiver, current)) {
            action();
        }
    }

    private bool TryBeginAuthoring(ProjectStructureAuthoringOpening opening, ProjectStructureAuthoringOpening? current) {
        if (!IsCurrentAuthoring(opening, current) || opening.IsBusy || opening.RequiresObservation) {
            return false;
        }
        opening.IsBusy = true;
        return true;
    }

    private void RecordAuthoringOutcome(ProjectStructureAuthoringOpening opening, ProjectStructureAuthoringOutcome outcome) {
        authoringOutcomes.Enqueue(outcome);
        while (authoringOutcomes.Count > 16) {
            authoringOutcomes.Dequeue();
        }
        if (outcome.Failure is { } failure) {
            Logger.LogWarning(failure, "Structure {Operation} for project {ProjectId}, lifetime {LifetimeId}, opening {OpeningId}, submission {SubmissionId}: {Outcome}.",
                outcome.Operation, outcome.Project.ProjectId, outcome.Project.LifetimeId, outcome.OpeningId, outcome.SubmissionId, outcome.Kind);
        }
        if (!deferredCompletionCts.IsCancellationRequested && ReferenceEquals(opening.Context.Actor, InsightsAuthentication) &&
            surface?.ExpectedProjectAdmission?.DatabaseProfileId == outcome.Project.DatabaseProfileId) {
            ReportActionResult(opening.Context, outcome.Message,
                outcome.Kind == ProjectStructureAuthoringResultKind.Committed ? "mint" : "warn");
        }
    }

    private Task RenderAuthoringOutcomeAsync()
        => deferredCompletionCts.IsCancellationRequested ? Task.CompletedTask : InvokeAsync(StateHasChanged);

    private void RetireInactiveAuthoring() {
        RetireImageAuthorities();
        if (mermaidOpening is { } mermaid && !IsCurrentAction(mermaid.Context)) {
            CloseMermaidViewer();
        }
        if (summaryOpening is { } summary && !IsCurrentAction(summary.Context)) {
            CloseSummary();
        }
        if (transcriptOpening is { } transcript && !IsCurrentAction(transcript.Context)) {
            CancelTranscriptAction();
        }
        if (textAssetOpening is { } text && !IsCurrentAction(text.Context)) {
            textAssetOpening = null;
        }
        foreach (var retired in composerOpenings.Where(entry => !IsCurrentAction(entry.Value.Ownership.Context)).Select(entry => entry.Key).ToArray()) {
            composerOpenings.Remove(retired);
        }
        if (composerOpening is { } composer && !IsCurrentAction(composer.Ownership.Context)) {
            composerOpening = null;
        }
        if (hierarchyOpening is { } hierarchy && !IsCurrentAction(hierarchy.Context)) {
            CloseProjectHierarchyDialog();
        }
        if (blockOpening is { } block && !IsCurrentAction(block.Context)) {
            CloseBlockMutationDialog();
        }
        if (transferOpening is { } transfer && !IsCurrentAction(transfer.Context)) {
            CloseSubprojectTransferDialog();
        }
        if (projectCreateOpening is { } creation && (!IsCurrentAction(creation.Context) || !ReferenceEquals(projectCreateParentOpening, hierarchyOpening))) {
            projectCreateOpening = null;
            projectHierarchyDialogBeforeCreate = null;
            showProjectCreateModal = false;
        }
    }

    private async Task RefreshAuthoringSurfaceAsync(ProjectStructureAuthoringOpening opening,
        ProjectStructureAuthoringOutcome outcome, string selection, Func<bool> canPublish) {
        try {
            await ReloadSurfaceAsync(selection, () => IsCurrentAction(opening.Context) && canPublish());
        } catch (Exception exception) {
            RecordAuthoringOutcome(opening, outcome with {
                Message = $"{outcome.Message} The accepted change could not be refreshed. Reload the original project; do not repeat the write.",
                Failure = exception
            });
        }
    }

    private sealed record ProjectStructureActionContext(
        ProjectStructureSurface Surface,
        ProjectWriteAdmission Admission,
        long NavigationRevision,
        Task<Microsoft.AspNetCore.Components.Authorization.AuthenticationState>? Actor) {
        public long RuntimeGeneration { get; init; }
    }

    private ProjectStructureActionContext CaptureActionContext() {
        var displayed = surface ?? throw new InvalidOperationException("The project surface is no longer available. Reload it before continuing.");
        var admission = displayed.ExpectedProjectAdmission
            ?? throw new InvalidOperationException("The original project lifetime is unavailable. Reload the project before continuing.");
        return new(displayed, admission, actionNavigationRevision, InsightsAuthentication) { RuntimeGeneration = ContentDatabase.Generation };
    }

    private ProjectStructureActionContext? CaptureActionContext(string actionId, ProjectStructureNode? node) {
        var retainsTarget = actionId is "summary" or "export-image" or "transcript:create" or "transcript:summarize"
            or "transcript:find-my-tasks" or "transcript:find-others-deliveries" ||
            IsSecretReferenceCreateAction(actionId) ||
            (actionId == ProjectStructureActionCatalogAdapter.EditActionId && node?.ObjectType == ProjectObjectType.SecretReference);
        return retainsTarget ? CaptureActionContext() : null;
    }

    private bool IsCurrentAction(ProjectStructureActionContext context)
        => !deferredCompletionCts.IsCancellationRequested &&
           context.NavigationRevision == actionNavigationRevision &&
           ReferenceEquals(context.Actor, InsightsAuthentication) &&
           ProjectId == context.Surface.ProjectId &&
           surface?.ExpectedProjectAdmission == context.Admission;

    private string ReportActionResult(ProjectStructureActionContext context, string message, string tone = "mint") {
        var feedback = IsCurrentAction(context)
            ? message
            : $"{message} Original project: {context.Surface.ProjectName} ({context.Surface.ProjectId:D}).";
        workflowFeedback = feedback;
        workflowFeedbackTone = tone;
        return feedback;
    }

    private string ReportActionFailure(ProjectStructureActionContext context, Exception exception, string? completed = null) {
        Logger.LogWarning(exception, "Structure action for original project {ProjectId} did not finish.", context.Surface.ProjectId);
        return ReportActionResult(context,
            completed is null ? exception.Message : $"{completed} The remaining action did not finish: {exception.Message}",
            "warn");
    }

    private async Task RefreshCreatedActionAsync(ProjectStructureActionContext context, ProjectStructureNode created) {
        if (IsCurrentAction(context)) {
            await ReloadSurfaceAsync(created.Id);
        }
    }

}
