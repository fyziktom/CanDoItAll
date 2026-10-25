# P02c native editor validation record

Status: source frozen for parent validation on 2026-10-16, including the minimal
pending-list wrapper repair described below. Completed subset proof and current
failures are distinguished from planned/refreshed runs; native/browser closure
has not been claimed.
The parent owns builds, test discovery/execution, portability enforcement,
resource cleanup and commits. This subagent ran no build, test or browser host.

## Boundary and behavior review

`Pages/ProjectNativeDefinitionOperations.cs` fixes trusted `LocalWorkspace` and
uses coherent definition reads for both acquisition and receipt access. It binds
the draft to namespace/caller across before/after context reads, refuses missing
existing projects, checks original scope again before freezing, and uses the
existing owner codec and protected writer. Current read access never reacquires
mutation authority just to disclose a receipt. An undispatched draft may acquire
the current epoch within its original namespace/caller, but a restored namespace
cannot silently adopt an earlier draft.

`ProjectDefinitionInputBounds` shares count/strict UTF-8/aggregate accounting for
raw editor models and typed commands. `ProjectModalHost.ValidateCapture` is a
synchronous typed host policy checked before mutable lists are copied; the native
adapter checks fresh limits again before creating the frozen DTO. The renderer
has no access to Projects owner services or IndexedDB.

`ProjectsPage.Pending.cs` owns the scoped bounded index and explicit recovery
actions. `ProjectEditorOperationSession` pairs the live submission row/revision
map with one immutable pending operation. Applied `BeginDispatch` is the only
permission to invoke the protected writer. Known acceptance and its identity are
retained before browser receipt persistence and local acknowledgement. Historical
acceptance is kept separate from a later current-definition read; inspecting the
latter never retokenizes the existing draft. A restored browser Accepted record
requires fresh authorized owner lookup before explicit apply. Subsequent typing
in a live editor remains governed by the existing per-field capture revision map.

The page retires presentation independently of evidence. Late un-reconciled
acceptance cannot seed, navigate or mutate a successor. Pending open reads use
editor intent plus a clear generation; exact CAS cleanup cannot release a newer
session. Known success/refusal of a starter attempt may clear an already
reconciled acceptance after close. Unknown starter completion remains uncertain;
attempted rows remain inspectable and excluded from automatic seed capture.
No receipt recovery or replay begins a new starter effect.

New `ProjectPendingListSurface` and `ProjectPendingRecoverySurface` consume typed
values/callbacks only. Shared Alert, Stack, Cluster, FormSection, Button and
TextBlock compose the status/actions; the existing Wide modal and body scroll
remain primary. No CSS, package, authorization injection or responsive redesign
was added. Components MCP was unavailable; existing shared source/usages were
reviewed, and no MCP validation is claimed. `PendingRecovery` in the independent
Projects sandbox supplies explicitly synthetic check/apply/finish state with no
browser storage or owner writes. Changed/new rendering inputs have reviewed
register entries; final register enforcement belongs to the parent.
The same scenario also renders `ProjectPendingReferenceSurface` with synthetic
Prepared, Unconfirmed, Accepted and other-binding metadata, export and explicit
discard confirmation. It does not open IndexedDB or resolve an owner service.

## Frozen proof sources

