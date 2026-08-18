# SB07 Semantic Invariants

- `SB07-INV-01` — Read-only definition projections never contain or request the system prompt.
- `SB07-INV-02` — Editor loading and all definition mutations require Manage authorization.
- `SB07-INV-03` — Provider, model, and thinking-effort choices come from the typed provider resolver projection and contain no provider SDK types.
- `SB07-INV-04` — Unsupported thinking effort, invalid timeout, invalid model parameters, and invalid schema JSON are rejected before mutation.
- `SB07-INV-05` — Updates and status changes carry the expected concurrency token; conflicts require explicit reload of current server state.
- `SB07-INV-06` — Definition status transitions are explicit and source-owned, never inferred from display strings.
- `SB07-INV-07` — The editor preserves immutable definition identity/revision metadata and creates a new revision through the application mutation contract.
- `SB07-INV-08` — Failures are rendered only through the sanitized UI failure contract.
- `SB07-INV-09` — The editor is one wide dialog with an internally scrolling body and reusable neutral field components.
- `SB07-INV-10` — No route, navigation entry, conversation workspace, streaming follower, or floating integration is activated in SB07.
