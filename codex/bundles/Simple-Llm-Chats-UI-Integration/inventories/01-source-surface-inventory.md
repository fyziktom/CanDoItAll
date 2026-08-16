# Source Surface Inventory

## Reusable Conversation Components

- `src/UI/CanDoItAll.Conversations.Components/`
- Presentation models under `Presentation/`
- Participant cards/lists/picker
- Thread rail/history
- Workspace/header/transcript/message/composer
- Definition editor/provider/model/temperature controls
- Floating catalog/window/active-list/lifecycle fields

## Agent Adapters And Owners

- `src/MAF/Common/CanDoItAll.AgentFramework.Components/Agent*PresentationMapper.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Components/ChatWorkspacePanel.razor`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentThreadPresentationMapper.cs`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentChatPanel.*`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/FloatingAgentChatHost.*`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentDetailsDialog.*`

## Simple Chat Product And Durable Runtime

- `src/Modules/CanDoItAll.Modules.LlmChats/`
- `src/Modules/CanDoItAll.Modules.LlmChats.Persistence/`
- `src/MAF/Common/CanDoItAll.AgentFramework.Llm.Abstractions/`
- `src/App/CanDoItAll.Web/Api/LlmChat*.cs`
- `docs/llm-chats-api.md`
- `docs/architecture/llm-chats-boundary-and-handoffs.md`

## Composition And Shell

- `src/App/CanDoItAll.Composition/ModuleAssemblies.cs`
- `src/App/CanDoItAll.Web/Components/Routes.razor`
- `src/App/CanDoItAll.Web/Components/Layout/MainLayout.*`
- `src/UI/CanDoItAll.AppComponents/`

## Test Workspaces

- `tests/Solutions/CanDoItAll.Tests.Components.slnx`
- `tests/Solutions/CanDoItAll.Tests.Unit.slnx`
- `tests/Solutions/CanDoItAll.Tests.Integration.slnx`
- `tests/Solutions/CanDoItAll.Tests.Playwright.slnx`
- `tests/Solutions/CanDoItAll.Tests.Stable.slnx`
