using CanDoItAll.Components.CanvasLib;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace CanDoItAll.Modules.Workbench.Pages;

public partial class ProjectStructurePage
{
    [Inject]
    private ProjectAssetCreationService AssetCreationService { get; set; } = default!;

    [Inject]
    private ProjectStructureBatchDeletionCoordinator BatchDeletionCoordinator { get; set; } = default!;

    private static readonly IReadOnlyList<string> SummaryStatusOptions =
    [
        "Draft",
        "Planned",
        "In progress",
        "Review",
        "Blocked",
        "Done",
        "N/A"
    ];

    private string selectionBorderName = string.Empty;
    private string? reconnectNodeId;
    private ProjectStructureDeletePrompt? pendingDeletePrompt;
    private string? deleteFailure;
    private ProjectStructureActionContext? deleteActionContext;
    private long deleteSelectionRevision;
    private sealed record RetainedDeletionOutcome(ProjectStructureActionContext Origin,
        ProjectStructureDeletionResult? Result = null, ProjectStructureDeletionBatchRecovery? Recovery = null, Exception? Failure = null);
    private readonly Queue<RetainedDeletionOutcome> retainedDeletionOutcomes = new();
    private ProjectStructureSummaryDialogState? summaryDialog;
    private ProjectStructureTranscriptActionDialogState? pendingTranscriptAction;
    private ProjectStructureNode? mermaidPreviewNode;
    private ProjectStructureAuthoringOpening? mermaidOpening;
    private string? workflowFeedback;
    private string workflowFeedbackTone = "neutral";
    private readonly List<ProjectStructureDeletionRecovery> pendingDeletionRecoveries = [];
    private readonly List<ProjectStructureDeletionCompletionNotice> deletionCompletionNotices = [];
    private bool isRetryingDeletionCleanup;

    private bool CanOpenSelectedSummary
        => selectedNode is not null &&
           surface is not null &&
           ProjectStructureSummaryBuilder.Build(surface, selectedNode).Rows.Count > 1;

    private bool IsReconnectMode => !string.IsNullOrWhiteSpace(reconnectNodeId);
    private bool IsDependencyMode => string.Equals(canvasToolMode, CanvasAuthoringMode.Dependency, StringComparison.Ordinal);
    private bool IsDeleteMode => string.Equals(canvasToolMode, CanvasAuthoringMode.Delete, StringComparison.Ordinal);
    private ProjectStructureNode? MermaidViewerNode => HasMermaidViewer(mermaidPreviewNode) ? mermaidPreviewNode : null;

    private bool CanCreateTranscript(ProjectStructureNode? node)
        => node?.ObjectType == ProjectObjectType.Recording;

    private bool HasTranscriptActions(ProjectStructureNode? node)
        => node?.ObjectType == ProjectObjectType.Transcript;

    private static bool HasMermaidViewer(ProjectStructureNode? node)
    {
        if (node is null || node.ObjectType != ProjectObjectType.File)
        {
            return false;
        }

        if (ProjectStructureNodeHelpers.CanRenderAttachmentPreview(node))
        {
            return false;
        }

        if (HasMermaidObjectSubtype(node.ObjectSubtype))
        {
            return true;
        }

        ProjectObjectMetadataEnvelope metadata;
        try
        {
            metadata = ProjectObjectMetadataSerializer.Parse(node.MetadataJson);
        }
        catch (InvalidOperationException)
        {
            metadata = new ProjectObjectMetadataEnvelope();
        }

        if (metadata.File?.FileSubtype == ProjectFileSubtype.Mermaid ||
            HasMermaidFileExtension(metadata.File?.ExternalPath))
        {
            return true;
        }

        return false;
    }

