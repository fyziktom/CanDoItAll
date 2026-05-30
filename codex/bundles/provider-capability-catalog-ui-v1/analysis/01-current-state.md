# Current State

## Source Observations

- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/AgentsHomePage.razor.cs` loads dashboard counts from `IAgentFrameworkWorkspaceService.GetDashboardAsync()`. Provider count is therefore the merged AgentFramework provider catalog.
- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/AgentsHomePage.razor` currently renders `ProviderManagementPanel` for the providers tab.
- `repo://src/CanDoItAll.Modules.Workspace/Pages/Components/ProviderManagementPanel.razor.cs` lists `WorkspaceService.ListProviderProfilesAsync()`, which reads only `Workspace_ProviderProfiles`; after a clean dev DB this materializes one provider while the AgentFramework catalog returns four.
- `repo://src/CanDoItAll.Modules.AgentFramework/Providers/WorkspaceBackedAgentProviderProfileRegistry.cs` already merges Workspace DB providers with catalog providers, explaining the count/list mismatch.
- `repo://src/CanDoItAll.AgentFramework.Models/Providers/ProviderModels.cs` has no provider tags.
- `repo://src/CanDoItAll.AgentFramework.Models/Capabilities/CapabilityModels.cs` has no capability tags.
- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentCapabilitiesPanel.razor` uses a flat `SelectionListItem` agent list and a vertical capability card list.
- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentCatalogPanel.razor` already uses `TreeView` for agent/team hierarchy.
- `repo://src/CanDoItAll.Modules.Plugins/Pages/PluginCatalogTreeNodeBuilder.cs` provides a local pattern for tag-grouped tree nodes.
- `repo://src/CanDoItAll.AppComponents/Components/Steps.razor` and `repo://src/CanDoItAll.Modules.Workspace/Pages/Components/StorageSettingsPanel.razor` provide the generic wizard step pattern.
- `TagEditor` is already consumed from the shared component namespace in `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentDetailsDialog.razor`.

## Component Discovery Note

The requested CanDoItAll components MCP was queried before implementation, but every `components_search` call failed with `Transport closed`. The fallback is direct source inspection of existing component usage.
