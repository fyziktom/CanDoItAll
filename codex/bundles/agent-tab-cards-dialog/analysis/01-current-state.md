# Current State

## Affected Surfaces

- `/agents` is implemented by `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\AgentsHomePage.razor`.
- The Agents tab currently renders `AgentCatalogPanel` inline.
- `AgentCatalogPanel` uses a `ListDetailShell`: the left list is `SelectionListItem`, and the right pane contains the full technical editor.
- `AgentSwitchDialog` in chat currently renders its own card markup rather than using `AgentSelectionCard`.
- `AgentSelectionCard` already exists in `CanDoItAll.AgentFramework.Components` but is not currently used by the switch dialog or Agents tab.
- Capability assignment exists in `AgentCapabilitiesPanel`, using `AgentEditorModel.SelectedCapabilityIds` plus `WorkspaceService.SaveAgentAsync`.

## Existing Component Paths

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Components\AgentSelectionCard.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Components\AgentSwitchDialog.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Components\AgentSwitchDialog.razor.css`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\AgentCatalogPanel.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\AgentCatalogPanel.razor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\AgentCapabilitiesPanel.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\AgentCapabilitiesPanel.razor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Navigation\Tabs.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Modals\DialogService.cs`

## Current Tests

- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\AgentChatModalTests.cs` covers switch-agent modal behavior.
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\AiAgentsPageTests.cs` covers AgentCatalogPanel and AgentsHomePage behavior.

## Component MCP Note

- The `candoitall-components-mcp` skill requires querying the component MCP before new layout markup. Tool discovery did not expose `components_search` or related component tools in this session. The fallback source of truth is the checked-in BaseLib component source and existing usage examples for `Grid`, `Stack`, `Tabs`, `DialogService`, `FormSection`, and related primitives.
