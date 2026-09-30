# W1 — Storage Recovery and owner continuation

## Complete surface, not a passive demonstration

Extract the actual `StoragePlacementRecoveryDialog` body, navigation, detail and action controls. Retain the existing production opening path from Storage with a captured saved StorageId or explicit all-catalog scope. Preserve both feeds, their independent offsets and PageSize 8 semantics. A scanned page can be empty and still have a next offset. Never use row count alone to infer exhaustion. [WS10, WS13]

Include inspection, explicit refresh, file-placement reconciliation, external-termination verification, cancelled-run receipt reconciliation and both offered Workflow continuation actions. Render restricted/unavailable/blocked states honestly. The current component fixture only implements Process-style owner follow-up; it cannot substitute for Workflow proof. [WS12, WS13]

## Owner boundary

Leave `IStoragePlacementRecovery`, `IStoragePlacementOwnerContinuation`, their actual access adapters and persistent owners in their present layers. Map safe display values into a dedicated leaf contract if the current assembly closure is heavy. Existing public wire property names, enum values and the original request identity remain unchanged.

A rendered action captures the complete original context, placement intent and expected owner output. For Workflow this includes RunId, OccurrencePath, Slot and Fingerprint exactly as observed. Never recreate these from a title, current row, latest workflow version or the selected project. Backend revalidation still decides whether that original output can continue. [WS11, WS12]

Opening, paging, inspecting or refreshing must not reconcile anything, launch a run, send an upload, complete an asset, modify a permission or attest that an external transfer ended. An explicit continuation may finish the already-prepared native asset or record its existing receipt. It must not regenerate content, restart the model or invent a new intent.

Do not replace failed authority with local admin access. Keep read-only, imported-history, changed original project lifetime, disabled/missing storage, changed target fingerprint and cancellation outcomes. A read-only UI must have no actionable mutation controls and direct handlers must refuse as appropriate; owner negative tests prove the actual boundary.

## Lifetime and result rules

Separate the selected detail from list positions. Capture the opening catalog filter; changing the underlying catalog editor must not silently retarget an already-open Recovery view. Define a new explicit lifetime when the actual Recovery target changes. Same-row inspection must not replay an effect.

Read errors retain a clear unavailable state, not a true empty page. If keeping old rows, label their original context and disable stale actions until current owner evidence is obtained. An old success, denied read or finally block must not change a successor dialog. Close/dispose must release subscriptions and exact request sources even when a dependency ignores cancellation; cleanup cannot depend on UI publication being allowed.

Current access/context failure may retire the current sensitive action scope. Stale failure from an older request must not retire the new scope. A schema/version-independent availability flag must not masquerade as admission for a particular original intent.

If the command returned known progress and the subsequent list/detail refresh fails, retain the command's result and exact identity. Retry observation only. If owner acknowledgement is unknown, preserve that uncertainty and require the existing recovery protocol; do not blindly replay. Existing domain continuation may be repeat-safe under exact identity, but prove that from the actual owner instead of assuming it.

## Scenarios and concrete tests

Use the same real renderer and state code in production and the new sandbox. Scripted source data must model both feeds, safe context, exact outcomes and optional Workflow intent; simulation is labelled and bounded. No live driver or owner service is registered in the sandbox.

Required deterministic cases:

| Transition | Required observation |
|---|---|
| Open/read/refresh/paging | No command, upload, model or native commit; both feeds advance correctly |
| Empty continuation page with next offset | Next is still available; subsequent page can contain data |
| Inspect A, then B, then A while old read waits | Only the active exact request publishes |
| Close/reopen or profile/caller changes during read | No stale rows/actions, no disposed-CTS failure or retained child |
| Reconcile while re-entrantly clicking/pressing Enter | One admitted command for the target; exact original context |
| Command progress then follow-up read failure | Progress and identity retained; refresh does not re-execute |
| Workflow prepared-asset completion | Exact expected output, native asset and acknowledgement proven independently |
| Cancelled-run receipt reconciliation | No resumed model/tool run; original run and receipt only |
| Denied/changed-owner/imported/missing evidence | Correct refusal and zero unauthorized effect |
| External termination | No dispatch until explicit true acknowledgement; false/direct stale call refused |

For the external-termination positive case, use a task-owned controlled transport fixture whose dispatch can actually be observed as stopped. Do not assert a real external FTP job stopped merely to make the UI test pass. A sandbox demonstration is not production evidence for this attestation.

Production proof must use a real private PostgreSQL-backed prepared placement and actual owner continuation. Reuse existing integration fixtures to seed a controlled interrupted state, then invoke the target action through the UI. Read the persisted storage/native/workflow receipts and content hash afterwards. Driver/model counters must show no second upload or generation caused by recovery. Keep negative owner and HTTP tests for original project life, wrong fingerprint and revoked access.

Record a completed W1 boundary with actual dependencies and fresh source/published sandbox browser results. Then continue W2; do not stop after this stage.
