# Current state and planned delta

## Current state

- `CanDoItAll.AgentFramework.Components` owns both reusable chat markup and direct AgentFramework dependencies.
- `CanDoItAll.Modules.AgentFramework` owns product pages and substantial backend orchestration in Razor code-behind.
- `AgentSelectionCard`, `AgentCompactList`, and `AgentSwitchDialog` expose `AgentDefinition`.
- `ProviderModelSelector` exposes `ProviderProfile`.
- `AgentDetailsDialog` combines general identity/runtime editing with agent-only policies.
- `FloatingAgentChatHost` combines presentation, coordinator state, context, history, and handle lifecycle.
- `ChatWorkspacePanel` has consumers in Agents, contextual Agent windows, and Processes.
- Simple Chat backend is product-ready enough for a later UI phase, but its module is non-Razor and must stay untouched here.

## Planned delta

- add a focused app-owned neutral Conversation Components project;
- introduce small presentation records and focused Razor components;
- add AgentFramework adapters/facades that map existing records to those presentation records;
- keep backend service injection and commands in existing Agent-owned layers;
- migrate current Agent consumers;
- remove duplicated presentation code only after compatibility proof;
- preserve visible behavior;
- leave Simple Chat UI entirely dormant.
