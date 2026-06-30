using CanDoItAll.AgentFramework.Components;
using CanDoItAll.Components.CanvasLib;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workbench.Pages;

public partial class ProjectStructurePage
{
    private const string AgentWindowKey = "project-structure.agents";
    private const string AgentChatWindowKey = "project-structure.agents.chat";

    private CanvasWorkbenchWindowState AgentWindowState => ResolveWindowState(AgentWindowKey);

    private Task HandleAgentWindowStateChangedAsync(CanvasWorkbenchWindowState state)
        => PersistWindowStateAsync(AgentWindowKey, state);

    private Task ToggleAgentWindowAsync()
        => ToggleWindowAsync(AgentWindowKey);

    private async Task HandleAgentWorkspaceRefreshRequestedAsync(ContextualAgentWorkspaceRefreshRequest request)
    {
        if (request.WorkspaceKind != ContextualAgentWorkspaceKind.ProjectStructure ||
            request.ProjectId != ProjectId)
        {
            return;
        }

        await CaptureCurrentWorkbenchStateAsync();
        await ReloadSurfaceAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task CaptureCurrentWorkbenchStateAsync()
    {
        if (workbenchRef is null)
        {
            return;
        }

        try
        {
            currentViewStateJson = NormalizePersistedCanvasStateJson(await workbenchRef.GetStateJsonAsync());
        }
        catch (Exception exception)
        {
            Logger.LogDebug(exception, "Unable to capture project structure canvas state before contextual agent refresh.");
        }
    }
}
