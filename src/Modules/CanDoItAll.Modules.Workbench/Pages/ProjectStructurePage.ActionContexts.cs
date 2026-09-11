using CanDoItAll.SharedKernel;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench.CanvasAdapters;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workbench.Pages;

public partial class ProjectStructurePage {
    private long actionNavigationRevision;
    private ProjectStructureActionContext? summaryActionContext;
    private ProjectStructureActionContext? transcriptActionContext;
    private ProjectStructureActionContext? secretReferenceActionContext;

    private sealed record ProjectStructureActionContext(
        ProjectStructureSurface Surface,
        ProjectWriteAdmission Admission,
        long NavigationRevision);

    private ProjectStructureActionContext CaptureActionContext() {
        var displayed = surface ?? throw new InvalidOperationException("The project surface is no longer available. Reload it before continuing.");
        var admission = displayed.ExpectedProjectAdmission
            ?? throw new InvalidOperationException("The original project lifetime is unavailable. Reload the project before continuing.");
        return new(displayed, admission, actionNavigationRevision);
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

    private async Task RefreshSummaryActionAsync(ProjectStructureActionContext context, ProjectStructureNode created, string rootNodeId) {
        if (!ReferenceEquals(summaryActionContext, context)) {
            return;
        }
        await RefreshCreatedActionAsync(context, created);
        if (IsCurrentAction(context) && ReferenceEquals(summaryActionContext, context)) {
            await OpenSummaryAsync(rootNodeId);
        }
    }
}
