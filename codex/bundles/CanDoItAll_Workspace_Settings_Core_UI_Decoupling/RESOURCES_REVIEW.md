# Resources and Memory review

## Review basis and limits

Reviewed implementation `20d816dab0f10d4b55efe4b8ff7e0f6af26dc32d` follows Memory correction
`9dac16414e38774eb7eb96d22cde7480bb31f9ea`. The compare against the previous Memory commit
contains the archived input, that correction and Resources implementation. [EV01, EV02, EV03]
The source ranges read are explicit in [SOURCES.md](SOURCES.md). This was source review,
not a .NET build, executed regression, browser session or rerun of the implementer's evidence.

## Preserve

ME-R1 is present: a new accepted provider revision removes an incompatible current query
result while retaining the original submission. Auto-derived feedback context is distinguished
from manual input. Tests cover visible result then refresh, held result then own refresh,
replacement/removal, same-revision/failed reads and manual edit-away-and-back. [ME01, ME02]

Resources uses independent Registry/Browse controllers shared by real production adapters
and the sandbox. Registry retains the EditContext, field/configuration revisions, accepted
identity before read-back and per-target mutation receipts. Browse detaches acquired leases
before asynchronous release and fences source/preview publication. Real adapters keep project
admission, canonical profile and source/revision/provider checks with the existing owners.
The Configuration.UI child cuts the previous Workspace renderer edge. [RS01-RS09]
These are useful boundaries to retain, not reasons to replace them with a generic controller.

## RS-R1 — catalog/reference readiness overwrites exact editor readiness

Priority: medium, required before the next extraction. A source-proven state-transition defect;
runtime reproduction is required first. No authorization bypass or persisted data corruption
has been demonstrated by this review.

`ResourceRegistryController.RefreshAsync` sets `Access = Ready` after a successful resource
list read. That result says nothing about a simultaneous or failed `GetAsync(id)`.
`LoadEditorAsync` uses the same `Access` property for Loading/Failed. `SelectAsync` treats
same ID plus Ready as a no-op; mutation admission only rejects Loading, not an uninitialized
exact editor. The route maps this same property into Agent context access. [RS01, RS09]

### Reproduction A: pending exact editor, successful independent refresh

- Initialize the catalog and a valid editor A.
- Select B and hold its `owner.GetAsync(B)`; retain the selection task rather than blocking the dispatcher.
- The current draft is an ID-only B placeholder; its editor load is still pending.
- Run the real header Refresh and complete the list/reference reads.
- Current code publishes Ready. The view no longer tells the truth about exact editor availability;
  a same-ID select is treated as completed and the handler's Loading gate no longer applies.
- Complete B and verify that only this exact successful acquisition may establish editor readiness.

The real refresh control is independent and available while the editor is pending. This is
normal Blazor asynchronous re-entry, not simultaneous execution of two CPU threads. [RS03, EV04]

### Reproduction B: failed editor, successful independent refresh

- Open a resource route or select B; let catalog/reference loading succeed but fail `GetAsync(B)` once.
- Retain the ID-only B draft and the explicit editor error.
- Click Refresh; list/reference reads now succeed.
- Current code changes Access to Ready although no editor was fetched successfully.
- Click B in the actual list. `SelectAsync` returns immediately because the ID matches and Access is Ready.
  The empty placeholder cannot be repaired by that advertised exact-selection retry; selecting away
  and back works around it but is not a correct recovery contract.

The same family includes an initial route read that fails before an editor is loaded. Retrying
catalogs must not mark a default placeholder as a fully loaded stored record. [RS01, RS03, RS07]

### Required correction

Track the selected editor's acquired/loading/failed state and origin independently of catalog
and optional reference loading. A simple explicit state is enough. Derive the externally visible
readiness and mutation admission from the facts they actually require. A same-ID selection is
only a no-op for an acquired editor, not for pending/failed/uninitialized placeholders.

A reference refresh must not reset a valid dirty editor or force a redundant detail read. A
failed detail must have a working exact retry. Never fill missing details with default values
and treat them as loaded. Failures of unrelated references must remain distinct from selected
editor loading; preserve the existing route/lifetime checks and historical-resource cleanup.
Do not indiscriminately reject every operation whenever any reference is stale.

Guard UI controls and direct handlers. An unacquired placeholder must not dispatch Save/Delete,
regardless of whether a test fills its fields to look valid. A historical resource that was
actually acquired retains its existing supported cleanup behavior. Keep backend authorization
and project admission enforcement unchanged.

### Required tests

Use the actual shared controller, existing `ResourceScenarioStore` hold pattern or a narrow
owner decorator and the top-level renderer. Prove A, B, initial route failure/recovery,
cancellation-ignoring A-B-A, same-ID no-op after success, dirty/context/config preservation and
failed optional references. Add a real routed-host test that verifies Agent readiness stays
Loading/Failed until the exact editor is acquired. Assert owner-call counts and captured IDs;
no fixed sleeps or source-string assertions as behavioral proof.

Existing tests already cover saved-ID retention, postcommit warnings, unknown review, stale
editor A-B-A and route mismatch. Preserve them. They do not cover the independent reference
refresh orderings above. [RS07]

## Other areas

No second blocking finding is asserted from this focused review. The reviewed Browse lifecycle
and adapter paths remain important regression surfaces, not an exhaustive certification of all
file/provider/platform behavior. Do not invent unrelated fixes to fill a quota.
