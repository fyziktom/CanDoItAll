using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.Workbench.Pages;

public partial class ProjectStructurePage
{
    private ProjectStructureProcessLinkDialogState? processLinkDialog;
    private ProjectStructureProcessStartDialogState? processStartDialog;

    private Task OpenAddProcessDialogAsync(ProjectStructureNode node)
    {
        return OpenLinkProcessDialogAsync(node);
    }

    private Task OpenLinkProcessDialogAsync(ProjectStructureNode node)
    {
        processLinkDialog = new ProjectStructureProcessLinkDialogState(
            node.Id,
            node.Title,
            [],
            null,
            "Process linking is unavailable while the Process module is rebuilt.");
        return InvokeAsync(StateHasChanged);
    }

    private void CloseProcessLinkDialog()
    {
        processLinkDialog = null;
    }

    private void HandleProcessLinkSelectionChanged(ChangeEventArgs args)
    {
    }

    private Task ExecuteProcessLinkAsync()
    {
        if (processLinkDialog is not null)
        {
            processLinkDialog = processLinkDialog with
            {
                Error = "Process linking is unavailable while the Process module is rebuilt."
            };
        }

        return InvokeAsync(StateHasChanged);
    }

    private Task OpenStartProcessDialogAsync(ProjectStructureNode node)
    {
        processStartDialog = new ProjectStructureProcessStartDialogState(
            ProjectId,
            Guid.Empty,
            node.Id,
            node.Title,
            null,
            string.Empty,
            null,
            ProjectStructureProcessStartStage.Confirm,
            false,
            false,
            "Process launching is unavailable while the Process module is rebuilt.",
            [],
            string.Empty,
            DateTimeOffset.UtcNow,
            false,
            string.Empty);
        return InvokeAsync(StateHasChanged);
    }

    private Task OpenEstimateProcessDialogAsync(ProjectStructureNode node)
    {
        return OpenStartProcessDialogAsync(node);
    }

    private void CloseProcessStartDialog()
    {
        processStartDialog = null;
    }

    private Task ReviewAndStartProcessAsync()
    {
        return ExecuteProcessStartAsync();
    }

    private Task ExecuteProcessStartAsync()
    {
        if (processStartDialog is not null)
        {
            processStartDialog = processStartDialog with
            {
                Error = "Process launching is unavailable while the Process module is rebuilt."
            };
        }

        return InvokeAsync(StateHasChanged);
    }

    private Task SelectProcessStartCandidateAsync(ProjectStructureProcessStartCandidateSelection selection)
    {
        return Task.CompletedTask;
    }

    private Task OpenManualProcessStartAgentPickerAsync(Guid launchPlanRoleId)
    {
        return Task.CompletedTask;
    }

    private Task HandleProcessStartAssignmentsReviewedChanged(ChangeEventArgs args)
    {
        if (processStartDialog is not null)
        {
            var isChecked = args.Value is bool value && value;
            processStartDialog = processStartDialog with
            {
                AssignmentsReviewed = isChecked
            };
        }

        return InvokeAsync(StateHasChanged);
    }

    private Task RequestHrManagerMatchAsync()
    {
        return ExecuteProcessStartAsync();
    }

    private Task CancelHrManagerMatchAsync()
    {
        return Task.CompletedTask;
    }

    private Task ExecuteHrManagerMatchAsync()
    {
        return ExecuteProcessStartAsync();
    }
}