    private static bool HasMermaidObjectSubtype(string? objectSubtype)
        => string.Equals(objectSubtype, "mermaid", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(objectSubtype, "mmd", StringComparison.OrdinalIgnoreCase);

    private static bool HasMermaidFileExtension(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var extension = Path.GetExtension(path);
        return string.Equals(extension, ".mmd", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".mermaid", StringComparison.OrdinalIgnoreCase);
    }

    private async Task BeginReconnectAsync(string? nodeId = null)
    {
        var context = CaptureActionContext();
        var source = context.Surface.Nodes.FirstOrDefault(node => node.Id == (nodeId ?? selectedNode?.Id));
        if (source is null) {
            return;
        }
        reconnectNodeId = source.Id;
        reconnectOpening = new(context, source);
        await SetCanvasToolModeAsync(CanvasAuthoringMode.Select);
        await InvokeAsync(StateHasChanged);
    }

    private async Task DisconnectNodeAsync(string? nodeId = null, ProjectStructureActionContext? capturedContext = null) {
        var context = capturedContext ?? CaptureActionContext();
        var targetNode = context.Surface.Nodes.FirstOrDefault(node => node.Id == (nodeId ?? selectedNode?.Id));
        if (!IsCurrentAction(context) || targetNode is null) {
            return;
        }
        if (reconnectOpening is { } pending && IsCurrentAction(pending.Context) && (pending.IsBusy || pending.RequiresObservation)) {
            return;
        }
        var opening = new ProjectStructureAuthoringOpening(context, targetNode) { IsBusy = true };
        reconnectOpening = opening;
        reconnectNodeId = null;
        await ReparentBranchAsync(opening, null);
    }

    private ProjectStructureAuthoringOpening? dependencyOpening;
    private ProjectStructureAuthoringOpening? linkOperation;
    private ProjectStructureAuthoringOpening? reconnectOpening;

    private Task DeleteDependencyAsync(string? sourceNodeId, string? targetNodeId, string? linkKind) {
        if (!Enum.TryParse<ProjectObjectLinkKind>(linkKind, ignoreCase: true, out var kind) || !Enum.IsDefined(kind)) {
            return Task.CompletedTask;
        }
        var context = CaptureActionContext();
        var link = context.Surface.Links.FirstOrDefault(link => link.SourceId == sourceNodeId && link.TargetId == targetNodeId && link.Kind == kind);
        var nodes = context.Surface.Nodes.Where(node => node.Id == sourceNodeId || node.Id == targetNodeId).ToArray();
        return link is not { IsUserAuthored: true, RecordId: not null } || nodes.Length != 2 ? Task.CompletedTask :
            ExecuteGraphLinkAsync(context, link, nodes, remove: true);
    }

    private async Task ExecuteGraphLinkAsync(ProjectStructureActionContext context, ProjectStructureLink requested,
        IReadOnlyList<ProjectStructureNode> nodes, bool remove) {
        if (!IsCurrentAction(context) || linkOperation is { } pending && IsCurrentAction(pending.Context) && (pending.IsBusy || pending.RequiresObservation)) {
            return;
        }
        var opening = new ProjectStructureAuthoringOpening(context, nodes[0]) { IsBusy = true };
        var selectionRevision = insightsSelectionRevision;
        var mode = canvasToolMode;
        linkOperation = opening;
        var outcome = new ProjectStructureAuthoringOutcome(opening.Id, Guid.NewGuid(), context.Admission,
            remove ? ProjectStructureAuthoringOperation.DisconnectNodes : ProjectStructureAuthoringOperation.ConnectNodes,
            ProjectStructureAuthoringResultKind.Rejected, "The selected dependency could not be changed.") {
            SourceNodeId = requested.SourceId, Link = requested
        };
        try {
            if (remove) {
                var removed = await ProjectWorkbenchService.UnlinkObjectsAsync(context.Surface.ProjectId, requested.SourceId, requested.TargetId, requested.Kind,
                    mutationOwner: CreateProjectStructureUiAgentContext(context.Surface.ProjectId) with { ExpectedProjectAdmission = context.Admission }, expectedLink: requested);
                if (!removed) {
                    RecordAuthoringOutcome(opening, outcome);
                    return;
                }
            } else {
                var accepted = await ProjectWorkbenchService.LinkObjectsDetailedAsync(context.Admission, requested.SourceId, requested.TargetId, requested.Kind, nodes);
                outcome = outcome with { Link = accepted };
            }
            outcome = outcome with { Kind = ProjectStructureAuthoringResultKind.Committed,
                Message = remove ? "The dependency link was deleted." : "The dependency link was added." };
            RecordAuthoringOutcome(opening, outcome);
            if (IsCurrentAuthoring(opening, linkOperation) && selectionRevision == insightsSelectionRevision) {
                if (canvasToolMode == mode) {
                    await SetCanvasToolModeAsync(CanvasAuthoringMode.Select);
                }
                await RefreshAuthoringSurfaceAsync(opening, outcome, requested.SourceId,
                    () => ReferenceEquals(linkOperation, opening) && selectionRevision == insightsSelectionRevision);
            }
        } catch (Exception failure) {
            opening.RequiresObservation = !IsKnownGraphRejection(failure);
            RecordAuthoringOutcome(opening, outcome with { Failure = failure,
                Kind = opening.RequiresObservation ? ProjectStructureAuthoringResultKind.Unconfirmed : ProjectStructureAuthoringResultKind.Rejected,
                Message = opening.RequiresObservation ? "The dependency change is unconfirmed. Observe the original project before repeating it." : failure.Message });
        } finally {
            opening.IsBusy = false;
            await RenderAuthoringOutcomeAsync();
        }
    }

    private async Task DeleteNodeAsync(string? nodeId = null, ProjectStructureActionContext? capturedContext = null)
    {
        var context = capturedContext ?? CaptureActionContext();
        var targetNode = context.Surface.Nodes.FirstOrDefault(node => node.Id == (nodeId ?? selectedNode?.Id));
        if (!IsCurrentAction(context) || targetNode is null)
        {
            return;
        }

        var prompt = BuildDeletePrompt(targetNode);
        if (!prompt.RequiresConfirmation)
        {
            var deleted = await DeleteSelectedNodesAsync(
                [targetNode],
                ProjectStructureManagedStorageDisposition.DeleteOwnedManagedFiles,
                $"{targetNode.Title} was deleted.",
                "The selected node could not be deleted.", context);
            if (!deleted)
            {
                return;
            }

            return;
        }

        deleteFailure = null;
        deleteActionContext = context;
        deleteSelectionRevision = insightsSelectionRevision;
        pendingDeletePrompt = prompt;
        await InvokeAsync(StateHasChanged);
    }

    private async Task DeleteNodesAsync(IReadOnlyCollection<string> nodeIds, ProjectStructureActionContext? capturedContext = null)
    {
        var context = capturedContext ?? CaptureActionContext();
        if (!IsCurrentAction(context)) {
            return;
        }
        var targetNodes = context.Surface.Nodes.Where(node => nodeIds.Contains(node.Id, StringComparer.Ordinal)).ToArray();
        if (targetNodes.Length == 0)
        {
            return;
        }

        if (targetNodes.Length == 1)
        {
            await DeleteNodeAsync(targetNodes[0].Id, context);
            return;
        }

        deleteActionContext = context;
        deleteSelectionRevision = insightsSelectionRevision;
        deleteFailure = null;
        pendingDeletePrompt = BuildDeletePrompt(targetNodes);
        await InvokeAsync(StateHasChanged);
    }

    private async Task ConfirmDeleteAsync(ProjectStructureDeleteConfirmation confirmation) {
        var deletePrompt = confirmation.Prompt;
        var context = deleteActionContext;
        if (!ReferenceEquals(pendingDeletePrompt, deletePrompt) || context is null ||
            !IsCurrentAction(context) || deleteSelectionRevision != insightsSelectionRevision) {
            return;
        }
        try {
            await InsightsAdmissions.RequireCurrentAsync(context.Admission, deferredCompletionCts.Token);
        } catch (Exception exception) {
            RetainDeletionOutcome(new(context, Failure: exception));
            Logger.LogWarning(exception, "Delete confirmation was not admitted for original project {ProjectId}, lifetime {LifetimeId}.",
                context.Surface.ProjectId, context.Admission.LifetimeId);
            if (ReferenceEquals(pendingDeletePrompt, deletePrompt) && IsCurrentAction(context)) {
                deleteFailure = exception.Message;
                await InvokeAsync(StateHasChanged);
            }
            return;
        }
        if (!ReferenceEquals(pendingDeletePrompt, deletePrompt) || !IsCurrentAction(context) ||
            deleteSelectionRevision != insightsSelectionRevision) {
            return;
        }
        var managedStorageDisposition = confirmation.Disposition;
        var nodeIds = ResolvePendingDeleteNodeIds(deletePrompt);
        var targetNodes = context.Surface.Nodes.Where(node => nodeIds.Contains(node.Id, StringComparer.Ordinal)).ToList();
        pendingDeletePrompt = null;
        deleteFailure = null;
        deleteActionContext = null;
        reconnectNodeId = null;
        var isBulk = targetNodes.Count > 1;
        var hasManagedAttachments = deletePrompt.ManagedAttachmentCount > 0;
        var retainedFiles = managedStorageDisposition ==
            ProjectStructureManagedStorageDisposition.RetainManagedFiles;
        await DeleteSelectedNodesAsync(
            targetNodes,
            managedStorageDisposition,
            !hasManagedAttachments
                ? isBulk
                    ? $"{targetNodes.Count} selected branches were deleted."
                    : "The selected branch was deleted."
                : retainedFiles
                ? isBulk
                    ? $"{targetNodes.Count} selected branches were deleted; their managed files were preserved."
                    : "The selected branch was deleted; its managed files were preserved."
                : isBulk
                    ? $"{targetNodes.Count} selected branches and eligible managed files were deleted."
                    : "The selected branch and its eligible managed files were deleted.",
            isBulk ? "The selected branches could not be deleted." : "The selected branch could not be deleted.", context);
    }

    private void CancelDelete(ProjectStructureDeletePrompt prompt) {
        if (ReferenceEquals(pendingDeletePrompt, prompt)) {
            pendingDeletePrompt = null;
            deleteFailure = null;
            deleteActionContext = null;
        }
    }

    private async Task<bool> DeleteSelectedNodesAsync(
        IReadOnlyList<ProjectStructureNode> targetNodes,
        ProjectStructureManagedStorageDisposition managedStorageDisposition,
        string successMessage,
        string failureMessage,
        ProjectStructureActionContext context)
    {
        var deletedAny = false;
        var failedAny = false;
        string? partialFailureFeedback = null;
        var deletionWarnings = new List<ProjectStructureDeletionWarning>();
        try
        {
            var deletion = await BatchDeletionCoordinator.DeleteNodesAsync(
                context.Surface.ProjectId,
                targetNodes.Select(node => node.Id).ToArray(),
                managedStorageDisposition,
                mutationOwner: CreateProjectStructureUiAgentContext(context.Surface.ProjectId) with { ExpectedProjectAdmission = context.Admission });
            RetainDeletionOutcome(new(context, Result: deletion));
            deletionWarnings.AddRange(deletion.DeletionWarnings);
            deletedAny = deletion.DeletedNodeCount > 0;
        }
        catch (ProjectStructureDeletionBatchPartialCommitException exception)
        {
            RetainDeletionOutcome(new(context, Recovery: exception.Recovery));
            deletedAny = exception.Recovery.CompletedNodeCount > 0 ||
                         exception.Recovery.Recoveries.Count > 0;
            failedAny = true;
            deletionWarnings.AddRange(exception.Recovery.Warnings);
            if (IsCurrentAction(context)) {
                foreach (var recovery in exception.Recovery.Recoveries) {
                    AddOrReplacePendingDeletionRecovery(recovery);
                }
            }

            if (exception.Recovery.BranchFailures.Count > 0)
            {
                var completedText = exception.Recovery.CompletedNodeCount == 1
                    ? "1 node was confirmed deleted."
                    : $"{exception.Recovery.CompletedNodeCount} nodes were confirmed deleted.";
                var failedText = exception.Recovery.BranchFailures.Count == 1
                    ? "1 selected branch requires separate follow-up."
                    : $"{exception.Recovery.BranchFailures.Count} selected branches require separate follow-up.";
                var remediation = string.Join(
                    " ",
                    exception.Recovery.BranchFailures
                        .Select(failure => failure.Remediation)
                        .Distinct(StringComparer.Ordinal));
                partialFailureFeedback = exception.Recovery.CompletedNodeCount > 0
                    ? $"{completedText} {failedText} {remediation}"
                    : $"No additional nodes were confirmed deleted. {failedText} {remediation}";
                failureMessage = partialFailureFeedback;
                foreach (var branchFailure in exception.Recovery.BranchFailures)
                {
                    Logger.LogWarning(
                        "Project structure branch deletion failed. ProjectId={ProjectId} RootNodeId={RootNodeId} FailureKind={FailureKind} BindingId={BindingId} Disposition={Disposition}.",
                        context.Surface.ProjectId,
                        branchFailure.RootNodeId,
                        branchFailure.Kind,
                        branchFailure.BindingId,
                        branchFailure.RequestedDisposition);
                }
            }
        }
        catch (ProjectManagedStorageBindingException exception)
        {
            RetainDeletionOutcome(new(context, Failure: exception));
            failedAny = true;
            failureMessage = managedStorageDisposition ==
                ProjectStructureManagedStorageDisposition.DeleteOwnedManagedFiles
                    ? "The nodes were not deleted because current managed-file ownership could not be verified. Choose Delete node only to preserve the files, or migrate the files into the active workspace first."
                    : failureMessage;
            Logger.LogWarning(
                "Project structure deletion was blocked by managed-storage validation. ProjectId={ProjectId} BindingId={BindingId} Disposition={Disposition}.",
                context.Surface.ProjectId,
                exception.BindingId,
                managedStorageDisposition);
        }
        catch (Exception exception)
        {
            RetainDeletionOutcome(new(context, Failure: exception));
            failureMessage = "No additional deletion result was confirmed. Inspect the original project before retrying.";
            failedAny = true;
            Logger.LogWarning(
                "Project structure deletion failed before durable completion. ProjectId={ProjectId} RootCount={RootCount} Disposition={Disposition} FailureType={FailureType}.",
                context.Surface.ProjectId,
                targetNodes.Count,
                managedStorageDisposition,
                exception.GetType().Name);
        }

        if (!IsCurrentAction(context)) {
            return deletedAny && !failedAny;
        }
        if (failedAny)
        {
            try {
                await ReloadSurfaceAsync();
            } catch (Exception exception) {
                RetainDeletionOutcome(new(context, Failure: exception));
                if (IsCurrentAction(context)) {
                    workflowFeedback = "The original deletion outcome is retained, but the view could not refresh. Reload; do not repeat the deletion.";
                    workflowFeedbackTone = "warn";
                }
                return false;
            }
            if (!IsCurrentAction(context)) {
                return false;
            }
            workflowFeedback = pendingDeletionRecoveries.Count > 0
                ? AppendDeletionWarningFeedback(
                    string.Join(
                        " ",
                        partialFailureFeedback,
                        $"{pendingDeletionRecoveries.Count} deleted branch cleanup operation(s) remain pending. Retry cleanup to finish managed-storage and assignment reconciliation.")
                        .Trim(),
                    deletionWarnings)
                : AppendDeletionWarningFeedback(failureMessage, deletionWarnings);
            workflowFeedbackTone = "warn";
            return false;
        }

        if (deletedAny)
        {
            try {
                await ReloadSurfaceAsync();
            } catch (Exception exception) {
                RetainDeletionOutcome(new(context, Failure: exception));
                if (IsCurrentAction(context)) {
                    workflowFeedback = "The original deletion outcome is retained, but the view could not refresh. Reload; do not repeat the deletion.";
                    workflowFeedbackTone = "warn";
                }
                return false;
            }
            if (!IsCurrentAction(context)) {
                return false;
            }
            if (pendingDeletionRecoveries.Count > 0)
            {
                workflowFeedback =
                    $"The selected branch deletion completed, but {pendingDeletionRecoveries.Count} earlier cleanup operation(s) remain pending.";
                workflowFeedbackTone = "warn";
                return false;
            }

            workflowFeedback = AppendDeletionWarningFeedback(
                successMessage,
                deletionWarnings);
            workflowFeedbackTone = deletionWarnings.Count > 0 ? "warn" : "mint";
            return true;
        }

        workflowFeedback = failureMessage;
        workflowFeedbackTone = "warn";
        await InvokeAsync(StateHasChanged);
        return false;
    }

    private void RetainDeletionOutcome(RetainedDeletionOutcome outcome) {
        retainedDeletionOutcomes.Enqueue(outcome);
        while (retainedDeletionOutcomes.Count > 16) {
            retainedDeletionOutcomes.Dequeue();
        }
    }

    private async Task RetryPendingDeletionCleanupAsync()
    {
        if (pendingDeletionRecoveries.Count == 0 || isRetryingDeletionCleanup)
        {
            return;
        }

        isRetryingDeletionCleanup = true;
        var pending = pendingDeletionRecoveries.ToArray();
        pendingDeletionRecoveries.Clear();
        var deletionWarnings = new List<ProjectStructureDeletionWarning>();
        try
        {
            foreach (var recovery in pending)
            {
                try
                {
                    var deletion = await ProjectWorkbenchService.RetryDeletionCleanupDetailedAsync(
                        recovery.ProjectId,
                        recovery.RootNodeId,
                        recovery.DurableMutationId,
                        deferredCompletionCts.Token);
                    deletionWarnings.AddRange(deletion.DeletionWarnings);
                }
                catch (ProjectStructureDeletionPartialCommitException exception)
                {
                    AddOrReplacePendingDeletionRecovery(exception.Recovery);
                }
                catch (Exception exception)
                {
                    AddOrReplacePendingDeletionRecovery(recovery);
                    Logger.LogWarning(
                        "Project structure durable deletion retry failed. ProjectId={ProjectId} MutationId={MutationId} FailureType={FailureType}.",
                        recovery.ProjectId,
                        recovery.DurableMutationId,
                        exception.GetType().Name);
                }
            }

            await ReloadSurfaceAsync();
            if (pendingDeletionRecoveries.Count == 0)
            {
                workflowFeedback = AppendDeletionWarningFeedback(
                    "Deleted branch cleanup completed.",
                    deletionWarnings);
                workflowFeedbackTone = deletionWarnings.Count > 0 ? "warn" : "mint";
            }
            else
            {
                workflowFeedback =
                    $"{pendingDeletionRecoveries.Count} deleted branch cleanup operation(s) are still pending. Retry cleanup again after resolving the storage or assignment failure.";
                workflowFeedbackTone = "warn";
            }
        }
        finally
        {
            isRetryingDeletionCleanup = false;
        }
    }

    private static string AppendDeletionWarningFeedback(
        string message,
        IReadOnlyCollection<ProjectStructureDeletionWarning> warnings)
    {
        if (warnings.Count == 0)
        {
            return message;
        }

        return $"{message} {string.Join(" ", warnings.Select(warning => $"{warning.Message} {warning.Remediation}"))}";
    }

    private static string BuildDeletionCompletionNoticeFeedback(
        IReadOnlyCollection<ProjectStructureDeletionCompletionNotice> notices)
    {
        return string.Join(" ", notices.SelectMany(notice => notice.Warnings.Select(warning =>
            $"Completed deletion {notice.DurableMutationId:D} for {notice.RootNodeId} retained {warning.RetainedObject.Provider} object {warning.RetainedObject.Locator} on storage {warning.RetainedObject.StorageId?.ToString("D") ?? "bootstrap"}: {warning.RetainedObject.Reason} {warning.Remediation}")));
    }

    private void AddOrReplacePendingDeletionRecovery(
        ProjectStructureDeletionRecovery recovery)
    {
        pendingDeletionRecoveries.RemoveAll(item =>
            item.DurableMutationId == recovery.DurableMutationId);
        pendingDeletionRecoveries.Add(recovery);
    }

    private IReadOnlyList<ProjectStructureNode> ResolveDeleteTargetNodes(IReadOnlyCollection<string> nodeIds)
    {
        if (surface is null || nodeIds.Count == 0)
        {
            return [];
        }

        var nodesById = surface.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        var targets = nodeIds
            .Where(nodeId => !string.IsNullOrWhiteSpace(nodeId))
            .Distinct(StringComparer.Ordinal)
            .Select(nodeId => nodesById.GetValueOrDefault(nodeId))
            .Where(node => node is not null)
            .Cast<ProjectStructureNode>()
            .ToList();
        if (targets.Count < 2)
        {
            return targets;
        }

        var selectedIds = targets
            .Select(node => node.Id)
            .ToHashSet(StringComparer.Ordinal);
        return targets
            .Where(node => !HasSelectedDeleteAncestor(node, selectedIds, nodesById))
            .ToList();
    }

    private static bool HasSelectedDeleteAncestor(
        ProjectStructureNode node,
        IReadOnlySet<string> selectedIds,
        IReadOnlyDictionary<string, ProjectStructureNode> nodesById)
    {
        var visitedIds = new HashSet<string>(StringComparer.Ordinal);
        var parentId = node.ParentId;
        while (!string.IsNullOrWhiteSpace(parentId) && visitedIds.Add(parentId))
        {
            if (selectedIds.Contains(parentId))
            {
                return true;
            }

            parentId = nodesById.TryGetValue(parentId, out var parent)
                ? parent.ParentId
                : null;
        }

        return false;
    }

    private static IReadOnlyList<string> ResolvePendingDeleteNodeIds(ProjectStructureDeletePrompt prompt)
        => prompt.NodeIds.Count > 0 ? prompt.NodeIds : [prompt.NodeId];

    private async Task OpenMermaidViewerAsync(ProjectStructureNode node) {
        if (!HasMermaidViewer(node)) {
            CloseMermaidViewer();
            await OpenAttachmentPreviewAsync(node);
            return;
        }
        var opening = new ProjectStructureAuthoringOpening(CaptureActionContext(), node);
        mermaidOpening = opening;
        mermaidPreviewNode = null;
        await CloseAttachmentPreviewAsync();
        if (IsCurrentAuthoring(opening, mermaidOpening)) {
            mermaidPreviewNode = node;
        }
    }

    private void CloseMermaidViewer() {
        mermaidOpening = null;
        mermaidPreviewNode = null;
    }

    private async Task EditMermaidPreviewNodeAsync(ProjectStructureAuthoringOpening? opening) {
        if (opening is null || !TryBeginAuthoring(opening, mermaidOpening)) {
            return;
        }
        try {
            await ProjectWorkbenchService.RequireContentCurrentAsync(opening.Context.Admission, opening.Node, deferredCompletionCts.Token);
            if (!IsCurrentAuthoring(opening, mermaidOpening)) {
                return;
            }
            CloseMermaidViewer();
            await OpenEditDialogAsync(opening.Node, opening.Context);
        } catch (Exception failure) {
            LogContentFailure(opening, ProjectStructureAuthoringOperation.EditNode, failure);
            ReportActionResult(opening.Context, "The original Mermaid content changed or is unavailable. Reopen it before editing.", "warn");
        } finally {
            opening.IsBusy = false;
        }
    }

    private async Task HandleReconnectSelectionAsync(string? nodeId) {
        var opening = reconnectOpening;
        if (opening is null || string.IsNullOrWhiteSpace(reconnectNodeId) || !TryBeginAuthoring(opening, reconnectOpening)) {
            return;
        }
        var target = surface?.Nodes.FirstOrDefault(node => node.Id == nodeId);
        if (target is null || target.Id == opening.Node.Id) {
            opening.IsBusy = false;
            return;
        }
        await ReparentBranchAsync(opening, target);
    }

    private async Task ReparentBranchAsync(ProjectStructureAuthoringOpening opening, ProjectStructureNode? target) {
        var context = opening.Context;
        var selectionRevision = insightsSelectionRevision;
        var outcome = new ProjectStructureAuthoringOutcome(opening.Id, Guid.NewGuid(), context.Admission,
            target is null ? ProjectStructureAuthoringOperation.DisconnectNodes : ProjectStructureAuthoringOperation.MoveNodes,
            ProjectStructureAuthoringResultKind.Rejected, "The branch could not be reconnected.") {
            SourceNodeId = opening.Node.Id
        };
        try {
            var accepted = await ProjectWorkbenchService.ReparentObjectAsync(context.Surface.ProjectId, opening.Node.Id, target?.Id,
                mutationOwner: CreateProjectStructureUiAgentContext(context.Surface.ProjectId) with { ExpectedProjectAdmission = context.Admission },
                expectedNodes: target is null ? [opening.Node] : [opening.Node, target]);
            if (accepted is not null) {
                outcome = outcome with { Kind = ProjectStructureAuthoringResultKind.Committed, Node = accepted,
                    Message = target is null ? $"{opening.Node.Title} was disconnected." : $"{opening.Node.Title} was reconnected under {target.Title}." };
            }
            RecordAuthoringOutcome(opening, outcome);
            if (accepted is not null && IsCurrentAuthoring(opening, reconnectOpening) && selectionRevision == insightsSelectionRevision) {
                reconnectNodeId = null;
                await RefreshAuthoringSurfaceAsync(opening, outcome, target?.Id ?? opening.Node.Id,
                    () => ReferenceEquals(reconnectOpening, opening) && selectionRevision == insightsSelectionRevision);
            }
        } catch (Exception failure) {
            opening.RequiresObservation = !IsKnownGraphRejection(failure);
            RecordAuthoringOutcome(opening, outcome with { Failure = failure,
                Kind = opening.RequiresObservation ? ProjectStructureAuthoringResultKind.Unconfirmed : ProjectStructureAuthoringResultKind.Rejected,
                Message = opening.RequiresObservation ? "Reconnection is unconfirmed. Observe the original project before repeating it." : failure.Message });
        } finally {
            opening.IsBusy = false;
            await RenderAuthoringOutcomeAsync();
        }
    }

    private async Task<bool> TryAdoptMovedNodesIntoBordersAsync(CanvasWorkbenchNodesMovedEventArgs args)
    {
        if (surface is null || args.Positions.Count == 0)
        {
            return false;
        }

        var uiState = ResolveEditableUiState();
        if (uiState.GroupFrames.Count == 0)
        {
            return false;
        }

        var positionsByNodeId = surface.Nodes.ToDictionary(
            node => node.Id,
            node => (node.X, node.Y),
            StringComparer.Ordinal);
        foreach (var position in args.Positions)
        {
            positionsByNodeId[position.NodeId] = (position.X, position.Y);
        }

        var changed = false;
        foreach (var frame in uiState.GroupFrames)
        {
            var frameBounds = ResolveFrameBounds(frame.AnchorNodeIds, positionsByNodeId);
            if (frameBounds is null)
            {
                continue;
            }

            foreach (var position in args.Positions)
            {
                if (frame.AnchorNodeIds.Contains(position.NodeId, StringComparer.Ordinal))
                {
                    continue;
                }

                if (position.X >= frameBounds.Value.Left &&
                    position.X <= frameBounds.Value.Right &&
                    position.Y >= frameBounds.Value.Top &&
                    position.Y <= frameBounds.Value.Bottom)
                {
                    frame.AnchorNodeIds.Add(position.NodeId);
                    changed = true;
                }
            }
        }

        if (changed)
        {
            await PersistCanvasUiStateAsync(uiState);
        }

        return changed;
    }

    private (double Left, double Top, double Right, double Bottom)? ResolveFrameBounds(
        IReadOnlyCollection<string> anchorNodeIds,
        IReadOnlyDictionary<string, (double X, double Y)> positionsByNodeId)
    {
        var anchors = anchorNodeIds
            .Where(anchorId => positionsByNodeId.ContainsKey(anchorId))
            .Select(anchorId => positionsByNodeId[anchorId])
            .ToList();
        if (anchors.Count == 0)
        {
            return null;
        }

        const double nodeHalfWidth = 130;
        const double nodeHalfHeight = 72;
        const double paddingX = 38;
        const double paddingY = 42;

        var left = anchors.Min(anchor => anchor.X - nodeHalfWidth) - paddingX;
        var right = anchors.Max(anchor => anchor.X + nodeHalfWidth) + paddingX;
        var top = anchors.Min(anchor => anchor.Y - nodeHalfHeight) - paddingY;
        var bottom = anchors.Max(anchor => anchor.Y + nodeHalfHeight) + paddingY;
        return (left, top, right, bottom);
    }

    private ProjectStructureDeletePrompt BuildDeletePrompt(ProjectStructureNode node)
    {
        var affectedNodeIds = CollectDeleteSubtreeNodeIds([node.Id]);
        var descendantCount = Math.Max(0, affectedNodeIds.Count - 1);
        var managedAttachmentCount = CountManagedAttachments(affectedNodeIds);
        var linkedNodeCount = CountLinkedNodes(node.Id);
        var dependencyLinkCount = CountDependencyLinks(node.Id);
        var requiresConfirmation = node.ObjectType != ProjectObjectType.Note ||
                                   descendantCount > 0 ||
                                   linkedNodeCount > 1 ||
                                   managedAttachmentCount > 0 ||
                                   node.ArtifactId.HasValue;
        var impactParts = new List<string>();
        if (descendantCount > 0)
        {
            impactParts.Add($"This will delete {descendantCount + 1} nodes including child items.");
        }
        else
        {
            impactParts.Add("This will delete this node.");
        }

        if (dependencyLinkCount > 0)
        {
            impactParts.Add(
                linkedNodeCount > 0
                    ? $"It also removes {dependencyLinkCount} visible dependency link{(dependencyLinkCount == 1 ? string.Empty : "s")} touching {linkedNodeCount} connected node{(linkedNodeCount == 1 ? string.Empty : "s")}."
                    : $"It also removes {dependencyLinkCount} visible dependency link{(dependencyLinkCount == 1 ? string.Empty : "s")}.");
        }

        AppendManagedAttachmentDeletionImpact(impactParts, managedAttachmentCount);

        var impactCopy = string.Join(" ", impactParts);

        return new ProjectStructureDeletePrompt(
            node.Id,
            node.Title,
            descendantCount,
            requiresConfirmation,
            impactCopy,
            managedAttachmentCount);
    }

    private ProjectStructureDeletePrompt BuildDeletePrompt(IReadOnlyList<ProjectStructureNode> nodes)
    {
        if (nodes.Count == 1)
        {
            return BuildDeletePrompt(nodes[0]);
        }

        var nodeIds = nodes
            .Select(node => node.Id)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var affectedNodeIds = CollectDeleteSubtreeNodeIds(nodeIds);
        var descendantCount = Math.Max(0, affectedNodeIds.Count - nodeIds.Count);
        var managedAttachmentCount = CountManagedAttachments(affectedNodeIds);
        var dependencyLinkCount = CountDependencyLinks(affectedNodeIds);
        var linkedNodeCount = CountLinkedNodes(affectedNodeIds);
        var impactParts = new List<string>
        {
            descendantCount > 0
                ? $"This will delete {nodeIds.Count} selected root nodes and {affectedNodeIds.Count} total nodes including child items."
                : $"This will delete {nodeIds.Count} selected nodes."
        };

        if (dependencyLinkCount > 0)
        {
            impactParts.Add(
                linkedNodeCount > 0
                    ? $"It also removes {dependencyLinkCount} visible dependency link{(dependencyLinkCount == 1 ? string.Empty : "s")} touching {linkedNodeCount} connected node{(linkedNodeCount == 1 ? string.Empty : "s")} outside the selection."
                    : $"It also removes {dependencyLinkCount} visible dependency link{(dependencyLinkCount == 1 ? string.Empty : "s")} inside the selected branches.");
        }

        AppendManagedAttachmentDeletionImpact(impactParts, managedAttachmentCount);

        return new ProjectStructureDeletePrompt(
            nodeIds[0],
            $"{nodeIds.Count} selected nodes",
            descendantCount,
            RequiresConfirmation: true,
            string.Join(" ", impactParts),
            managedAttachmentCount)
        {
            NodeIds = nodeIds
        };
    }

    private int CountDescendants(string nodeId)
    {
        if (surface is null)
        {
            return 0;
        }

        var childrenByParent = surface.Nodes
            .Where(node => !string.IsNullOrWhiteSpace(node.ParentId))
            .GroupBy(node => node.ParentId!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(child => child.Id).ToList(), StringComparer.Ordinal);
        var count = 0;
        var queue = new Queue<string>();
        queue.Enqueue(nodeId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!childrenByParent.TryGetValue(current, out var children))
            {
                continue;
            }

            foreach (var child in children)
            {
                count++;
                queue.Enqueue(child);
            }
        }

        return count;
    }

