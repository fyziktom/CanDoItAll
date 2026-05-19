# Source Artifacts

## User And Architect Notes

- `N001`: Main goal is "Agents teams, their management and usage."
- `N002`: Split agents into teams; each team has multiple agents.
- `N003`: Team creation must be in the Agents module.
- `N004`: Add a tree view on the Agents tab of the Agents page that shows teams and agents under each team.
- `N005`: Clicking a team item in the tree view filters agents for that team.
- `N006`: Adding agents to a team must open a modal with multi-selection by clicking agent cards.
- `N007`: The team membership modal should follow the same card-selection pattern as the switch-agent modal in the chat tab.
- `N008`: One agent can be in multiple teams.
- `N009`: During HR matching of a starting process, it must be possible to select a team for delivery.
- `N010`: HR matching must add/select agents for required process roles even when they are not part of the selected team.
- `N011`: Out-of-team required-role selections must be marked in the matching modal.

## Repository Evidence Inputs

- Agent catalog page and panel: `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\AgentsHomePage.razor`, `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\AgentCatalogPanel.razor`, `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\AgentCatalogPanel.razor.cs`
- Existing chat switch-agent modal and card pattern: `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Components\AgentSwitchDialog.razor`, `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Components\AgentSelectionCard.razor`
- Agent catalog contracts and storage: `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Workspace\WorkspaceModels.cs`, `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Agents\AgentModels.cs`, `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Contracts\Contracts.cs`, `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Catalog\AgentFrameworkWorkspaceCatalogService.Agents.cs`
- Process launch and HR matching: `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceRunsLaunchSection.razor`, `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.Launch.cs`, `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Launch\ProcessesService.Launch.Staffing.cs`, `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Launch\ProcessesService.Launch.Reads.cs`, `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\ProcessesApi.cs`
