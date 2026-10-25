# P02c retained reference disposal amendment

Status: implemented source under parent validation, 2026-10-16. The parent
authorized the narrow implementation after review. This record does not claim
runtime closure; the parent owns serialized builds, tests and static enforcement.

## Problem and decision

The current 64-record/16-MiB IndexedDB limit is global to the browser origin, but
the recovery list is scoped to the current namespace/caller. Old bindings can
therefore exhaust capacity with no product action to release it. Browser developer
tools cannot remain the only escape. Add a small, explicit **Manage retained save
references** action beside the pending list, available even when the current
definition context or scoped listing cannot be read.

This action inventories metadata only across bindings. It lets the browser user
copy/export an original reference and deliberately discard that one local record
at an exact revision. Discarding is destruction of recovery evidence, not a server
cancellation, rejection, acknowledgement or rollback proof. It never triggers a
lookup under a reconstructed caller, invokes a mutation, changes the operation
identity, retokenizes a draft or automatically starts a replacement save.

Keep this separate from normal `CancelPrepared` and `ClearResolved`: those encode
specific known protocol states. A new explicit `DiscardEvidence` action must not
be accepted by either existing transition. An unresolved record may be discarded
only through this visible user decision; it must never be an automatic quota
handler or a fallback after a failed save.

## Minimal typed seam

Add a small administration boundary adjacent to the existing Projects-specific
pending store, implemented by the same Web adapter. It does not return
`ProjectPendingSubmission`, accepted bytes, frozen commands or editor models.

| Operation | Input | Output |
| --- | --- | --- |
| `ListReferencesAsync` | bounded page size and opaque typed continuation | immutable `ProjectPendingReferencePage` |
| `ExportReference` | selected immutable metadata | versioned metadata-only UTF-8 reference document |
| `DiscardEvidenceAsync` | original operation + exact observed revision | closed Applied/Changed/Missing result with metadata only |

`ProjectPendingReference` contains the exact operation address (namespace UUID,
original caller binding, epoch represented without JavaScript number rounding,
operation UUID), browser revision, last locally recorded stage, uncertainty flag,
starter-plan-present flag, original nullable target ID and bounded stored-byte
count. The UI labels the stage **Recorded in this browser**. An Accepted label is
not a fresh authorization check or evidence that the current definition still
equals that receipt. An original create has a null target; do not invent a target
from a current search or decode accepted payload just to fill metadata.

Classification against current trusted context is `Current`, `OtherBinding`, or
`ContextUnavailable`. The adapter may compare these values but must never use a
stored caller binding to construct an invocation. Context unavailability does not
block local metadata export/disposal, and does not expose the raw stored body.
The full-body reader and receipt lookup retain their existing current caller and
dataset checks; the administration boundary cannot call them.

The reference export has a named format/version, exact original address, original
target if known, local stage/uncertainty, and an explicit statement that it is a
reference, not accepted/absent proof. Exclude draft fields, external identity,
accepted child/scalar values, starter plans, tokens, credentials and invocation
objects. A download/copy completion confirms only the local action; the app must
not claim the user has permanently backed it up. Importing such a reference can
at most open an explicitly authorized outcome lookup later; it contains no
command and never authorizes dispatch.

## Store transaction and multi-tab behavior

Keep the current schema and strict transaction primitive for this narrow slice.
Add a global bounded cursor that projects only validated header metadata into the
JS response. It must not call the C# recovery codec or serialize `frozenRequest`,
`acceptedPayload` or `absenceProof` into the interop result. Existing IndexedDB
records are same-origin plaintext; this seam is application disclosure control,
not encryption or protection from arbitrary same-origin script. Cursor iteration
may read the current combined record in browser memory; no body leaves the module
through this administrative API. A future split-header layout is not required for
this bounded store and must not be implied by the implementation.

Discard runs one strict readwrite transaction spanning submissions and usage
metadata. Compare exact key and global-monotonic revision; on mismatch return only
Changed/Missing metadata, with no deletion and no body. On equality, delete that
record and decrement count/bytes consistently using the same checked accounting
as existing clear. Resolve only after transaction completion. Preserve the global
revision allocator so deletion/recreation cannot make an old confirmation valid.
Quota, abort, retired connection and blocked upgrade fail visibly without clearing
anything. No per-record or database-wide automatic eviction is introduced.

If another tab accepts the operation before the transaction, its newer revision
wins and the confirmation becomes stale; the user must inspect and confirm again.
If discard wins after an already applied dispatch claim, the remote call may still
commit. The result may no longer fit a deleted local record, so that tab keeps any
known accepted result in memory and reports evidence removal; it cannot downgrade
known acceptance or issue a fresh operation. An old prepared/dispatching revision
cannot obtain a new applied claim after deletion. There is no claim that local
disposal cancels in-flight work in another tab.

The administrative result is metadata-only even on stale CAS. Reusing today's
`ChangeAsync` full-record decoder here would leak foreign frozen/accepted bodies
and is specifically forbidden. Use a separate typed response mapper over the same
module transport, not a new generic persistence abstraction.

## User flow and presentation lifetime

Use the existing shared dialog/list/Alert/Button/TextBlock components, desktop
layout and a bounded 16-row page. Show the reference, local state, byte usage and
binding classification; omit project names and raw values. Per row:

1. **Copy/export original reference** is available without an owner call.
2. **Discard local recovery data** opens an exact-row confirmation displaying the
   address and observed local state. For Dispatching/Unconfirmed, say that the
   operation may already have committed or may still finish, and that discard
   removes the browser's command/result recovery data. For Accepted, say that the
   accepted effect is not undone. For Prepared, distinguish this deliberate
   disposal from the normal current-scope undispatched cancellation flow.
