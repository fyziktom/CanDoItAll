using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workbench.Pages;

public partial class ProjectStructurePage
{
    private ProjectStructureBlockMutationDialogState? blockMutationDialog;
    private ProjectStructureSubprojectTransferDialogState? subprojectTransferDialog;

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
        blockMutationDialog = new ProjectStructureBlockMutationDialogState(
            mode,
            node.Id,
            node.Title,
            options,
            selectedActionId,
            string.Empty);
        await InvokeAsync(StateHasChanged);
    }

    private void CloseBlockMutationDialog()
        => blockMutationDialog = null;

    private void HandleBlockMutationSelectionChanged(ChangeEventArgs args)
    {
        if (blockMutationDialog is null)
        {
            return;
        }

        blockMutationDialog = blockMutationDialog with
        {
            SelectedActionId = args.Value?.ToString()?.Trim() ?? string.Empty,
            Error = string.Empty
        };
    }

    private async Task ExecuteBlockMutationAsync()
    {
        if (blockMutationDialog is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(blockMutationDialog.SelectedActionId) ||
            !ProjectStructureCanvasCatalog.TryResolveCreateDefinition(blockMutationDialog.SelectedActionId, out var definition))
        {
            blockMutationDialog = blockMutationDialog with { Error = "Choose a block type before continuing." };
            return;
        }

        var node = ResolveNode(blockMutationDialog.NodeId);
        if (node is null)
        {
            blockMutationDialog = null;
            workflowFeedback = "The selected node is no longer available.";
            workflowFeedbackTone = "warn";
            await InvokeAsync(StateHasChanged);
            return;
        }

        var reclassificationRequest = blockMutationDialog.Mode switch
        {
            ProjectStructureBlockMutationDialogMode.ChangeBlockType => new ProjectObjectReclassificationRequest(
                ProjectObjectType.ProjectBlock,
                definition.ObjectSubtype,
                node.Title,
                node.Subtitle,
                node.Notes,
                "{}"),
            _ => new ProjectObjectReclassificationRequest(
                definition.ObjectType,
                definition.ObjectSubtype,
                ProjectStructureNodeHelpers.BuildSimpleNoteTitle(string.IsNullOrWhiteSpace(node.Notes) ? node.Title : node.Notes),
                string.Empty,
                string.IsNullOrWhiteSpace(node.Notes) ? node.Title : node.Notes,
                "{}")
        };

        var updatedNode = await ProjectWorkbenchService.ReclassifyObjectAsync(ProjectId, node.Id, reclassificationRequest);
        if (updatedNode is null)
        {
            blockMutationDialog = blockMutationDialog with
            {
                Error = "The selected node could not be changed to the requested block type."
            };
            return;
        }

        var wasBlockTypeChange = blockMutationDialog.Mode == ProjectStructureBlockMutationDialogMode.ChangeBlockType;
        blockMutationDialog = null;
        await ApplySurfaceNodeUpdatesAsync([updatedNode]);
        workflowFeedback = wasBlockTypeChange
            ? $"{updatedNode.Title} was changed to {ProjectStructureCanvasCatalog.ResolveNodeLabel(updatedNode).ToLowerInvariant()}."
            : $"{updatedNode.Title} was converted to {ProjectStructureCanvasCatalog.ResolveNodeLabel(updatedNode).ToLowerInvariant()}.";
        workflowFeedbackTone = "mint";
        await InvokeAsync(StateHasChanged);
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

        subprojectTransferDialog = new ProjectStructureSubprojectTransferDialogState(
            node.Id,
            node.Title,
            descendantCount,
            $"{node.Title} subproject",
            string.Empty);
        await InvokeAsync(StateHasChanged);
    }

    private void CloseSubprojectTransferDialog()
        => subprojectTransferDialog = null;

    private void HandleSubprojectTransferNameChanged(ChangeEventArgs args)
    {
        if (subprojectTransferDialog is null)
        {
            return;
        }

        subprojectTransferDialog = subprojectTransferDialog with
        {
            ProjectName = args.Value?.ToString() ?? string.Empty,
            Error = string.Empty
        };
    }

    private async Task ExecuteSubprojectTransferAsync()
    {
        var transferDialog = subprojectTransferDialog;
        if (transferDialog is null)
        {
            return;
        }

        var projectName = transferDialog.ProjectName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(projectName))
        {
            subprojectTransferDialog = transferDialog with { Error = "Enter a subproject name before continuing." };
            return;
        }

        var sourceNode = ResolveNode(transferDialog.SourceNodeId);
        if (sourceNode is null)
        {
            subprojectTransferDialog = null;
            workflowFeedback = "The selected source node is no longer available.";
            workflowFeedbackTone = "warn";
            await InvokeAsync(StateHasChanged);
            return;
        }

        var sourceProject = await ProjectsService.GetAsync(ProjectId);
        var editor = await ProjectsService.GetAsync(null);
        editor.Name = projectName;
        editor.Description = $"Extracted from {sourceNode.Title} in {surface?.ProjectName ?? sourceProject.Name}.";
        editor.Objective = string.IsNullOrWhiteSpace(sourceNode.Notes)
            ? sourceNode.Title
            : ProjectStructureNodeHelpers.BuildSimpleNoteTitle(sourceNode.Notes);
        editor.CurrentPhase = string.IsNullOrWhiteSpace(sourceProject.CurrentPhase)
            ? "Discovery"
            : sourceProject.CurrentPhase;
        editor.Status = sourceProject.Status;

        try
        {
            var result = await SubprojectTransferCoordinator.MoveDescendantsToNewSubprojectAsync(
                ProjectId,
                editor,
                sourceNode.Id);

            subprojectTransferDialog = null;
            await ReloadSurfaceAsync($"project-child:{result.TargetProjectId}");
            workflowFeedback = result.Transfer.MovedNodeCount == 1
                ? $"Created {projectName} and moved 1 descendant into it."
                : $"Created {projectName} and moved {result.Transfer.MovedNodeCount} descendants into it.";
            workflowFeedbackTone = "mint";
        }
        catch (ProjectStructureCompensatedSubprojectTransferException exception)
        {
            Logger.LogWarning(
                exception,
                "Subproject transfer failed and removed empty child {TargetProjectId} for source project {SourceProjectId} and source node {SourceNodeId}.",
                exception.RemovedProjectId,
                ProjectId,
                sourceNode.Id);
            subprojectTransferDialog = transferDialog with
            {
                Error = "The transfer failed. The empty child project was removed, and the source structure was left unchanged."
            };
        }
        catch (ProjectStructureTransferPartialCommitException exception)
        {
            Logger.LogWarning(
                exception,
                "Subproject transfer retained child {TargetProjectId} after a partial commit for source project {SourceProjectId} and source node {SourceNodeId}. Durable mutation {DurableMutationId} has status {DurableMutationStatus}.",
                exception.Recovery.TargetProjectId,
                ProjectId,
                sourceNode.Id,
                exception.Recovery.DurableMutationId,
                exception.Recovery.DurableMutationStatus);
            subprojectTransferDialog = null;
            await ReloadSurfaceAsync($"project-child:{exception.Recovery.TargetProjectId}");
            workflowFeedback = $"Created {projectName} and moved its descendants, but assignment reconciliation still requires recovery. {exception.Recovery.RetryGuidance}";
            workflowFeedbackTone = "warn";
        }
        catch (ProjectStructureProjectCreationRejectedException exception)
        {
            Logger.LogWarning(
                exception,
                "Subproject creation was rejected for source project {SourceProjectId} and source node {SourceNodeId} with {ErrorCount} validation error(s).",
                ProjectId,
                sourceNode.Id,
                exception.Errors.Count);
            subprojectTransferDialog = transferDialog with
            {
                Error = exception.Message
            };
        }
        catch (ProjectStructureTransferRejectedException exception)
        {
            Logger.LogWarning(
                exception,
                "Subproject transfer was rejected for source project {SourceProjectId} and source node {SourceNodeId} with reason {RejectionReason}.",
                ProjectId,
                sourceNode.Id,
                exception.Reason);
            subprojectTransferDialog = transferDialog with
            {
                Error = exception.Reason == ProjectStructureTransferRejectionReason.TargetProjectMismatch
                    ? "The subproject transfer returned inconsistent target information. Review the logs before retrying."
                    : exception.Message
            };
        }
        catch (Exception exception)
        {
            Logger.LogError(
                exception,
                "Subproject transfer failed unexpectedly for source project {SourceProjectId} and source node {SourceNodeId}.",
                ProjectId,
                sourceNode.Id);
            subprojectTransferDialog = transferDialog with
            {
                Error = "The subproject transfer failed unexpectedly. Review the logs before retrying."
            };
        }

        await InvokeAsync(StateHasChanged);
    }
}
