# Requirements and behavior obligations

| Behavior | Required result / proof |
|---|---|
| Bootstrap | Catalog and secret metadata through one cohesive reads port; initial first provider remains selected |
| Selection | One semantic ProviderId; tree and editor agree; late A cannot replace B |
| New/reset | New defaults preserved; pending reads cannot replace the new draft |
| Core failure | No provider form or write actions; explicit error and Retry, same target |
| Secret failure | Explicit warning; existing unavailable reference remains selectable and unchanged |
| Lifetime | Superseded and disposed reads cancelled; token-ignoring late success/failure fenced |
| Sections | Connection, Prices, Runtime, Thinking, Sharing, History have explicit identities, independent of enum ordinals |
| Forms | Same EditContext across tabs; lazy history outside provider form; no implicit history reads |
| Existing effects | Save/delete/health/pricing semantics and source-managed read-only behavior preserved |
| Shared sources | Existing lazy overlay; external reload uses new read owner, backend unchanged |

Non-goals: mutation outcome redesign/cancellation guarantees, route serialization, history internals, provider backend changes, physical extraction, sibling changes, schema or shared CI changes.

Risk controls: replace one read owner at a time; deterministic suspended-read tests; keep existing production-composed provider scenarios. Core failures are intentional fail-closed corrections, not unsafe characterization acceptance.
