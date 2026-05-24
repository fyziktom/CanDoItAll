# DB bottleneck candidates

| ID | Area | Current suspected bottleneck | Required audit/fix |
|---|---|---|---|
| B1 | DbContext factory | Profile resolution + options creation per context | Canonical pooled runtime factory |
| B2 | Runtime switching | Lease/drain around every context | Restart/maintenance-first profile activation |
| B3 | Automation delivery | Single-row claim loops / no batch SKIP LOCKED | Batch claim with `UPDATE ... RETURNING` |
| B4 | Process dispatch | Static per-step semaphore wraps long-running execution | Durable PostgreSQL execution lease |
| B5 | Background jobs | In-memory queue for tracked work | Keep explicit transient queue or add durable PostgreSQL queue |
| B6 | InMemory profile | May be saved/listed/transferred as data source | Restrict to explicit override/test |
| B7 | Database transfer | Opens arbitrary source/target profiles | PostgreSQL-only transfer sources/targets |
| B8 | Legacy quarantine | Hidden retired-provider strings | Explicit allowlist and tests |