| Source/filter | Cases in source | Purpose |
| --- | ---: | --- |
| `ProjectPendingOperationSessionTests` | 13 | Existing claim/uncertainty/acceptance/state boundaries |
| `ProjectEditorOperationSessionTests` | 7 | Post-dispatch typing and child IDs; late accepted disposal; restored historical/current distinction; starter attempt once; replay/restored non-seeding; exact ordered/raw restore |
| `ProjectEditorSurfaceTests.Host_capture_validation_runs_before_snapshot_and_can_block_submission` | 1 | Host validation runs before capture and can prevent submission |
| `ProjectPendingReferenceSurfaceTests` | 3 | Exact explicit confirmation and metadata-only reference export; Accepted, Unconfirmed and Prepared meanings |
| `ProjectsPendingRecoveryTests` | 6 | Actual owner + explicit test store: acceptance persistence failure, successor isolation, prepared cancellation versus uncertain refusal; foreign reference disposal without body disclosure; terminal discard despite late known acceptance |
| `ProjectNativeDefinitionOperationsTests` | 3 | Missing-existing read refusal, namespace ABA refusal, shared raw count/UTF-8/aggregate bounds |
| Existing `ProjectsEditorMutationTests` seed refusal and retired-success cases | unchanged | Added exact store-empty assertions after known resolved seed outcomes; successor drafts remain unchanged |
| `ProjectsPendingRecoveryBrowserTests` | 1 | Actual native host/IndexedDB, owner success followed by injected browser acceptance-write failure, same-origin reload, historical receipt and independently later current definition |
| `BrowserProjectPendingSubmissionStoreTests` | 9 | Typed mapping and bounded storage transitions, including metadata-only listing/disposal that never calls the body codec |
| `ProjectPendingSubmissionBrowserTests` | 13 | Two-tab IndexedDB CAS, dominance, reopen persistence, quota/transaction failure and strict durability; cross-binding capacity release, stale/ABA disposal, accepted/discard race, abort/corrupt-body accounting and LF/CRLF key refusal before transaction |

The parent reported discovery/execution of the first 20 session cases: 20 passed
(83 ms), and the additional capture-validation case: 1 passed (488 ms). Those
runs preceded the final known-seed cleanup; they do not establish the final native
or browser closure. Direct Web and Projects.UI build results are recorded by the
parent, including the required refresh after the last production edits.

After the retained-reference implementation, the parent discovered 24 light
cases: the existing 21 passed; all 3 new reference-renderer cases failed because
`FormSection` rejects `data-testid`. The correction moved that attribute to a
supported shared `Stack` wrapper. The parent reran all 3 affected cases: 3 passed
(398 ms).
The parent subsequently reported the reference sandbox direct Release build
passed with zero warnings/errors, and all 9 browser-store adapter cases passed
within the combined 38-case Unit lane (`p02bc-repairs-unit.trx`). The owner recovery
codec's 8 cases passed in the earlier `p02b-foundation-unit.trx`; other classes in
that historical combined lane failed, so only this subset is cited. Native/browser
disposal cases remain source inventory until parent results are recorded. The narrow native filter
above is 26 cases: recovery 6, adapter 3 and existing editor mutation 17. No
source count is a substitute for discovery.

The parent's first expanded native run (`p02bc-native-tests.log`) then exposed a
second unsupported `data-testid` attribute, this time on `FormSection` in
`ProjectPendingListSurface.razor`. It caused page render failures across the lane.
The attribute now belongs to an outer shared `Stack`, preserving the selector's
entire list/action subtree. No state, callback or test-count change was made.
Its rendering-register source hash was refreshed. After refreshed Release
outputs, the parent reran all 62 native cases: **61 passed, 1 failed, 0 skipped**
in 4m29s (`p02bc-native-fixed-tests.log`). The earlier three passing reference
renderer cases cover a different surface and did not certify this list. The
original run remains failure evidence, not a completed passing lane.

The remaining failure was
`ProjectsPageTests.Shows_saved_project_as_card_with_dashboard_action`: its
immediate synchronous save click produced no card. Source review established a
readiness race: the pending index/context loads in `OnAfterRenderAsync`, separately
from editor acquisition; capture correctly refuses while it is incomplete, but
the submit buttons previously appeared enabled. The failure log does not include
the draft message, so this is a demonstrated source race rather than a claim that
the trace alone proved its sole cause.

The minimal repair adds a typed host-owned `SubmissionReady` value (default true
for other hosts). ProjectsPage supplies completed pending-index/context readiness;
both modal save actions disable while false, and direct form submission refuses
before validation/capture. The original capture guard remains. The existing
capture-policy Fact now checks both disabled buttons, refusal of direct submit
without running capture validation, and explicit readiness restoration. Four
existing page tests await the actual enabled affordance and await their events;
no timeout was increased, assertion removed or automatic retry added. Source
counts are unchanged and both rendering-register entries were refreshed.

