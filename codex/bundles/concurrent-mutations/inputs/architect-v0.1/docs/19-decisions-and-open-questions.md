# 19 · Decisions, alternatives and open questions

All entries are **proposed**, not accepted on the user's behalf. The machine-readable [decision register](../registers/decisions.json) and [open questions](../registers/open_questions.json) record stable IDs, gates and reviewer roles. This is a review agenda with defaults; it does not delay producing the architecture package until every answer exists.

## Decision register

| ID | Decision | Recommended direction | Gate |
|---|---|---|---|
| D01 | Preserve fact-owner application boundaries | Strengthen existing owner operations and typed adapters; do not create a global mutation engine. | P00 |
| D02 | Projects aggregate edit scope | Initially protect project scalars plus phases/options with one content stamp; no chat/runtime progress fan-in. | P01 |
| D03 | Physical token convention | Reuse application-managed GUID for Projects after context/writer proof; preserve other owners' existing conventions. | P01 |
| D04 | Coherent edit read | Use a bounded coherent read/snapshot for the editable aggregate and return its original stamp. | P01 |
| D05 | Transaction policy by intention | Retain current guarded paths for pilot, add original-state predicate; choose cheaper modes only with separate proof. | P01 |
| D06 | Scope and authority order | Preserve source-before-owner transaction and establish lock-class ordering with full bounded keys. | P01 |
| D07 | Projects safe retry result | Reuse an adequate durable owner record or add a minimal owner-local receipt in the effect transaction; link existing journals. | P01 |
| D08 | Projects search consistency | Prefer small same-DB projection in source transaction if bounded cost; otherwise complete durable ordered rebuild within pilot scope. | P01 |
| D09 | UI snapshot lifecycle | Apply existing host/submission/live reconciliation semantics and add draft-preserving conflicts. | P02 |
| D10 | Public contract cutover | Reject missing/stale original conditions on protected updates; convert or disable all enabled callers. | P02 |
| D11 | Selective merge and intentions | Introduce actual domain commands, stable row identity and opt-in independent scalar merge. | P03 |
| D12 | Required decision-basis ownership | Trusted operation/runtime defines required facts and membership witnesses; model can add but not omit dependencies. | P04 |
| D13 | Source publication guarantee | Use protected current-at-publication for material pilot objective/mandate facts; explicitly label pinned/remote weaker contracts. | P04 |
| D14 | Approval binding | Bind exact proposal effect, material basis and authority scope with versioned semantic identity; reapprove material changes. | P04 |
| D15 | Manual override and takeover | Explicit domain policy distinguishes edit-with-automation, pin/override, pause and takeover; owner fences retired attempt. | P04 |
| D16 | Existing owner-specific protection | Preserve task expected-state/assignment checks and each owner's append/versioned-definition semantics. | P05 |
| D17 | External effect semantics | Use specific driver/provider conditional/idempotent protocol; uncertainty and compensation remain explicit. | P06 |
| D18 | Dataset lifecycle and operation retention | Reuse proven profile/activation epoch or add maintenance-owned restore fence; distinguish restart from destructive restore; preserve safe replay horizon. | P01 |
| D19 | Shared abstractions threshold | Extract only demonstrated feature-neutral mechanics/vocabulary, never feature semantics into generic UI or infrastructure. | P03 |
| D20 | Performance decision gates | Measure SQL fan-out, locks, allocations and replans against matched baselines; candidate budgets require ratification. | P00 |
| D21 | Saved process compatibility | Keep immutable execution definitions distinct from live target publication and current claims; explicit legacy-basis policy. | P07 |
| D22 | Validation and moving-branch evidence | Refresh logical owners/callers and current tests at each phase; canonical broad lanes only at required triggers. | P00 |

## Questions that block specific guarantees

### Q01 · What is the complete enabled Projects writer perimeter on the implementation branch?
Reviewer: Projects + architecture. Gate: P00.

Default/action: No unknown enabled writer before pilot implementation; map actual callers.

