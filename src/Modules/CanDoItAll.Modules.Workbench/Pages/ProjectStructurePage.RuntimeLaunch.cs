using CanDoItAll.Components.BaseLib;

namespace CanDoItAll.Modules.Workbench.Pages;

public partial class ProjectStructurePage
{
    private async Task LaunchRuntimeAsync(
        ProjectStructureNode node,
        ProjectStructureRuntimeLaunchMode mode)
    {
        var approval = ProjectStructureRuntimeLaunchApproval.NotGranted;
        var resolution = RuntimeLauncher.Resolve(node);
        if (resolution.Plan is { RequiresApproval: true } plan)
        {
            var confirmed = await DialogService.OpenAsync<ProjectStructureRuntimeLaunchApprovalDialog>(
                "Approve script launch?",
                new Dictionary<string, object?>
                {
                    [nameof(ProjectStructureRuntimeLaunchApprovalDialog.DisplayName)] = plan.DisplayName
                },
                new DialogOptions
                {
                    Eyebrow = "Runtime policy",
                    Subtitle = "Explicit shell scripts require confirmation for every launch.",
                    Size = ModalSize.Compact,
                    DenseChrome = true,
                    TestId = "project-structure-runtime-launch-approval-dialog",
                    AriaLabel = "Confirm explicit runtime script launch",
                    ChromeCloseResult = false
                });
            if (confirmed is not true)
            {
                workflowFeedback = "The explicit script launch was not approved.";
                workflowFeedbackTone = "warn";
                await InvokeAsync(StateHasChanged);
                return;
            }

            approval = ProjectStructureRuntimeLaunchApproval.OperatorConfirmed;
        }

        var result = await RuntimeLauncher.LaunchAsync(node, mode, approval);
        workflowFeedback = result.Message;
        workflowFeedbackTone = result.IsSuccess ? "mint" : "warn";
        await InvokeAsync(StateHasChanged);
    }

    private async Task StopRuntimeAsync(ProjectStructureNode node)
    {
        var result = await RuntimeLauncher.StopAsync(node);
        workflowFeedback = result.Message;
        workflowFeedbackTone = result.IsSuccess ? "mint" : "warn";
        await InvokeAsync(StateHasChanged);
    }
}
