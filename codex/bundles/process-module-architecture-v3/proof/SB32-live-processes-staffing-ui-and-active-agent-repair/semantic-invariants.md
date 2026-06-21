# SB32 Semantic Invariants

- Technical subprocess steps in `software-delivery` must use technical responsible roles by default. Delivery Manager can coordinate or review, but must not be the visible responsible owner for architecture or implementation.
- Staffing tests must prove architecture resolves to a .NET architect-capable executor and implementation resolves to a .NET developer-capable executor when those agents exist.
- Manual HR overrides still flow through the SB31 readiness checks; this subbundle does not bypass readiness or silently downgrade errors.
- Live-process time windows are API semantics, not cosmetic UI filtering. A run with `LastEventAtUtc` before the window start is excluded even if its status is active.
- Active-agent cards are derived from runtime state plus step assignments. Expired leases are stale claims, not working agents.
- Process start feedback must survive navigation from project structure into Live Processes.
- The first Live Processes tab must give an operator enough context to see active runs, attention state, and next action without opening a secondary page first.
- The detail dialog must expose active agents, stale claims, incidents, manager messages, and recent events for the selected run.
