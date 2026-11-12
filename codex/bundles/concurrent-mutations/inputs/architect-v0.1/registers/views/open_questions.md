# Open Questions

Generated from the adjacent JSON register. Narrative documents may also require review after decisions change.

| id | question | review_owner | decision_gate | recommended_default_or_action | status |
| --- | --- | --- | --- | --- | --- |
| Q01 | What is the complete enabled Projects writer perimeter on the implementation branch? | Projects + architecture | P00 | No unknown enabled writer before pilot implementation; map actual callers. | open |
| Q02 | Which existing durable journal/record can prove Projects ordinary save outcomes? | Projects + runtime/persistence | P01 | Reuse only if atomic with effect and valid for UI/API/tool retry; otherwise minimal owner ledger. | open |
| Q03 | Which current dataset/profile epoch covers destructive restoration without rotating on restart? | Control plane + migration | P01 | Inspect current implementation; add maintenance-owned fence only if needed. | open |
| Q04 | Can the current small Search participant join source transaction within accepted latency/lock bounds? | Projects + Search + persistence | P01 | Prefer transactional participant if measured bounded; otherwise completed durable rebuild protocol. | open |
| Q05 | What is the complete lock-class order across source/claim, Projects, Workbench and storage gates? | Persistence + runtime owners | P01 | Preserve existing source-first rules, no universal alphabetical replacement. | open |
| Q06 | Should Projects no-op save advance UpdatedAt/activity despite no content change? | Projects product owner | P03 | Preserve existing external meaning until explicitly changed; do not stamp unrelated effects. | open |
| Q07 | Where does the API-only UI live and how will its original-stamp/result contract be validated? | API/UI owners | P00 | Identify actual repository/contract consumers; no assumption main Blazor tests cover it. | open |
| Q08 | Which single planning operation and material facts define the first basis pilot? | Workbench planning + governance | P04 | Project objective/definition and bounded relevant canonical task set; refine from actual tool. | open |
| Q09 | Which manual edits retain automation, pin a value, or transfer control? | Product/governance | P04 | Explicit action/scope, no global human-always-wins rule. | open |
| Q10 | What are maximum atomic batch/basis sizes and supported staged alternatives? | Relevant owner + performance | P04 | Ratify from current bounds and measured workloads; reject rather than truncate. | open |
| Q11 | How long can clients/checkpoints/queued work replay operation identities? | Runtime + operations + privacy | P02 | Set supported horizon and dedupe detail/compact retention before public promise; extend for later runtime lanes. | open |
| Q12 | What rollout scope in P05 is approved, given active extraction? | Architecture + individual owners | P05 | One small fully mapped owner at a time; graph wave separately scoped. | open |
| Q13 | Which storage/provider operations offer actual conditional replacement/idempotency/lookup? | Storage + integration | P06 | Capability matrix per actual implementation, weaker guarantees explicit. | open |
| Q14 | Which old process records require re-admission rather than replay? | Process runtime + governance | P07 | Readable history retained; never synthesize absent trusted basis. | open |
| Q15 | What measured SLO/budget and closure environments are actually available? | Operations + QA + performance | P00 | Candidate relative thresholds are discussion defaults; null measurements block claims. | open |
| Q16 | Does any binary-only consumer require compatibility when contracts move assemblies? | UI extraction + API/package owners | P00 | Verify actual evaluated graph/consumers; namespace equality alone insufficient. | open |
