# Fulfilled vs open matrix

| Area | Current status | Open concern | Follow-up owner |
|---|---|---|---|
| SQLite typed model removal | Mostly complete | Allow explicit quarantine terms only | SB01/SB02 |
| Main DbContext hot path | Improved | `DatabaseRuntimeState` still has dead switch/drain concepts | SB03 |
| Canonical DB truth | Partially improved | Runtime vs pending restart profile needs explicit model | SB02 |
| Profile activation | Restart-first | `EnableMaintenanceHotSwitch` option is misleading | SB03 |
| Profile-specific maintenance contexts | Exists through `CreateDbContextForProfileAsync` | Interface name and pooling/caching boundaries unclear | SB04 |
| Automation delivery claim | PostgreSQL batch claim exists | Claimed deliveries processed sequentially; same-envelope race must be considered before parallelism | SB05 |
| Process outbox claim | PostgreSQL batch claim exists | Claimed records processed sequentially; per-run/step partitioning needed | SB05 |
| Connector outbox claim | PostgreSQL batch claim exists | Claimed commands processed sequentially; external connector partition limits needed | SB05 |
| Process dispatch claim | Durable fields exist | Renewal failure does not abort; final transitions not proven claim-token gated | SB06 |
| Candidate loading | Works | Heavy full-run candidate loading before claim; N+1 execution-run calls | SB07 |
| Validation | Focused tests passed per report | broad suite/environment blocker remains | SB08 |
