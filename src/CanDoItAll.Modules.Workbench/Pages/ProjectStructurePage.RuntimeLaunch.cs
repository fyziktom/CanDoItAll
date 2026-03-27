namespace CanDoItAll.Modules.Workbench.Pages;

public partial class ProjectStructurePage
{
    private async Task LaunchRuntimeAsync(ProjectStructureNode node, bool runAsAdministrator)
    {
        var result = await RuntimeLauncher.LaunchAsync(node, runAsAdministrator);
        workflowFeedback = result.Message;
        workflowFeedbackTone = result.IsSuccess ? "mint" : "warn";
        await InvokeAsync(StateHasChanged);
    }
}
