# Repository evidence

## Architecture owners

- `docs/architecture/overview.md`
  - `src/UI` owns application-level reusable UI facades.
  - product modules own product behavior and typed UI state.
  - adding a project is justified when it creates a real dependency boundary or independent validation surface.
- `src/UI/README.md`
  - app-owned reusable UI belongs under `src/UI`;
  - product-specific orchestration stays in the owning module.
- `src/UI/CanDoItAll.AppComponents/CanDoItAll.AppComponents.csproj`
  - currently has no AgentFramework or LlmChats dependency.
- `src/MAF/Common/CanDoItAll.AgentFramework.Components/CanDoItAll.AgentFramework.Components.csproj`
  - directly references AgentFramework Models, Core, and Voice.
- `src/Modules/CanDoItAll.Modules.AgentFramework/CanDoItAll.Modules.AgentFramework.csproj`
  - owns the application-facing Agent pages and directly references broad Agent backend/runtime layers.

## Current chat surfaces

- `ChatWorkspacePanel.razor`
  - renders participant identity, transcript, markdown, status, execution, approvals, composer, attachments, voice, and runtime details;
  - accepts agent domain and execution types directly.
- `AgentChatPanel.razor` and `.razor.cs`
  - own thread rail, workspace loading, session creation/switching, execution orchestration, prompt gallery, voice, attachments, and errors.
- `FloatingAgentChatHost.razor` and `.razor.cs`
  - own agent catalog, active handles, context, history, lifecycle, and floating windows.
- `ContextualAgentWorkspaceWindows.razor`
  - consumes current AgentFramework chat components.
- `ProcessWorkspaceShell.razor`
  - consumes AgentFramework chat/context surfaces outside the Agents module.

## Current listing surfaces

- `AgentSelectionCard.razor`
- `AgentCompactList.razor`
- `AgentCompactListItem.razor`
- `AgentSwitchDialog.razor`
- `AgentCatalogPanel.razor`

Their public contracts use `AgentDefinition` and embed agent-specific status, workload, tags, capability, provider privacy, and history semantics.

## Current settings surfaces

- `AgentDetailsDialog.razor`
  - reusable overlap: identity, avatar, summary, instructions, provider, model;
  - agent-only content: status, workload, chat history, approvals, Memory, Images, capabilities, tools, skills, governance, and runtime policy.
- `ProviderModelSelector.razor`
  - reusable behavior but accepts `ProviderProfile`.
- `FloatingAgentChatSettingsPanel.razor`
  - generic overlap: active-chat retention and capacity;
  - agent-only content: prepared agent metadata stock and adaptive preparation.

## Simple Chat future overlap

- `LlmChatDefinition.cs`
  - name, summary, avatar, status, revision metadata.
- `LlmChatDefinitionRevision.cs`
  - system prompt, provider profile, provider kind/name, model, temperature/settings, thinking effort, timeout, response format.

These files are evidence for future compatibility only. Phase 1 must not reference them from production UI.
