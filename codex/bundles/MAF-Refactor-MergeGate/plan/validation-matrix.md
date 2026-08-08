# Validation matrix

| Area | Mandatory scenario |
|---|---|
| Authority parser | absent legacy vs valid current vs malformed current |
| Authority identity | agent/profile/generation/scope mismatch |
| Source authority | DI ownership, duplicate keys, unknown source |
| Policy pipeline | effective context returned and used downstream |
| Process policy | unrelated clone contributor cannot satisfy enrichment |
| Process cleanup | organization execution + project-scoped durable lease |
| Process cleanup | completed, failed, continuation, concurrent retry |
| File CAS | two independent scoped store instances |
| File hygiene | temp cleanup on fault/cancel |
| Conversation rollback | failed/crashed Adopt restores provider + acceleration |
| Active turn | rename rejected; exact entry/turn invariant |
| Capacity | no provider call with fewer than two slots |
| LLM retry | aggregate usage on success and typed failures |
| Workflow usage | known failure usage preserved |
| SB15 activation | no production registration/consumer |
| Runtime state | v1/v2 restore and approval fail-closed |
| Floating context | Canvas -> Gantt next-turn snapshot |
| Approval | mixed per-proposal decisions |
| Build/regression | clean Release, full Unit and Integration comparison |
