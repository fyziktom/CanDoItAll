using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;

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
                BuildSimpleNoteTitle(string.IsNullOrWhiteSpace(node.Notes) ? node.Title : node.Notes),
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
        if (subprojectTransferDialog is null)
        {
            return;
        }

        var projectName = subprojectTransferDialog.ProjectName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(projectName))
        {
            subprojectTransferDialog = subprojectTransferDialog with { Error = "Enter a subproject name before continuing." };
            return;
        }

        var sourceNode = ResolveNode(subprojectTransferDialog.SourceNodeId);
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
            : BuildSimpleNoteTitle(sourceNode.Notes);
        editor.CurrentPhase = string.IsNullOrWhiteSpace(sourceProject.CurrentPhase)
            ? "Discovery"
            : sourceProject.CurrentPhase;
        editor.Status = sourceProject.Status;

        var saveResult = await ProjectsService.SaveAsync(editor);
        if (saveResult.IsFailure)
        {
            subprojectTransferDialog = subprojectTransferDialog with
            {
                Error = saveResult.Errors.FirstOrDefault()?.Message ?? "The new subproject could not be created."
            };
            return;
        }

        var transferResult = await ProjectWorkbenchService.MoveDescendantsToProjectAsync(ProjectId, sourceNode.Id, saveResult.Value);
        if (transferResult is null)
        {
            subprojectTransferDialog = subprojectTransferDialog with
            {
                Error = "The new subproject was created, but the descendants could not be moved into it."
            };
            return;
        }

        var attachResult = await ProjectsService.AddSubprojectAsync(ProjectId, saveResult.Value);
        if (attachResult.IsFailure)
        {
            subprojectTransferDialog = subprojectTransferDialog with
            {
                Error = attachResult.Errors.FirstOrDefault()?.Message ?? "The descendants were moved, but the subproject could not be attached."
            };
            return;
        }

        subprojectTransferDialog = null;
        await ReloadSurfaceAsync($"project-child:{saveResult.Value}");
        workflowFeedback = transferResult.MovedNodeCount == 1
            ? $"Created {projectName} and moved 1 descendant into it."
            : $"Created {projectName} and moved {transferResult.MovedNodeCount} descendants into it.";
        workflowFeedbackTone = "mint";
        await InvokeAsync(StateHasChanged);
    }
}