Parent-approved refreshed repair proof is **one lightweight case plus five native
cases**, rather than claiming the failing 62-case attempt passed:

```text
FullyQualifiedName~ProjectEditorSurfaceTests.Host_capture_validation_runs_before_snapshot_and_can_block_submission
```

```text
FullyQualifiedName~ProjectsPageTests.Save_captures_the_last_unicode_name_input_without_blur|FullyQualifiedName~ProjectsPageTests.Saves_project_from_wizard_first_flow|FullyQualifiedName~ProjectsPageTests.Shows_saved_project_as_card_with_dashboard_action|FullyQualifiedName~ProjectsPageTests.Loaded_project_editor_preserves_its_lifetime_when_saving_after_same_id_recreation|FullyQualifiedName~ProjectsPendingRecoveryTests.Close_during_accepted_persistence_preserves_successor_and_requires_explicit_recovery
```

These repair results and refreshed native browser proof remain pending parent
execution. Previously unchanged passing cases retain their exact earlier result;
they are not relabeled as newly executed against the readiness repair.

The expanded native baseline filter is exactly:

```text
FullyQualifiedName~ProjectsPageTests|FullyQualifiedName~ProjectsPageUrlStateTests|FullyQualifiedName~ProjectsEditorMutationTests|FullyQualifiedName~ProjectsPendingRecoveryTests|FullyQualifiedName~ProjectNativeDefinitionOperationsTests
```

Its source count is **62**: 28 page + 8 URL state + 17 editor mutation + 6
pending recovery + 3 native adapter cases. `ProjectsFilesHostLifecycleTests`
is a separate 8-case class and is not included in this count.

The two added page theory rows cover package-import commit knowledge after the
retained-reference slice: acknowledged cleanup warning keeps counts/package ID/
original target and holds further package operations; the stable unconfirmed
error keeps its explanation and also holds dispatch. A forced callback cannot
repeat either import. Their filter is
`FullyQualifiedName~ProjectsPageTests.Package_import_cleanup_warning_or_unknown_outcome_preserves_evidence_and_blocks_repeat`
(2 source cases); the broader `ProjectsPageTests.Package_import` filter has 4.
Both new rows passed in the parent's 61-pass native rerun above; their coverage
is no longer merely planned. They do not inherit the separate backend result.

The next browser filters have source counts **13 + 1 + 1 = 15** after the added
canonical key-terminator regression Fact (no JavaScript production change):

```text
FullyQualifiedName~ProjectPendingSubmissionBrowserTests
FullyQualifiedName~ProjectsPendingRecoveryBrowserTests
FullyQualifiedName~ProjectsPortfolioBrowserTests.Two_browser_contexts_preserve_conflicting_drafts_and_require_explicit_current_based_recovery
```

The whole `ProjectsPortfolioBrowserTests` class has 2 cases; do not expect 1
from a class-wide filter. No existing browser test targets the Projects sandbox
`PendingRecovery` specimen. `RazorSandboxHost` supports AgentEditor/Application
only, while ProjectsFilesSandbox is a different application. The parent completed
a separate real Playwright proof at 1920x1080: select PendingRecovery and Reset,
open retained references, inspect Prepared/Unconfirmed/Accepted rows and the
foreign binding, export Unconfirmed as metadata-only JSON with a string epoch,
select then Keep all three rows, then explicitly confirm removal of exactly one.
Prepared and Accepted remained (2 records / 1024 bytes). Simulated writes and
seeds stayed zero, no horizontal overflow occurred, and the console had zero
errors/warnings. The parent visually reviewed `after-discard.png`.

