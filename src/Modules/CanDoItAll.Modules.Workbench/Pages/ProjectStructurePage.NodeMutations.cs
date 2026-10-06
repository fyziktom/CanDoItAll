using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workbench.Pages;

public partial class ProjectStructurePage
{
    private ProjectStructureBlockMutationDialogState? blockMutationDialog;
    private ProjectStructureSubprojectTransferDialogState? subprojectTransferDialog;
    private ProjectStructureAuthoringOpening? blockOpening;
    private ProjectStructureAuthoringOpening? transferOpening;

    private Task OpenChangeBlockTypeDialogAsync(ProjectStructureNode node)
        => OpenBlockMutationDialogAsync(node, ProjectStructureBlockMutationDialogMode.ChangeBlockType);

    private Task OpenNoteConversionDialogAsync(ProjectStructureNode node)
        => OpenBlockMutationDialogAsync(node, ProjectStructureBlockMutationDialogMode.ConvertNoteToBlock);

    private async Task<bool> TryHandleNodeMutationActionAsync(string actionId, string? nodeId)
    {
        var targetNode = ResolveNode(nodeId);
        if (targetNode is null)
        {
            return false;
        }

        switch (actionId)
        {
            case "block:change-type":
                await OpenChangeBlockTypeDialogAsync(targetNode);
                return true;
            case "note:convert-to-block":
                await OpenNoteConversionDialogAsync(targetNode);
                return true;
            case "move-descendants-to-subproject":
                await OpenMoveDescendantsToSubprojectDialogAsync(targetNode);
                return true;
            default:
                return false;
        }
    }

    private async Task OpenBlockMutationDialogAsync(ProjectStructureNode node, ProjectStructureBlockMutationDialogMode mode)
    {
        var options = mode == ProjectStructureBlockMutationDialogMode.ChangeBlockType
            ? ProjectStructureCanvasCatalog.BuildCommonBlockTypeOptions()
            : ProjectStructureCanvasCatalog.BuildNoteConversionOptions();
        if (options.Count == 0)
        {
            workflowFeedback = mode == ProjectStructureBlockMutationDialogMode.ChangeBlockType
                ? "Common block types are not available on this canvas."
                : "Conversion targets are not available on this canvas.";
            workflowFeedbackTone = "warn";
            await InvokeAsync(StateHasChanged);
            return;
        }

        var selectedActionId = mode == ProjectStructureBlockMutationDialogMode.ChangeBlockType
            ? options.FirstOrDefault(option => string.Equals(option.ObjectSubtype, node.ObjectSubtype, StringComparison.OrdinalIgnoreCase))?.ActionId ?? options[0].ActionId
            : options[0].ActionId;
        blockOpening = new(CaptureActionContext(), node);
        blockMutationDialog = new ProjectStructureBlockMutationDialogState(
            mode,
            node.Id,
            node.Title,
            options,
            selectedActionId,
            string.Empty) { OpeningId = blockOpening.Id };
        await InvokeAsync(StateHasChanged);
    }

    private void CloseBlockMutationDialog() {
        blockOpening = null;
        blockMutationDialog = null;
    }

    private void HandleBlockMutationSelectionChanged(ChangeEventArgs args)
    {
        if (blockMutationDialog is null || blockMutationDialog.IsBusy || blockMutationDialog.RequiresObservation)
        {
            return;
        }

        blockMutationDialog = blockMutationDialog with
        {
            SelectedActionId = args.Value?.ToString()?.Trim() ?? string.Empty,
            Error = string.Empty
        };
    }

