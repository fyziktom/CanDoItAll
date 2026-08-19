# SB06 Semantic Invariants

- `SB06-INV-01` — UI gateways call in-process LlmChats application contracts and never loop back through HTTP or SSE.
- `SB06-INV-02` — Read projections exclude the system prompt; editor/mutation access requires Manage.
- `SB06-INV-03` — Read, Manage, and Execute are independent typed permissions mapped by Web composition to existing API policies.
- `SB06-INV-04` — Disposing a follower event session never requests durable operation cancellation.
- `SB06-INV-05` — Durable cancellation occurs only through an explicit Execute-authorized cancel action.
- `SB06-INV-06` — Cursor duplicates are ignored; cursor/retention gaps clear transient text and force authoritative refresh.
- `SB06-INV-07` — UI failures are sanitized and do not expose provider bodies, application error messages, secrets, prompts, or request fingerprints.
- `SB06-INV-08` — Provider presentation is resolved through `ILlmChatProviderResolver` and exposes only allowlisted neutral values.
- `SB06-INV-09` — The UI project has no Web, Persistence, EF, Agent runtime, tool, skill, voice, or service-locator dependency.
- `SB06-INV-10` — No page route or navigation item is activated before later behavior subbundles complete.
