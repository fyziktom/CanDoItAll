# Selected Process source publication fence

Status: implemented after signed lock-order checkpoint `ad8793500`; the
[validation record](../validation/2026-10-20-selected-publication-fence.md) has 126 distinct
passing cases across an exact one-test-file repair bridge. Actual inherited Workbench
publication and saved-effect dispatch remain closed.

## Gap and ownership

The existing child fence proves current generation, allocation, runtime phase and claim
lifetime. It does not compare the complete original selected source. The durable selected
query already validates the canonical child/material binding, original prepared policy,
executor and supported contract pair, but is exposed only as a concrete persistence type.
Workbench cannot depend on Process persistence or the Process product module.

Expose that existing query through a narrow Process Application port, implemented by the
same scoped persistence query. Extend the existing publication fence with an explicit
selected-source entry accepting original binding, original dispatch observation and the
registered execution/content contracts. Retain the bare child fence for its existing
callers; it must not be substituted for the selected entry in the business adapter.
No module/project reference or product-specific content interpretation is added.

The query/fence proves durable Process identity and liveness. It does not grant current
Agent/operator permissions, Project write authority or active dataset access. Those remain
with the effect owner and its held catalog/Project/finite-operation protections. The future
Workbench adapter must compare original scope/profile/namespace with its admitted target
and current effect transaction before invoking this fence.

## Transaction and race rules

Require the effect owner's existing PostgreSQL ReadCommitted transaction. The effect owner
has already acquired all original Project gates. Acquire the same Process root advisory
key, validate the exact child, then lock the source runtime and assignment rows for share.
The assignment repair store can update rows without acquiring a root advisory key, so the
root alone cannot protect a readiness/executor observation through commit. Reuse the
legacy source guard's row-lock protocol through a small internal helper, preserving its
existing rejection behavior. Runtime step/claim replacement belongs to the root-fenced UOW.

Read the selected source and canonical material through the enlisted context. Compare the
complete original source value, ignoring only its two observation fields (observation time
and observed-current flag). Comparing only a copied fingerprint would allow a caller to
substitute a permission/capability field alongside that fingerprint. The canonical original
target envelope will bound and preserve this value in the business adapter.

Dispatch requires a currently live selected source and the original active claim. Manager
review requires its existing waiting-for-review phase, open retained child and absence of an active/live claim;
it must not revive dispatch. Both compare the original source/material identity. Recheck
the runtime/claim time condition after all asynchronous source reads and row waits, so an
earlier positive check cannot outlive the claim. The selected path requires an actual saved
source authority; the legacy unprepared/authority-free fixtures cannot become business
mutation authority. Current manager permission remains a separate effect-owner requirement.

Do not acquire a catalog lease from inside this transaction or while holding another
selected catalog snapshot. Do not stage business rows before the complete source fence.
Cancellation and rollback release the existing transaction locks. No cache, retry loop,
global lock or new lock registry is introduced.

## Validation and integration

Use actual PostgreSQL and the existing isolated fixture. Prove composed port resolution,
trusted prepared source success, full-value source substitution rejection, canonical
material/contract substitution rejection, current dispatch versus manager review, current
assignment repair exclusion until transaction end, root/generation quiescence, and expiry
after an awaited source-row lock. Confirm rollback and independent root progress where
the new locks matter. Repeat the existing selected source and legacy fence/guard tests.
Keep historical reads and generic legacy refusal behavior intact.

Extend test fixtures with explicit optional trusted source admission, preserving existing
legacy defaults. Capture the real operator authority and use the existing configured
coordinator/launch policy for the initial trusted commit; do not fabricate a source or
silently bypass admission. Build affected projects, confirm exact discovery, complete
portability/architecture review, freeze inputs, observe execution/resources and retain
failures as well as repairs before a signed checkpoint.

The later business integration still needs the explicit enlisted publisher hook, original
inherited target/context codec, separate inherited participant, parent-owned saved-effect
continuation and MAF context/purpose support without fabricated governance. Saved-effect
provider and receipt-owner routing must remain unambiguous. Process/Workflow producers,
atomic outcome/accounting, manager authorization and settings are not activated by this
fence alone.
