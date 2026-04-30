using CanDoItAll.Components.CanvasLib;

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
}