3. The user explicitly acknowledges evidence loss and confirms. There is no timer,
   auto-accept, prechecked confirmation or fresh save coupled to this action.
4. Applied removes the metadata row and refreshes usage. Changed shows the newer
   metadata and resets confirmation. Missing says the local reference is already
   absent; neither result claims a server outcome.

If this page still holds the selected live editor-operation pair, capture the
exact session and retire its presentation before disposal. Applied leaves that
draft terminal **Local recovery data discarded** with the original operation
reference still visible for the rest of that presentation. It must not set Ready
or call `ReleaseMutation` as if the server rejected it. There is no same-payload
fresh-ID Save button. The user can close it and deliberately open a current-based
editor; a current read still does not establish whether the old command committed.
A new creation requires an explicit new blank draft, never a copied/restamped
old frozen request. Preserve other live drafts and their raw text; no list refresh
may mutate them. Cross-tab evidence changes are detected by normal exact-revision
store operations; no automatic resubmission or generic history framework is added.

Known/unknown server semantics therefore remain unchanged. The new terminal fact
is only **the user discarded this local record**, not **the save was canceled**.
The capacity release is a visible administrative choice, distinct from normal
safe recovery. Documentation must stop claiming that every unresolved record is
retained forever once this explicit destruction option is shipped.

## Corrupt data scope

The old-binding capacity gap concerns valid records. Administration should remain
usable when a valid metadata header has an unsupported/corrupt payload: display
only its locally recorded metadata without verifying, decoding or exporting the
payload, and permit exact-key
revision disposal only when checked usage accounting is consistent. If the key,
revision or global accounting is corrupt, refuse per-row deletion and report an
explicit storage-integrity problem; do not guess bytes, reset counters or delete
the whole origin. A separately reviewed **reset only this Projects pending store**
flow may be needed for that exceptional integrity case, but is not a silent quota
fallback and must never delete unrelated Workbench/user browser storage.

## Proof required before implementation closes

Required proof dimensions; source coverage and runtime status are recorded below:

- Metadata-only listing/export of another namespace/caller contains no distinctive
  frozen/accepted field values; actual owner authorization is never invoked or
  reconstructed from those references.
- Fill the approved store with old-scope records, observe new preparation fail
  closed, explicitly discard one exact old reference, and prove a new current
  preparation fits without any automatic owner dispatch.
- Two same-origin tabs race Accepted against discard: exactly one revision
  decision wins; stale confirmation cannot remove newer acceptance. A later
  Unknown observation cannot change the meaning of successful local disposal.
- Discard races applied dispatch: remote outcome remains possible; the original
  live session never receives a new operation ID or a second automatic effect.
- Transaction abort/usage failure keeps records and counters unchanged; a disposed
  connection cannot report a successful administrative deletion.
- Captured confirmation cannot clear a recreated address (global revision ABA),
  cannot clear a different row and cannot release a successor editor session.
- Local Accepted, Unconfirmed and Prepared each render distinct confirmation
  meaning; applied discard leaves the affected editor terminal, preserves other
  drafts and exposes explicit reference export.
- Stored unsupported/corrupt body with sound header can be discarded without
  decoding it; invalid header/accounting fails visibly without a broad reset.

Reuse the existing Projects pending JS fixture and native isolated browser host.
Do not create a server per case or touch user browser data.

## Implemented source and validation handoff

`src/UI/CanDoItAll.Projects.UI/ProjectPendingReferenceContracts.cs` defines the
metadata-only interface, immutable reference/page/change and versioned reference
export. `BrowserProjectPendingSubmissionStore` implements this interface beside
the existing submission interface, using separate wire DTOs without the recovery
codec. Web DI aliases both interfaces to the same scoped concrete store.

`wwwroot/js/projectPendingSubmissions.js` implements bounded global metadata
listing and exact-revision discard in the existing strict transaction primitive.
Deletion checks stored string sizes and usage arithmetic; it can reduce an
already over-limit store but cannot admit a new record above the configured
limit. The global revision allocator survives deletion. No new database schema,
storage fallback or dependency was introduced.

`ProjectsPage.References.cs` owns inventory, explicit selection/confirmation and
live-session retirement. `ProjectPendingReferenceSurface.razor` renders typed
metadata/callbacks through shared components. A discarded live session stays
terminal `LocalRecoveryDiscarded`; late accepted metadata is retained in memory
without acknowledging the draft, starting seeds or reopening Save. Independent
current-scope reads and the original pending session remain the only routes to
body recovery and authorized receipt disclosure.

Source-derived additions are 3 `ProjectPendingReferenceSurfaceTests` theory
rows, 2 `ProjectsPendingRecoveryTests` facts (6 total), 2
`BrowserProjectPendingSubmissionStoreTests` facts (9 total), and 3
`ProjectPendingSubmissionBrowserTests` facts (12 total). These cover explicit
confirmation/export, unavailable-context inventory, known acceptance completing
after local disposal, codec-free metadata mapping, two-tab CAS/ABA, capacity
release and aborted/corrupt-body disposal. The native late-completion case holds
browser acceptance persistence after a real owner result; it proves terminal
client behavior, not cancellation of an in-flight database commit.

The first parent-run 24-case lightweight lane discovered all 24: 21 passed and
the 3 new renderer cases failed because `FormSection` does not accept arbitrary
HTML attributes. After moving the row attribute to shared `Stack`, the parent
reran all 3 affected cases: 3 passed (398 ms). The existing `PendingRecovery`
sandbox scenario now renders the same reference surface with synthetic
Prepared/Unconfirmed/Accepted and other-binding metadata plus explicit
selection/disposal. It has no browser store or owner-service dependency. Other
new native/unit/browser cases await parent execution. See
[native validation](../validation/P02c-native-editor.md) for final
run evidence rather than interpreting this source inventory as passing proof.