    private int CountManagedAttachments(IReadOnlySet<string> nodeIds)
    {
        if (surface is null || nodeIds.Count == 0)
        {
            return 0;
        }

        return surface.Nodes.Count(node =>
            nodeIds.Contains(node.Id) &&
            !node.IsSystemManaged &&
            ProjectStructureNodeHelpers.HasManagedAttachment(node));
    }

    private static void AppendManagedAttachmentDeletionImpact(
        ICollection<string> impactParts,
        int managedAttachmentCount)
    {
        if (managedAttachmentCount == 0)
        {
            return;
        }

        impactParts.Add(managedAttachmentCount == 1
            ? "This branch has one managed file attachment. Choose whether to preserve its stored file or request deletion; deletion occurs only when managed storage owns it and no other node references it."
            : $"This branch has {managedAttachmentCount} managed file attachments. Choose whether to preserve their stored files or request deletion; deletion occurs only for files owned by managed storage and not referenced by other nodes.");
    }

    private int CountLinkedNodes(string nodeId)
    {
        if (surface is null)
        {
            return 0;
        }

        return surface.Links
            .Where(link => string.Equals(link.SourceId, nodeId, StringComparison.Ordinal) ||
                           string.Equals(link.TargetId, nodeId, StringComparison.Ordinal))
            .Select(link => string.Equals(link.SourceId, nodeId, StringComparison.Ordinal) ? link.TargetId : link.SourceId)
            .Where(otherNodeId => !string.IsNullOrWhiteSpace(otherNodeId))
            .Distinct(StringComparer.Ordinal)
            .Count();
    }

