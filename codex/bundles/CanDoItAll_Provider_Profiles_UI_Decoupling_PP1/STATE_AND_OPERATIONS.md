# State, target and effect contract

## One ownership model, independent lifetimes

Keep catalog availability, exact editor acquisition, pending local writes, source-managed facts, active section, nested Thinking dialog and auxiliary text separate. A list read is not proof that the selected editor was acquired. A changed host/profile retires authority and callbacks, not an already accepted durable effect.

Use immutable activation/request identity where it closes a real race. Do not introduce a new generic lifecycle framework. Cancellation sources are canceled by their owning transitions and disposed by the admitted operation after it finishes. Cleanup is identity-based and unconditional with respect to whether a result may still publish.

### Required transition contract

| Transition | Required behavior |
|---|---|
| Same acquired provider, another section | Same logical draft/context; raw fields and nested draft survive according to explicit overlay policy; only selected integration data is loaded |
| Same acquired row clicked again | No implicit reset, target cancellation or native read merely due to cosmetic selection |
| Metadata refresh | Retain current draft; report stale/error independently; do not bless a missing detail as acquired |
| Failed same-ID acquisition retry | A real exact-target read, not no-op based only on ID |
| Different provider or New | Explicitly retire old editor/presentation; preserve unresolved native attempts in the existing recovery owner |
| Commit | Adopt returned identity/version and accepted unchanged fields; retain later editor input and any actual partial-warning state |
| Known rejection/conflict | Preserve correction/context and original version; distinguish from a genuinely unknown result |
| Unknown | Existing exact candidate/attempt retained; no blind duplicate write; verification is a read |
| Delete success | The exact original target becomes unavailable; successor selection remains usable; no fabricated rollback |
| Source notification | Metadata can update without resetting unrelated local editors; imported read-only replacements remain pinned to their source identity |
| Retired read or nested callback | No successor mutation, toast, selection jump, focus or busy-state cleanup |

## Native semantics matrix

| Action | Source and durable meaning | Retry rules |
|---|---|---|
| Save | Complete captured local editor, native validation/concurrency and registry | Known rejection can be corrected; existing verified-absent retry keeps candidate identity; unknown cannot become blind replay |
| Delete | Exact stored target, current native blocker/reference checks | Retain returned commit/unknown evidence; read-back does not delete again |
| Health | **Stored** provider config; local result can change persisted diagnostics/revision; source-managed path does not write local provider settings | Diagnostic is not automatically repeated after read-back failure; preserve current supported operation-owner recovery semantics |
| Load from provider | Captured **draft** endpoint/config/secret reference; models/prices prepared locally | Not a durable provider Save; refused/stale result leaves current draft unchanged |
| Thinking Apply | Captured local row/dialog modifies only its owner's configuration draft | No provider write or model request; current target/kind/transport/config must still match |
| Shared-source refresh | Real retained owner; it may alter imported profiles | Use original typed delivery and exact completion; do not rerun source effects merely to repair a UI read |

## Reconciliation detail

Use the actual submitted values, not just the initial loaded record, to decide which fields can accept canonical read-back. If a value remains unmodified after dispatch, accepted normalized values can be shown. If typed later, preserve it. Lists need semantic per-row reconciliation and raw-input preservation; array equality or row index alone is insufficient. Do not advance a concurrency token from a competing configuration without preserving the existing native conflict semantics.

Display mirrors should be projections of the owned draft or origin-bound editing buffers with explicit flush/reconcile rules, not hidden second authorities. Capturing one field then reading another mutable field after await is forbidden. An edit-to-visible optimization must not introduce validation gaps or replace the complete native model with an incomplete four-tab DTO.

## Outcome disclosures

Logs and user messages have different audiences. Runtime/transport exception bodies, secrets and full configuration submissions must not enter toasts, route state, screenshots or shareable evidence. Use existing safe owner error classifications, retain correlation IDs and record precise phase privately. A blanket “Save failed” after a known commit and a blanket “Unknown” after a known rejection are both wrong.
