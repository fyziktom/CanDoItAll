# Decisions

Generated from the adjacent JSON register. Narrative documents may also require review after decisions change.

| id | title | status | recommendation | decision_gate |
| --- | --- | --- | --- | --- |
| D01 | Preserve fact-owner application boundaries | proposed | Strengthen existing owner operations and typed adapters; do not create a global mutation engine. | P00 |
| D02 | Projects aggregate edit scope | proposed | Initially protect project scalars plus phases/options with one content stamp; no chat/runtime progress fan-in. | P01 |
| D03 | Physical token convention | proposed | Reuse application-managed GUID for Projects after context/writer proof; preserve other owners' existing conventions. | P01 |
| D04 | Coherent edit read | proposed | Use a bounded coherent read/snapshot for the editable aggregate and return its original stamp. | P01 |
| D05 | Transaction policy by intention | proposed | Retain current guarded paths for pilot, add original-state predicate; choose cheaper modes only with separate proof. | P01 |
| D06 | Scope and authority order | proposed | Preserve source-before-owner transaction and establish lock-class ordering with full bounded keys. | P01 |
| D07 | Projects safe retry result | proposed | Reuse an adequate durable owner record or add a minimal owner-local receipt in the effect transaction; link existing journals. | P01 |
| D08 | Projects search consistency | proposed | Prefer small same-DB projection in source transaction if bounded cost; otherwise complete durable ordered rebuild within pilot scope. | P01 |
| D09 | UI snapshot lifecycle | proposed | Apply existing host/submission/live reconciliation semantics and add draft-preserving conflicts. | P02 |
| D10 | Public contract cutover | proposed | Reject missing/stale original conditions on protected updates; convert or disable all enabled callers. | P02 |
| D11 | Selective merge and intentions | proposed | Introduce actual domain commands, stable row identity and opt-in independent scalar merge. | P03 |
| D12 | Required decision-basis ownership | proposed | Trusted operation/runtime defines required facts and membership witnesses; model can add but not omit dependencies. | P04 |
| D13 | Source publication guarantee | proposed | Use protected current-at-publication for material pilot objective/mandate facts; explicitly label pinned/remote weaker contracts. | P04 |
| D14 | Approval binding | proposed | Bind exact proposal effect, material basis and authority scope with versioned semantic identity; reapprove material changes. | P04 |
| D15 | Manual override and takeover | proposed | Explicit domain policy distinguishes edit-with-automation, pin/override, pause and takeover; owner fences retired attempt. | P04 |
| D16 | Existing owner-specific protection | proposed | Preserve task expected-state/assignment checks and each owner's append/versioned-definition semantics. | P05 |
| D17 | External effect semantics | proposed | Use specific driver/provider conditional/idempotent protocol; uncertainty and compensation remain explicit. | P06 |
| D18 | Dataset lifecycle and operation retention | proposed | Reuse proven profile/activation epoch or add maintenance-owned restore fence; distinguish restart from destructive restore; preserve safe replay horizon. | P01 |
| D19 | Shared abstractions threshold | proposed | Extract only demonstrated feature-neutral mechanics/vocabulary, never feature semantics into generic UI or infrastructure. | P03 |
| D20 | Performance decision gates | proposed | Measure SQL fan-out, locks, allocations and replans against matched baselines; candidate budgets require ratification. | P00 |
| D21 | Saved process compatibility | proposed | Keep immutable execution definitions distinct from live target publication and current claims; explicit legacy-basis policy. | P07 |
| D22 | Validation and moving-branch evidence | proposed | Refresh logical owners/callers and current tests at each phase; canonical broad lanes only at required triggers. | P00 |