### Q02 · Which existing durable journal/record can prove Projects ordinary save outcomes?
Reviewer: Projects + runtime/persistence. Gate: P01.

Default/action: Reuse only if atomic with effect and valid for UI/API/tool retry; otherwise minimal owner ledger.

### Q03 · Which current dataset/profile epoch covers destructive restoration without rotating on restart?
Reviewer: Control plane + migration. Gate: P01.

Default/action: Inspect current implementation; add maintenance-owned fence only if needed.

### Q04 · Can the current small Search participant join source transaction within accepted latency/lock bounds?
Reviewer: Projects + Search + persistence. Gate: P01.

Default/action: Prefer transactional participant if measured bounded; otherwise completed durable rebuild protocol.

### Q05 · What is the complete lock-class order across source/claim, Projects, Workbench and storage gates?
Reviewer: Persistence + runtime owners. Gate: P01.

Default/action: Preserve existing source-first rules, no universal alphabetical replacement.

### Q06 · Should Projects no-op save advance UpdatedAt/activity despite no content change?
Reviewer: Projects product owner. Gate: P03.

Default/action: Preserve existing external meaning until explicitly changed; do not stamp unrelated effects.

### Q07 · Where does the API-only UI live and how will its original-stamp/result contract be validated?
Reviewer: API/UI owners. Gate: P00.

Default/action: Identify actual repository/contract consumers; no assumption main Blazor tests cover it.

### Q08 · Which single planning operation and material facts define the first basis pilot?
Reviewer: Workbench planning + governance. Gate: P04.

Default/action: Project objective/definition and bounded relevant canonical task set; refine from actual tool.

### Q09 · Which manual edits retain automation, pin a value, or transfer control?
Reviewer: Product/governance. Gate: P04.

Default/action: Explicit action/scope, no global human-always-wins rule.

### Q10 · What are maximum atomic batch/basis sizes and supported staged alternatives?
Reviewer: Relevant owner + performance. Gate: P04.

Default/action: Ratify from current bounds and measured workloads; reject rather than truncate.

### Q11 · How long can clients/checkpoints/queued work replay operation identities?
Reviewer: Runtime + operations + privacy. Gate: P02.

Default/action: Set supported horizon and dedupe detail/compact retention before public promise; extend for later runtime lanes.

### Q12 · What rollout scope in P05 is approved, given active extraction?
Reviewer: Architecture + individual owners. Gate: P05.

Default/action: One small fully mapped owner at a time; graph wave separately scoped.

### Q13 · Which storage/provider operations offer actual conditional replacement/idempotency/lookup?
Reviewer: Storage + integration. Gate: P06.

Default/action: Capability matrix per actual implementation, weaker guarantees explicit.

### Q14 · Which old process records require re-admission rather than replay?
Reviewer: Process runtime + governance. Gate: P07.

Default/action: Readable history retained; never synthesize absent trusted basis.

### Q15 · What measured SLO/budget and closure environments are actually available?
Reviewer: Operations + QA + performance. Gate: P00.

Default/action: Candidate relative thresholds are discussion defaults; null measurements block claims.

### Q16 · Does any binary-only consumer require compatibility when contracts move assemblies?
Reviewer: UI extraction + API/package owners. Gate: P00.

Default/action: Verify actual evaluated graph/consumers; namespace equality alone insufficient.

## Alternatives deliberately deferred

A full real-time document collaboration model might eventually be justified for simultaneous rich-text work, but it does not replace owner authority, valid planning bases or operation identity. Event sourcing can provide history but does not automatically solve stale commands. A universal optimistic-lock interceptor cannot discover all business aggregates. A message broker solves neither mutable source freshness nor external exactly-once semantics by itself. These are not first-pilot prerequisites.

Finer content scopes, shared-read/exclusive-write gates, receipt infrastructure extracted for multiple owners and notification-based invalidations remain optional evolutions after evidence. Record the triggering measurement and proof obligation before adopting them. The simplest correct solution to one owner is preferable to a general framework whose bypasses and lock order cannot be explained.