Retained evidence is under
`output/playwright/concurrent-mutations-reference-sandbox/`:
`selected-unconfirmed.png`, `after-discard.png`, `after-discard.yml` and
`console.txt`. This synthetic browser proof covers rendering/interaction; it does
not replace the strict IndexedDB tests or actual owner/native recovery cases.
The parent verified the exact hidden host PID/start/command before stopping it,
released listener 60921 and closed the browser page; see the resource record.

The component harness now registers `ProjectPendingSubmissionTestStore` and
explicit limits before caller overrides. It is test-only, does not claim browser
durability, and gives deterministic acceptance persistence failure/hold seams.
Production continues to use the strict IndexedDB adapter with no fallback. The
browser case observes the actual pending-module request, including fingerprinted
asset URLs, and installs a page-scoped `IDBObjectStore.put` fault restricted to the
Projects pending database, submissions store, and accepted record stage. It asserts
exactly one injected accepted-record write failure after the actual owner result;
the accepted owner state is read back independently before recovery. The original
module and its integrity metadata remain unchanged. Navigation removes the page-only
fault, and `finally` explicitly restores the prototype if the original page remains.
It is not evidence of a lost database commit
acknowledgement—that boundary is covered by the actual PostgreSQL owner tests.

The first parent-run browser lane (`p02bc-browser.trx`) reported 1 pass and 14 failures.
The existing portfolio case passed. The 13 standalone store failures concern their
test JSON argument bridge and are owned separately. The native recovery failure's
screenshot showed `Project saved.`: its exact unversioned module route never matched
the import map's fingerprinted URL, so the former response injection did not run.
The repair adds module-request and fault-invocation assertions and the actual
accepted-record write failure above; it does not extend timeouts or alter production.
Source count remains one native browser case (13 store + 1 native + 1 portfolio = 15).
The refreshed parent-run failed-case selection (`p02bc-browser-repair.trx` and
`p02bc-browser-repair-tests.log`) completed with **11 passed, 3 failed, 0 skipped
of 14** in 37 seconds. The native recovery case passed in **27.8702479 seconds**
(2026-10-16 19:13:51.7453864Z–19:14:19.6233528Z). Its exact owner read-back, one
injected accepted-record write failure, retained original reference after navigation,
authorized historical receipt reconciliation, separate later-definition read and
final reference cleanup assertions passed. The three remaining failures are in the
standalone store class; this result does not certify that entire class or browser lane.

Retained native evidence:
`output/playwright/crm-hr-live-agent-tools/20261016T191351755Z-9b1b49ad04454a6283bb76055446ee9d/`
contains `projects-durable-historical-recovery.png` and `server.log`. The log records
the injected failure in `ObserveAcceptedAsync` at 19:14:16Z and a subsequent new
browser circuit for recovery. Its expected injected error and existing Development
startup warnings are retained; no zero-console-error claim is made.

The 1920×1080 screenshot was visually reviewed. The wide dialog keeps its recovery
actions above the two-column editor/comparison area and its footer visible. The
left name is `Accepted frozen browser name`; the right saved definition is
`Current definition after another writer`. Text explains that the current read
does not replace the accepted result. No horizontal clipping is visible. One
wording defect was visible: the left historical draft was labeled `Current-based editor`
because the renderer checked only whether a conflict exists. The parent authorized
the minimal copy correction to `Your draft` for all comparison states; existing
state-specific notices retain their context. No new test case was added for this
copy edit. Source counts are unchanged. The earlier screenshot proves the data
separation. The parent subsequently rebuilt UI/Web and reran the native case;
the refreshed proof and visual review are recorded below.

The log identifies owned Web PID **29376**, listener **58128**, and lease
`cditall_primary_4d752cae1efe42118d68f971ed267557` on the owned fixture at
`127.0.0.1:60920`. Read-only post-run checks confirmed the PID absent, no listener,
zero matching `pg_database` rows and zero matching `pg_stat_activity` rows. The
fixture identity and ownership label were verified before these catalog reads;
no cleanup mutation was needed. Browser/host disposal is also covered by the
passing test's awaited `LiveUiHost.DisposeAsync` path. The shared PostgreSQL
container remains intentionally retained for subsequent bundle lanes.

