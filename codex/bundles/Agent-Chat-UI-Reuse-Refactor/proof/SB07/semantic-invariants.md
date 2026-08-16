# SB07 semantic invariants

- Production labels remain `Agents`, `Agent chats`, and `Active chats`; no mixed-source or Simple Chat control exists.
- Catalog default geometry remains 560 by 720 at top-right with the same min/max constraints and controlled `OverlayWindowState`.
- The Agent coordinator remains the only owner of visible/hidden handles, retention, capacity, run state, keep-active, and stop behavior.
- Closing a visible chat still opens the Agent decision dialog; Keep active hides without stopping and Stop removes only the active handle while preserving durable history.
- Context access and affinity remain fail-closed and Agent-owned; Detach and Follow context use the existing conversation binding service.
- Prepared activation stock, adaptive preparation, and prepared-resource retention remain in the Agent settings owner.
- Neutral presentation contains no AgentFramework, LlmChats, persistence, backend service, EF, or service-location reference.
