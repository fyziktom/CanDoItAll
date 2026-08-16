# SB01 live consumer inventory

| Surface | Live consumers |
|---|---|
| `FloatingAgentChatHost` | `src/App/CanDoItAll.Web/Components/Layout/MainLayout.razor` |
| `AgentCatalogPanel` | `AgentsHomePage.razor` |
| `AgentChatPanel` | `AgentsHomePage.razor`, `FloatingAgentChatHost.razor` |
| `AgentSelectionCard` | `AgentCatalogPanel.razor`, `AgentSwitchDialog.razor`, `AgentTeamMembersDialog.razor`, `CrmHrAgentsPage.razor` |
| `AgentCompactList` | `FloatingAgentChatHost.razor` |
| `ChatWorkspacePanel` | `AgentChatPanel.razor`, `ContextualAgentWorkspaceWindows.razor`, `ProcessWorkspaceShell.razor` |
| `ProviderModelSelector` | `AgentDetailsDialog.razor` and three `WorkflowCanvasEditor.razor` fields |
| `FloatingAgentChatSettingsPanel` | `FloatingAgentChatHost.razor` |

`WorkflowCanvasEditor` is an additional live provider-selector consumer discovered during execution and is carried into the migration plan. The root host, contextual workspace, Processes workspace, and CRM-HR card consumer remain compatibility consumers; none may take a dependency on Agent module internals that they do not already own.

