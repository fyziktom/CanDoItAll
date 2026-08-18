# C# Current-State Inventory

## Reviewed Ownership

- `CanDoItAll.Conversations.Components` owns backend-neutral Razor presentation.
- `CanDoItAll.AgentFramework.Components` owns Agent adapters and Agent-specific UI fragments.
- `CanDoItAll.Modules.AgentFramework` owns Agent product pages, floating coordinator integration, context authority, and runtime actions.
- `CanDoItAll.Modules.LlmChats` owns definitions, conversations, operations, durable event sessions, and provider-neutral application contracts.
- `CanDoItAll.Modules.LlmChats.Persistence` owns PostgreSQL and runtime adapters.
- `CanDoItAll.Web` owns shell composition, routing, and HTTP transport.

## Existing Hotspots Relevant To This Bundle

- `FloatingAgentChatHost.razor(.cs)` still combines catalog presentation with Agent source orchestration.
- The neutral active list retains hard-coded Open/Stop semantics.
- Transcript transient state is single-item and User-shaped.
- The conversation state hides the active operation identity.

## Positive Baseline

The previous refactor created real project separation rather than partial-class or namespace-only separation. The target is incremental hardening and new adapters, not a second broad decomposition.

## Execution Evidence Requirement

SB01 must build a scoped CodeAnalytics snapshot covering:

- `CanDoItAll.Conversations.Components`
- `CanDoItAll.AgentFramework.Components`
- `CanDoItAll.Modules.AgentFramework`
- `CanDoItAll.Modules.LlmChats`
- `CanDoItAll.Modules.LlmChats.Persistence`
- `CanDoItAll.Web`

Record dashboard health, findings, project references, reverse references, and cycles. Do not reuse predecessor snapshot ids as current proof.
