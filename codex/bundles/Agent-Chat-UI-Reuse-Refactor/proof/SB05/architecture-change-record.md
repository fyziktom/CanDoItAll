# Architecture change record — SB05

## Decision

Conversation workspace presentation now belongs to `CanDoItAll.Conversations.Components`. The Agent component project remains the adapter and orchestration owner.

## Responsibility moved

- workspace/header layout and responsive CSS
- transcript and empty-state rendering
- typed message, role, avatar, badge, and pending-state presentation
- user/assistant message bubbles, timestamp/token/copy chrome, and safe markdown
- prompt text area and composer layout/callback routing

## Responsibility retained by AgentFramework

- `ChatSessionRecord`, `ChatMessageRecord`, execution records, and `Guid` identities
- `User request:` hidden-context parsing and copy-value projection
- send, cancel, approval, attachment, voice, prompt-gallery, runtime-dialog, and execution behavior
- editable thread titles and Agent-specific header/action fragments

## Dependency direction

`CanDoItAll.Modules.AgentFramework` -> `CanDoItAll.AgentFramework.Components` -> `CanDoItAll.Conversations.Components`.

The neutral project has no reverse reference, no AgentFramework/LlmChats/runtime/persistence dependency, no DI/service location, and no `Guid` identity contract. CodeAnalytics snapshot `snap-20260816122736-acdf4779` found three scoped projects, the expected one-way project references, and no blocking diagnostics.

## Architecture review

- real presentation ownership was removed from `ChatWorkspacePanel`; it is now a focused facade with Agent-owned slots
- no new partial file was added to `ChatWorkspacePanel` or `AgentChatPanel`
- Markdig ownership moved with safe markdown rendering; no duplicate package dependency remains in the Agent component project
- extension points are focused `RenderFragment` slots, not a boolean-god workspace component
- existing Agent public facade parameters and callbacks remain compatible

Decision: CP2 architecture gate passes to SB06.
