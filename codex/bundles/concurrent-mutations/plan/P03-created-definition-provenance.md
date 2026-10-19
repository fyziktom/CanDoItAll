# Original-created definition targets: integration design

Status: source integrated for validation, not accepted. The preceding exact definition
claim guard passed its separate 24-case candidate. This vertical needs its own proof.

The remaining functional gap is an Update of a project created earlier in the same
execution, or an organization-scoped creation under that new project. The initial source
ceiling correctly omits the new target. Current grants alone must not broaden that ceiling.
Hierarchy already derives a bounded extension from the exact earlier approved creation,
its owner receipt, and the native consumed reservation. Definition operations must use the
same derivation, including replay, cached-result disclosure, interrupted/cancelled
reconciliation and historical review. Merely adding the live write path is insufficient.

## Responsibility and dependency direction

1. Generalize the existing Workbench creation-provenance resolver to target collections
   and an operation-specific original Agent admission. Keep the maximum at three targets
   (definition operations need at most one existing target) and preserve the one bounded
   immutable-prefix scan. Projects continues to own native namespace, reservation and
   lifetime checks, including the final enlisted recheck.
2. Remove its constructor dependency on the definition recovery provider. Pass the existing
   typed original-creation lookup delegate from each caller. This prevents a DI cycle when
   definition recovery also needs derived provenance. Do not resolve services dynamically,
   add a generic coordinator, or copy owner receipt logic.
3. Return the existing typed Agent/Process source admissions. For definition operations,
   preserve the actual Update/ProjectCreation/SubprojectCreation domain and tool identity.
   The hierarchy resolver must not accidentally grant the hierarchy domain to an Update.
4. Ordinary write admission supplies the exact live claim's prior observation. Completed
   result disclosure supplies Core's completed-result prior observation. Reconciliation
   uses the actual pinned/cancelled receipt claim. Historical lookup uses the active
   read-only claim and its sealed current catalog basis. None can be substituted for a
   dispatch claim.
5. Resolve read provenance lazily inside the owner's receipt-access callback. An expired,
   missing or different-dataset operation must keep its owner classification without
   unnecessarily requiring a still-retained creation receipt. Missing provenance is
   unavailable, current permission revocation is denied; do not silently turn one into the
   other. The creation lookup delegate must accept only creation tools, preventing
   recursive Update resolution. Creation receipt disclosure uses its exact own reservation;
   it does not recursively read arbitrary parent histories.

## Scope and failure semantics

The original session, dataset namespace, epoch, caller, governance/source project and full
Process dispatch must match. Current target grants are necessary but never sufficient.
Imported, legacy-unbound, unconsumed, future, foreign-session or unapproved evidence cannot
derive access. No returned JSON proves provenance. A conflict or unavailable original
receipt preserves the original operation; it does not manufacture a replacement intent.

Keep the current Project-scoped ceiling: the original project and its directly created
children. A grandchild is not silently admitted. Organization scope may create under an
originally created target only with the original and current creation permissions. Process
delegation retains the original initiating project and current executor target grant.

Fast path: if the original ceiling already covers the target, do not scan its journal or
look up unrelated creation receipts. Capture one immutable prefix per needed operation and
reuse it; at most three creation lookups and one bounded native reservation/lifetime batch.
This work does not remove the existing coarse publication-lock cost. Selected-key admission
and the matched SQL/contention cohort remain separate activation requirements.

## Required proof

- Actual registered Agent tools: create then update; child create then update in Project
  scope; organization create then create child; the same frozen provider source must be used.
- Actual Process tools: different initiating/executing Agents, original source permission,
  executor target grant, expired or replaced claim refusal, both accepted receipt families.
- Live execution, completed cached disclosure, lost-ack replay, pinned interrupted and
  cancelled receipt reconciliation, historical pages and actual seed/Web restart.
- Revocation at publication, recreated IDs, deleted/imported/unconsumed creation evidence,
  wrong epoch/session/approval and future proposals. Assert no effect/receipt on refusal.
- Existing hierarchy/provenance and definition regression lanes. No project-reference
  changes, no new schema, full portability enforcement and resource closure.

Changing only the operation-specific source derivation would leave historical and
reconciliation gaps; all caller modes above must ship as one reviewed vertical change.