    private int CountDependencyLinks(string nodeId)
    {
        if (surface is null)
        {
            return 0;
        }

        return surface.Links.Count(link =>
            link.Kind == ProjectObjectLinkKind.DependsOn &&
            (string.Equals(link.SourceId, nodeId, StringComparison.Ordinal) ||
             string.Equals(link.TargetId, nodeId, StringComparison.Ordinal)));
    }

    private HashSet<string> CollectDeleteSubtreeNodeIds(IReadOnlyCollection<string> rootNodeIds)
    {
        var collectedIds = new HashSet<string>(rootNodeIds, StringComparer.Ordinal);
        if (surface is null || rootNodeIds.Count == 0)
        {
            return collectedIds;
        }

        var childrenByParent = surface.Nodes
            .Where(node => !string.IsNullOrWhiteSpace(node.ParentId))
            .GroupBy(node => node.ParentId!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(child => child.Id).ToList(), StringComparer.Ordinal);
        var queue = new Queue<string>(rootNodeIds);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!childrenByParent.TryGetValue(current, out var children))
            {
                continue;
            }

            foreach (var child in children)
            {
                if (collectedIds.Add(child))
                {
                    queue.Enqueue(child);
                }
            }
        }

        return collectedIds;
    }

    private int CountLinkedNodes(IReadOnlySet<string> nodeIds)
    {
        if (surface is null || nodeIds.Count == 0)
        {
            return 0;
        }

        return surface.Links
            .Where(link => nodeIds.Contains(link.SourceId) || nodeIds.Contains(link.TargetId))
            .Select(link => nodeIds.Contains(link.SourceId) ? link.TargetId : link.SourceId)
            .Where(otherNodeId => !string.IsNullOrWhiteSpace(otherNodeId) && !nodeIds.Contains(otherNodeId))
            .Distinct(StringComparer.Ordinal)
            .Count();
    }

    private int CountDependencyLinks(IReadOnlySet<string> nodeIds)
    {
        if (surface is null || nodeIds.Count == 0)
        {
            return 0;
        }

        return surface.Links.Count(link =>
            link.Kind == ProjectObjectLinkKind.DependsOn &&
            (nodeIds.Contains(link.SourceId) || nodeIds.Contains(link.TargetId)));
    }

    private ProjectStructureNode? ResolveNode(string? nodeId)
        => string.IsNullOrWhiteSpace(nodeId)
            ? selectedNode
            : surface?.Nodes.FirstOrDefault(node => string.Equals(node.Id, nodeId, StringComparison.Ordinal));

    private static Guid? TryParseCustomNodeArtifactId(string nodeId)
        => nodeId.StartsWith("custom:", StringComparison.OrdinalIgnoreCase) &&
           Guid.TryParse(nodeId["custom:".Length..], out var parsed)
            ? parsed
            : null;

    private static string BuildTranscriptPrompt(ProjectLlmActionKind actionKind, string title, string transcriptText)
        => actionKind switch
        {
            ProjectLlmActionKind.Summarize =>
                $"Summarize the transcript '{title}'. Focus on decisions, risks, follow-ups, and unresolved questions.\n\nTranscript:\n{transcriptText}",
            ProjectLlmActionKind.FindMyTasks =>
                $"Read the transcript '{title}' and extract only tasks, commitments, or follow-ups assigned to the requester. Use a concise markdown checklist.\n\nTranscript:\n{transcriptText}",
            _ =>
                $"Read the transcript '{title}' and extract only deliveries, promises, or handoffs that other people owe to the requester. Use a concise markdown checklist.\n\nTranscript:\n{transcriptText}"
        };

    private static string ResolveTranscriptActionLabel(ProjectLlmActionKind actionKind)
        => actionKind switch
        {
            ProjectLlmActionKind.FindMyTasks => "Find my tasks",
            ProjectLlmActionKind.FindOthersDeliveries => "Find others delivery to me",
            _ => "Summarize"
        };

    private static string SanitizeExportName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "project-structure";
        }

        string displayName = string.Join(
            "-",
            value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .ToLowerInvariant();
        return PortablePhysicalFileNamePolicy.Encode(displayName).PhysicalName;
    }

}
