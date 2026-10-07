# Storage catalog source review

These are pre-extraction behaviors and risks, not regressions attributed to the completed
API change. Read ST01–06 at the actual checkout before choosing implementation details.
The reviewer did not execute them. Confirm behavior with owner and renderer baselines.

## Complete selected surface

ST01 renders catalog search/list, StorageSummaryCard, all three wizard steps (Identity,
Connectivity, Routing), FileSystem/IPFS/FTP settings, secret references, enabled/read-only
flags, display order, capability/health information, tracked routing purposes, Clear, Test,
Save, Delete and a Recovery launch. Preserve actual Steps/layout/shared controls and reachable
footers, not screenshots of static substitutions. Empty matches and unavailable catalog are
different states. A missing selected record does not remove the list or become New.

## Existing lifecycle risks

Load resets the editor; Edit assigns results without a generation fence; Save captures no
independent snapshot in the current page, refreshes global lists and replaces the editor;
Delete unconditionally resets; Test updates the passed model after awaits. The handlers have
no complete in-method admission/lifetime policy. Immediate text/raw numeric state is absent
from several fields. These must be covered during extraction rather than moved unchanged.

Test delayed A→B→A editor reads, Save followed by path/provider/purpose edits, New/selection/
close while waiting, errors and postcommit refresh. Capture the command before any asynchronous
owner validation. Keep the three-step position and EditContext when refreshing references.
Opaque pre-write owner state and presentation selection must not be conflated.

## Save is a multi-stage operation

ST02 does: catalog Save → ApplyDefaultPurposes → Activity. ST05 loops over tracked purposes
and saves each separately. A failure can therefore mean catalog committed with some routing
changes committed, not all-or-nothing success or total failure. Record only facts established
by the actual owner boundary. At minimum keep the catalog's acknowledged ID plus routing
complete/incomplete-or-unknown and Activity/read-back state. A thrown routing call may have
partially persisted; never report that no routes changed without proof.

UI retry of observation refreshes exact catalog/routing facts; it cannot replay Save, defaults
application or Test. An explicit subsequent operator action is different and must be labelled.
Do not introduce an outbox, make all these stages transactional, or add global locks merely
to simplify the result. Preserve existing routing selection/disable/priority behavior.

ST04 SaveCore can create a new row when the supplied ID no longer resolves, and a new row's
actual generated ID is what matters. ST02 GetStorage also falls back to a new template when
an exact target is absent. The new editor path must distinguish missing from explicit New
and must not resurrect/recreate a disappeared edited target. A pre-read outside the actual
write is not an atomic guarantee. Use the smallest owner-local exact-editor operation if
needed, without silently changing legacy upsert consumers or adding a general concurrency
protocol. Add disappearance-at-owner-boundary and actual row-count tests.

## Connection test is NOT a read-only command

For an unsaved draft, ST02 tests a constructed driver input and records Activity without
creating that storage catalog entry. For an existing ID it tests the current draft and then
saves configuration plus health/capability metadata. It does NOT merely test the stored row
or update a health timestamp. Preserve this shipped distinction and make the UI wording
explicit. Do not switch to the separate StorageConnectionTestService as a mechanical reuse:
ST06 proves that service's original-configuration behavior, which is a different contract.

Capture exact ID/configuration/secret reference before the driver await. Do not later use
mutable model.Id, selected provider or current database to persist a test of another target.
Treat Test as a potentially mutating/external operation in admission, retirement and receipts.
Health status belongs to the tested configuration; an edited endpoint must not inherit a
fresh Healthy badge for the old endpoint. Keep completed test facts as original observations
if the live draft changed. Failed health and unknown persistence are separate dimensions.

A retired view may cancel unadmitted work, not undo an acknowledged write. If the owner returns
known persisted configuration then Activity fails, retain that fact. If the driver completed
but its metadata write is unacknowledged, do not infer commit from health success. No automatic
network retest. Only private harmless filesystem tests and controlled remote drivers are
needed; remote fixture behavior must be labelled, not reported as live FTP/IPFS proof.

## Lazy owner side effects and profile identity

ST04 List/Get/Rules call EnsureBootstrapFileSystemStorageAsync. Those reads may ensure the
bootstrap row/rule and execute existing host-binding migration. Do not claim they are globally
write-free, remove them without an owner migration plan, or call them from render/keypress.
A UI refresh never replays the user's previous Save/Test; that does not mean the existing
owner initialization has no effects.

Pin the original database profile/generation using the existing canonical owner/factory and
write-fence conventions. Verify actual lifetimes before coding. Catalog, default-purpose
writes, health persistence and Activity must never drift into a new selected database midway
through a command. Preserve the existing rejection/retirement policy and original accepted
facts; no new database-switch implementation belongs here.

## Routing and general app behavior

Track the eight purposes from the real owner, not hard-coded second routing rules. Selection
may replace that purpose's workspace target; unselection only disables the existing rule
belonging to this storage. Disabled rules preserve unrelated fields. Project-specific rules,
alternatives, MIME/length filters and priorities are not this wizard's redesign target.
ST06 checks byte preservation, malformed unrelated configuration, exact matched alternatives,
real configuration round-trip and disabled-rule retention. Keep all of these.

Saving two different catalog rows can still affect the same workspace routing purpose.
Do not assume distinct storage IDs imply independent global routing. Within the view, admit
conflicting routing operations consistently and ensure old outcomes never overwrite newer
views. Do not claim a new distributed concurrency guarantee; preserve/inspect the owner's
existing guarantee and explicitly document any limit.

## Deferred integration seams

Recovery remains the existing host-owned dialog with captured StorageId and original-intent
semantics (ST07). No automatic Inspect/Reconcile/termination attestation/owner continuation
is triggered by opening or saving the catalog. Shared catalog selection (ST08) remains in
its current family, including exact saved IDs, missing references and AllowAll behavior.
No agent-allowlist broadening or cross-module reference migration is needed for this leaf.

The sandbox substitutes owner data/commands only. It must display the full actual wizard,
not real production Recovery or pickers secretly injected behind a lightweight parent.
