# Source hotspot inventory

## Neutral-boundary candidates

- `src/UI/CanDoItAll.Conversations.Components/**` — new preferred owner
- `src/UI/CanDoItAll.AppComponents/**` — approved fallback location only

## AgentFramework component sources

- `src/MAF/Common/CanDoItAll.AgentFramework.Components/ChatWorkspacePanel.razor`
- `src/MAF/Common/CanDoItAll.AgentFramework.Components/ChatMarkdownRenderer.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Components/ChatPromptTextArea.razor`
- `src/MAF/Common/CanDoItAll.AgentFramework.Components/AgentSelectionCard.razor`
- `src/MAF/Common/CanDoItAll.AgentFramework.Components/AgentCompactList.razor`
- `src/MAF/Common/CanDoItAll.AgentFramework.Components/AgentCompactListItem.razor`
- `src/MAF/Common/CanDoItAll.AgentFramework.Components/AgentSwitchDialog.razor`
- `src/MAF/Common/CanDoItAll.AgentFramework.Components/AgentThreadHistoryDialog.razor`
- `src/MAF/Common/CanDoItAll.AgentFramework.Components/ProviderModelSelector.razor`
- `src/MAF/Common/CanDoItAll.AgentFramework.Components/AgentThinkingEffortSettings.razor`
- `src/MAF/Common/CanDoItAll.AgentFramework.Components/ContextualAgentWorkspaceWindows.razor`
- `src/MAF/Common/CanDoItAll.AgentFramework.Components/CanDoItAll.AgentFramework.Components.csproj`

## Agent product UI

- `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentChatPanel.razor`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentChatPanel.razor.cs`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentCatalogPanel.razor`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentDetailsDialog.razor`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentDetailsDialog.razor.cs`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/FloatingAgentChatHost.razor`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/FloatingAgentChatHost.razor.cs`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/FloatingAgentChatSettingsPanel.razor`
- `src/Modules/CanDoItAll.Modules.AgentFramework/CanDoItAll.Modules.AgentFramework.csproj`

## Non-Agent module consumers

- `src/Modules/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceShell.razor`
- any additional consumers returned by live symbol/reference analysis

## Tests

- `tests/Components/CanDoItAll.Tests.Components/**`
- `tests/Solutions/CanDoItAll.Tests.Components.slnx`
- targeted Playwright files returned by live impact analysis
- test project references and solution files when the new neutral project is added

## Context-only Simple Chat evidence

These files must not be production change targets in Phase 1:

- `src/Modules/CanDoItAll.Modules.LlmChats/Definitions/LlmChatDefinition.cs`
- `src/Modules/CanDoItAll.Modules.LlmChats/Definitions/LlmChatDefinitionRevision.cs`
- `src/Modules/CanDoItAll.Modules.LlmChats/**`
- current Simple Chat HTTP/SSE/API files
