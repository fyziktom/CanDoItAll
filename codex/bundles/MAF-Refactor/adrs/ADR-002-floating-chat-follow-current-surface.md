# ADR-002: Floating chats follow the current surface through context epochs

- Status: Accepted for implementation
- Date: 2026-08-06

## Context

A floating agent is a long-lived conversation overlay. The user can open it on Project Structure Canvas, switch to Gantt, and continue the same conversation. The agent needs the new view on the next turn, but a run already in progress must not be retargeted.

The live context registry already republishes on view changes and captures an atomic snapshot at Send. What is missing is a chat-owned interpretation of the relationship between the previous turn and the current observation.

## Decision

Introduce `AgentConversationContextBinding`, `AgentContextEpochId`, and `AgentContextTransition`.

Initial modes:

- `FollowCurrentSurface` — default for floating chats.
- `Detached` — no application observation or product authority is supplied.

Transition rules:

| Change | Transition | Epoch behavior |
|---|---|---|
| Canvas -> Gantt in Project X | `ViewChanged` | same epoch |
| Selected task changes in Project X | `SelectionChanged` | same epoch |
| Project X -> Project Y | `SourceEntityChanged` | new epoch |
| Project Structure -> another module | `SourceKindChanged` | new epoch |
| Context removed | `ContextDetached` | new epoch |
| Context cannot be captured | `ContextUnavailable` | do not silently reuse old observation |

A context epoch marks which historical UI facts are current. The transcript remains continuous, but the trusted turn header tells the model that facts from an earlier epoch are historical and must not be treated as current.

Navigation does not call the model. The transition is included in the next explicit turn. The floating chat UI displays the current binding and pending transition so the user can see what the next turn will use.

Do not implement `PinnedToSource` in this bundle. Pinning an inactive source is safe only after the owning module provides a canonical rehydrator; stale opaque UI attachments cannot implement a pin.

## Consequences

- One conversation can move naturally between Canvas and Gantt.
- Cross-project continuity remains possible without silently carrying old authority.
- The model is not invoked merely because a tab changes.
- The chat UI can expose `Following: Project X / Gantt` and `Context changed: Project Y` states.

## Proof scenarios

1. Canvas turn admitted at observation version N; UI switches to Gantt version N+1; admitted turn still uses N.
2. Next turn uses N+1 and contains a trusted `ViewChanged` transition.
3. Project switch increments the context epoch and re-resolves authority.
4. Detached mode sends no application observation and no context-derived authority.
5. Navigation alone produces no provider invocation.