    private async Task ExecuteBlockMutationAsync() {
        var dialog = blockMutationDialog;
        var opening = blockOpening;
        if (dialog is null || opening is null || !IsCurrentAuthoring(opening, blockOpening)) {
            return;
        }
        if (!dialog.Options.Any(option => option.ActionId == dialog.SelectedActionId) ||
            !ProjectStructureCanvasCatalog.TryResolveCreateDefinition(dialog.SelectedActionId, out var definition)) {
            blockMutationDialog = dialog with { Error = "Choose a block type before continuing." };
            return;
        }
        if (!TryBeginAuthoring(opening, blockOpening)) {
            return;
        }
        blockMutationDialog = dialog with { IsBusy = true, Error = string.Empty };
        var node = opening.Node;
        var wasBlockTypeChange = dialog.Mode == ProjectStructureBlockMutationDialogMode.ChangeBlockType;
        var notes = string.IsNullOrWhiteSpace(node.Notes) ? node.Title : node.Notes;
        var request = new ProjectObjectReclassificationRequest(
            wasBlockTypeChange ? ProjectObjectType.ProjectBlock : definition.ObjectType,
            definition.ObjectSubtype,
            wasBlockTypeChange ? node.Title : ProjectStructureNodeHelpers.BuildSimpleNoteTitle(notes),
            wasBlockTypeChange ? node.Subtitle : string.Empty,
            wasBlockTypeChange ? node.Notes : notes) {
                ExpectedProjectAdmission = opening.Context.Admission,
                ExpectedObjectType = node.ObjectType,
                ExpectedObjectSubtype = node.ObjectSubtype
            };
        var outcome = new ProjectStructureAuthoringOutcome(opening.Id, Guid.NewGuid(), opening.Context.Admission,
            ProjectStructureAuthoringOperation.ConvertNode, ProjectStructureAuthoringResultKind.Unconfirmed,
            $"The conversion result for node {node.Id} was not confirmed. Inspect the original node before retrying.") { SourceNodeId = node.Id };
        try {
            var updated = await ProjectWorkbenchService.ReclassifyObjectAsync(opening.Context.Surface.ProjectId, node.Id, request);
            outcome = updated is null
                ? outcome with { Kind = ProjectStructureAuthoringResultKind.Rejected, Message = "The original node is unavailable or can no longer be changed to the requested type." }
                : outcome with { Kind = ProjectStructureAuthoringResultKind.Committed, Node = updated,
                    Message = $"{updated.Title} was {(wasBlockTypeChange ? "changed" : "converted")} to {ProjectStructureCanvasCatalog.ResolveNodeLabel(updated).ToLowerInvariant()}." };
        } catch (ProjectWriteAdmissionRejectedException exception) {
            outcome = outcome with { Kind = ProjectStructureAuthoringResultKind.Rejected, Message = exception.Message, Failure = exception };
        } catch (Exception exception) {
            opening.RequiresObservation = true;
            outcome = outcome with { Failure = exception };
        } finally {
            opening.IsBusy = false;
        }
        RecordAuthoringOutcome(opening, outcome);
        if (IsCurrentAuthoring(opening, blockOpening)) {
            if (outcome.Kind == ProjectStructureAuthoringResultKind.Committed) {
                blockMutationDialog = null;
                try {
                    await ApplySurfaceNodeUpdatesAsync([outcome.Node!]);
                } catch (Exception failure) {
                    RecordAuthoringOutcome(opening, outcome with { Failure = failure,
                        Message = $"{outcome.Message} The accepted change could not be displayed. Reload the original project; do not repeat the conversion." });
                }
                if (ReferenceEquals(opening, blockOpening)) {
                    blockOpening = null;
                }
            } else {
                blockMutationDialog = dialog with { Error = outcome.Message, RequiresObservation = opening.RequiresObservation };
            }
        }
        await RenderAuthoringOutcomeAsync();
    }

    private async Task OpenMoveDescendantsToSubprojectDialogAsync(ProjectStructureNode node)
    {
        var descendantCount = CountSubtreeDescendants(node.Id, IsUserAuthoredCanvasNode);
        if (descendantCount == 0)
        {
            workflowFeedback = "This node does not have user-authored descendants to move into a subproject.";
            workflowFeedbackTone = "warn";
            await InvokeAsync(StateHasChanged);
            return;
        }

        transferOpening = new(CaptureActionContext(), node);
        subprojectTransferDialog = new ProjectStructureSubprojectTransferDialogState(
            node.Id,
            node.Title,
            descendantCount,
            $"{node.Title} subproject",
            string.Empty) { OpeningId = transferOpening.Id };
        await InvokeAsync(StateHasChanged);
    }

    private void CloseSubprojectTransferDialog() {
        transferOpening = null;
        subprojectTransferDialog = null;
    }

    private void HandleSubprojectTransferNameChanged(ChangeEventArgs args)
    {
        if (subprojectTransferDialog is null || subprojectTransferDialog.IsBusy || subprojectTransferDialog.RequiresObservation)
        {
            return;
        }

        subprojectTransferDialog = subprojectTransferDialog with
        {
            ProjectName = args.Value?.ToString() ?? string.Empty,
            Error = string.Empty
        };
    }

