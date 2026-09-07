# Selected patterns and rejected alternatives

| Force | Selected pattern | Rejected simpler/competing alternative | Types/projects and proof |
|---|---|---|---|
| Independent async list/detail and target replacement | Per-component session with lane-specific request ownership and captured identity | One isBusy plus agent-only generation fails run races; circuit-scoped session leaks between owners | One Module session; public stale success/error/finally and disposal tests |
| Catalog and canonical history reads need deterministic testing | Cohesive feature read adapter over the existing registered workspace | Interface per method or moving canonical query code; retain delegate alternative only if G00 proves it simpler | One Module read contract/adapter, no project; registered DB-backed fixture and token propagation |
| Real render subtree must work without services | Controlled immutable presentation and typed intents | Injecting a controller into UI; copied mock renderer; duplicating selected IDs | Surface/state/mapper in Module then moved; no-service render/intents/collection independence |
| Current list remains usable when detail fails | Separate accepted list/detail state with explicit desired target and manual-selection revision | LoadEverything transaction; silently retain A as B; automatically override a manual selection | Session transitions; missing/stale/wrong-target and detail-only retry proof |
| Shared real child consumers block honest extraction | Move the single pure timeline and metric components into existing UI | UI -> broad Components; duplicate children; moving whole runtime dialog | Consumer rebuild/render tests and evaluated acyclic graph |

No mutation outcome taxonomy, event bus, outbox, generic async state framework, factory hierarchy or catch-all application controller is warranted. Constructor/interface/member counts are observations, not architecture assertions. A mapper owns real presentation conversion; it must not forward every original record/property without reducing authority and mutable ownership.