The final repair selection (`p02bc-browser-final-four.trx`) passed **4/4 with
0 skipped** in 30 seconds: the three remaining standalone-store cases plus
the native recovery case after the label correction. The native case took
**28.2202497 seconds**, 19:21:04.0030891Z–19:21:32.2236951Z. Across the retained
selections, all 13 standalone store cases, this native case and the separately
passing portfolio case have passing evidence; this is not a claim that the first
15-case run passed. UI/Web direct Release builds passed in 3.23/29.96 seconds.
The parent also reports final no-write portability enforcement passing with
36,268 findings / 15,439 allowances and rendering validation passing 1,195 inputs.

Refreshed evidence directory:
`output/playwright/crm-hr-live-agent-tools/20261016T192104007Z-b167087f08184a2998c326c76393c5fc/`
contains `projects-durable-historical-recovery.png` and `server.log`. Visual review
at 1920×1080 confirms the corrected `Your draft` heading, historical accepted name
on the left, later saved definition on the right, visible recovery actions and
footer, and no visible horizontal clipping. The log confirms the expected injected
accepted-record write failure at 19:21:28Z. No additional layout or wording defect
was found in this screenshot.

The refreshed host identity was PID **28292**, listener **55244**, lease
`cditall_primary_b3d209e9a5fe4abd94d4c0de032fe66f`. Read-only cleanup verification
confirmed the process absent, zero listeners, zero exact database matches and
zero sessions for that lease on the verified owned fixture at `127.0.0.1:60920`.
No cleanup mutation was needed. Production and test source remain frozen.

## Limits and remaining perimeter

Only the frozen submitted full definition and exact accepted receipt persist.
Post-dispatch typing and raw starter plans remain available in the live editor
but do not survive browser restart; restart text states this explicitly. A scalar
pending record is not fabricated into a full aggregate editor. This native store
currently originates only full create/replace operations.

The scoped recovery list shows current namespace/caller records. The implemented
[retained-reference disposal amendment](../plan/P02c-retained-reference-disposal.md)
adds a separate metadata-only inventory across bindings, available even when
trusted context/scoped listing is unavailable. It exposes original-reference
copy/export and explicit exact-revision disposal to release the global
64-record/16-MiB capacity. Disposal is not rollback or cancellation proof and
cannot create a replacement operation; a matching live editor stays terminal
even when acceptance subsequently completes. The administration interop returns
no body on either Applied or Changed. Unresolved evidence remains retained unless
the user deliberately confirms this destructive local action. Invalid header or
accounting still fails visibly; no database-wide reset is implied. Parent runtime
validation remains required. Current target blocking is a native client
safeguard, not a universal database lock for other applications or tabs.

This cutover migrates `ProjectsPage.SaveCoreAsync`. Legacy facade, maintenance,
package and sibling SPA paths remain in the overall owner-cutover inventory.
The two package import warning/uncertainty component rows also cover changed
target/path and failed subsequent target refresh without losing terminal evidence
or releasing the hold. These native component assertions passed in the 61-pass
native rerun described above. The parent's separate four passing package persistence
cases in `p02bc-package-fixed.trx` are backend commit-knowledge proof, not these
component results. Package
holds are page-instance only; see the
[package review](../reviews/P02b-package-import-commit-review.md) for the P06
navigation/restart recovery gap.
Workbench's separate create modal still uses its legacy path. The
[next-client readiness review](../plan/P02cd-client-cutover-readiness.md) identifies
it as a separate root-create UI caller and proposes reusing the existing trusted
LocalWorkspace native session/codec rather than adding a new governed reservation
protocol. That future cutover is not certified by this record.

No remote effects, user secret values, external services or development fixtures
were created by this subagent. Parent-run browser proof uses the existing isolated
host/context at 1920×1080 and disposes it through the fixture. The test does not
clear user browser databases or reuse port 5032.