    private async Task ExecuteSubprojectTransferAsync() {
        var dialog = subprojectTransferDialog;
        var opening = transferOpening;
        if (dialog is null || opening is null || !IsCurrentAuthoring(opening, transferOpening)) {
            return;
        }
        var projectName = dialog.ProjectName.Trim();
        if (string.IsNullOrWhiteSpace(projectName)) {
            subprojectTransferDialog = dialog with { Error = "Enter a subproject name before continuing." };
            return;
        }
        if (!TryBeginAuthoring(opening, transferOpening)) {
            return;
        }
        subprojectTransferDialog = dialog with { IsBusy = true, Error = string.Empty };
        var source = opening.Context;
        var sourceNode = opening.Node;
        var targetId = Guid.NewGuid();
        var submissionId = Guid.NewGuid();
        var outcome = new ProjectStructureAuthoringOutcome(opening.Id, submissionId, source.Admission,
            ProjectStructureAuthoringOperation.TransferDescendants, ProjectStructureAuthoringResultKind.Unconfirmed,
            $"The transfer result for target project {targetId:D} was not confirmed. Inspect that target and the original source before retrying.") {
                TargetProjectId = targetId,
                SourceNodeId = sourceNode.Id
            };
        try {
            var reservation = await InsightsAdmissions.ReserveCreationAsync(targetId, opening.Id, submissionId,
                source.Surface.ProjectId, expectedParentAdmission: source.Admission);
            var sourceProject = await ProjectsService.GetAsync(source.Surface.ProjectId);
            var editor = await ProjectsService.GetAsync(null);
            editor.Name = projectName;
            editor.Description = $"Extracted from {sourceNode.Title} in {source.Surface.ProjectName}.";
            editor.Objective = string.IsNullOrWhiteSpace(sourceNode.Notes) ? sourceNode.Title : ProjectStructureNodeHelpers.BuildSimpleNoteTitle(sourceNode.Notes);
            editor.CurrentPhase = string.IsNullOrWhiteSpace(sourceProject.CurrentPhase) ? "Discovery" : sourceProject.CurrentPhase;
            editor.Status = sourceProject.Status;
            var owner = CreateProjectStructureUiAgentContext(source.Surface.ProjectId) with { ExpectedProjectAdmission = source.Admission };
            var result = await SubprojectTransferCoordinator.MoveDescendantsToNewSubprojectAsync(
                source.Surface.ProjectId, reservation, editor, sourceNode.Id, mutationOwner: owner);
            outcome = outcome with { Kind = ProjectStructureAuthoringResultKind.Committed, Transfer = result, Creation = result.CreationReceipt,
                Message = result.Transfer.MovedNodeCount == 1
                    ? $"Created {projectName} and moved 1 descendant into it."
                    : $"Created {projectName} and moved {result.Transfer.MovedNodeCount} descendants into it." };
        } catch (ProjectWriteAdmissionRejectedException exception) {
            outcome = outcome with { Kind = ProjectStructureAuthoringResultKind.Rejected, Message = exception.Message, Failure = exception };
        } catch (ProjectStructureCompensatedSubprojectTransferException exception) {
            outcome = outcome with { Kind = ProjectStructureAuthoringResultKind.Compensated, Failure = exception, Creation = exception.CreationReceipt,
                TargetProjectId = exception.RemovedProjectId,
                Message = "The transfer failed. The empty child project was removed, and the source structure was left unchanged." };
        } catch (ProjectStructureTransferPartialCommitException exception) {
            outcome = outcome with { Kind = ProjectStructureAuthoringResultKind.PartialCommit, Failure = exception, Creation = exception.CreationReceipt,
                TargetProjectId = exception.Recovery.TargetProjectId,
                Message = $"Created {projectName} and moved its descendants, but assignment reconciliation still requires recovery. {exception.Recovery.RetryGuidance}" };
        } catch (ProjectStructureProjectCreationRejectedException exception) {
            outcome = outcome with { Kind = ProjectStructureAuthoringResultKind.Rejected, Message = exception.Message, Failure = exception };
        } catch (ProjectStructureTransferRejectedException exception) {
            outcome = outcome with { Kind = ProjectStructureAuthoringResultKind.Rejected, Failure = exception,
                Message = exception.Reason == ProjectStructureTransferRejectionReason.TargetProjectMismatch
                    ? "The subproject transfer returned inconsistent target information. Review the logs before retrying."
                    : exception.Message };
        } catch (ProjectStructureAgentException exception) when (exception.Details is ProjectCreationPartialCompletion partial) {
            opening.RequiresObservation = true;
            outcome = outcome with { Failure = exception, Message = exception.SafeMessage, Creation = partial.CreationReceipt };
        } catch (Exception exception) {
            opening.RequiresObservation = true;
            outcome = outcome with { Failure = exception };
        } finally {
            opening.IsBusy = false;
        }
        RecordAuthoringOutcome(opening, outcome);
        if (IsCurrentAuthoring(opening, transferOpening)) {
            if (outcome.Kind is ProjectStructureAuthoringResultKind.Committed or ProjectStructureAuthoringResultKind.PartialCommit) {
                subprojectTransferDialog = null;
                await RefreshAuthoringSurfaceAsync(opening, outcome, BuildProjectChildNodeKey(outcome.TargetProjectId!.Value),
                    () => ReferenceEquals(opening, transferOpening));
                if (ReferenceEquals(opening, transferOpening)) {
                    transferOpening = null;
                }
            } else {
                subprojectTransferDialog = dialog with { Error = outcome.Message, RequiresObservation = opening.RequiresObservation };
            }
        }
        await RenderAuthoringOutcomeAsync();
    }
}
