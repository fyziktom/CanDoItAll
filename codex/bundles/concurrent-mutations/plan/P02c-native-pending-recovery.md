# P02b/P02c — Native pending submission integration outline

Status: integration outline, 2026-10-16. Production P02a sources and the P06 plan are
frozen. The standalone browser store and its test sources are prepared separately;
no builds or tests have run for that slice. Parent freezes the shared P02b contracts
before the C# adapter, host and renderer integration starts.

This outline implements the native portion of
[P02b](P02b-durable-results.md) and
[07](../../../../docs/architecture/concurrent-mutations/07-projects-contract-cutover.md).
After P02a commit `1452352f0`, the persistence owner amended 06/07/P02b to replace
raw browserState localStorage load/save/remove for pending mutations with the atomic
IndexedDB mechanism below. Their plaintext/same-origin privacy scope, per-operation
identity, bounded retention and no automatic submission rules remain. Workbench
navigation storage and its schema remain separate.

## Current seams and minimum edits

| Existing source | Reuse / exact new responsibility |
| --- | --- |
| `src/UI/CanDoItAll.Projects.UI/ProjectEditorDraft.cs` | Preserve live Model/EditContext, field revisions, row-reference mapping, original expectation, preflight acknowledgement validation, AcceptedOutcome and conflict/current/previous drafts. Add a reference to the immutable pending operation and pure recovery view state; do not serialize this class. |
| `src/UI/CanDoItAll.Projects.UI/ProjectModalHost.razor` | Keep the Wide primary editor, body scroll and shared comparison. Render typed recovery status/actions through parameters and callbacks; no service/JS/storage/auth injection. Continue allowing newer typing while one captured submission is pending, but block another save of that draft. |
| `src/Modules/CanDoItAll.Modules.Projects/Pages/ProjectsPage.razor`, `SaveCoreAsync`, `TryAdmitMutation`, `ReleaseMutation`, `RetireEditor`, `IsActiveEditor` | Replace unprotected SaveEditor call with the native protected adapter after persistence. Keep per-project mutation blocking and editor-generation checks. Persist exact acceptance before draft acknowledgement; late accepted responses may update their own pending record but cannot reopen or replace an editor. |
| Same file, `ReadCurrentDefinitionAsync` / `StartCurrentDefinitionDraft` | Preserve explicit current read and current-based reconciliation. Historical accepted receipt and current definition remain different typed inputs. Do not fetch current state and use it as the accepted result. |
| `src/UI/CanDoItAll.Projects.UI/ProjectsPresentationContracts.cs` | Add narrowly scoped presentation/store contracts, preferably a separate `ProjectPendingSubmissionContracts.cs` for this cohesive boundary. UI references only Projects.Contracts; no Infrastructure/Web dependency. |
| New `src/Modules/CanDoItAll.Modules.Projects/Pages/ProjectEditorOperationSession.cs` | Small host-owned orchestrator for prepare/dispatch/observe/restore; uses frozen owner adapter contracts plus the pending store. Keep database calls and browser effects out of the renderer. No new project, generic mutation coordinator or second definition writer. |
| New `src/App/CanDoItAll.Web/Infrastructure/BrowserProjectPendingSubmissionStore.cs` | Implement the UI pending-store boundary using IJSRuntime and checked typed serialization. Register a scoped implementation in Web composition. No inference of caller identity from HttpContext absence or browser keys. |
| `src/App/CanDoItAll.Web/wwwroot/js/browserState.js` plus a small Projects-specific script/module if needed | Existing load/save/remove/listKeys are raw localStorage operations. No IndexedDB or Web Locks implementation was found in current Web sources. Add narrow IndexedDB methods under a separate Projects pending namespace; leave existing helpers unchanged. Use constants for database/store/protocol names. |
| `src/App/CanDoItAll.Web/Infrastructure/BrowserWorkspaceStateStore.cs` | Read-only precedent for JS binding and retired runtime checks. Its WorkbenchSessionSnapshot, profile-ID key and runtime-fingerprint partition are not mutation recovery identity; do not extend or reuse that snapshot. |
| `src/App/CanDoItAll.Web/Components/Layout/MainLayout.Lifetime.cs` / `.DatabaseProfiles.cs` | Follow current listener registration/disposal and late-result fences. Database-switch notifications invalidate views, not pending evidence. A BroadcastChannel notification is an invalidation hint, not a dispatch trigger or authorization. |

The small store interface is a real browser boundary needed by native recovery and
the in-memory scenario/test host. Do not invent an interface for each codec/state
helper. The owner adapter names and full/scalar result types come from the frozen
P02b contract; this card does not introduce competing DTOs for them.

## Persisted record and presentation state

Use a versioned record keyed by the full operation address:
`(DatasetNamespaceId, ProjectCallerBinding, Epoch, OperationId)`. Caller binding is
the required non-authoritative server-issued precondition from06; the server still
derives current scope independently. Never key by project name, API token, circuit,
connection string, profile alias or authenticated display name.

Persist only:

- Record schema version and positive store-wide monotonically allocated record
  revision. Deletion never resets the allocator; recreating the same address must
  not let an old expected revision match the new record.
- Full typed operation, request schema/normalization version and closed command
  kind; the exact frozen bounded command, including original expectation and
  ordered phase/option slots. Store raw typed submitted strings before owner
  normalization; the server's versioned semantic digest is a separate concern.
- Typed stage, active attempt ID where applicable, and whether an earlier dispatch
  remains unconfirmed. An attempt ID is client bookkeeping, never a new owner
  operation ID.
- Optional exact versioned accepted payload bytes and essential result metadata;
  decode through the owner codec and validate against the frozen submission.
- Optional authoritative ClosedAbsent evidence, or known rejection of the sole
  first attempt; error codes are bounded and cannot contain raw server exceptions.
- Optional creation/update timestamps as UI information, never proof of remote
  ordering; the initial store does not require them.
- A small `HadStarterPlan` indicator if useful for honest recovery text. Starter
  objects, their effect receipts and live row mappings are not definition receipts.

Do not serialize EditContext, FieldIdentifier, mutable row references, delegates,
PreviousDraft chains, authority/source lease objects, credentials, access tokens,
DOM date fragments or arbitrary browser state. The live capture keeps those only
for current-circuit reconciliation.

The browser record is untrusted input. Validate bounded schema/counts/bytes before
materializing commands, required identity/version fields, closed enums and exact
acknowledgement mapping. Fail visibly on corrupt/unsupported records; do not drop,
truncate, repair with current tokens or automatically discard them. Locally stored
accepted bytes are historical browser evidence, not current authorization. After
restart, authorized receipt lookup verifies/retrieves the accepted owner result
before it drives normal mutation readiness. An expired lookup may leave a locally
retained historical result visible as such, but cannot grant new effects.

Separate a persisted protocol stage from presentation conditions such as
Loading, Checking, ContextChanged, StorageUnavailable and RecordChanged. A current
read failure or old epoch does not rewrite the persisted result as Rejected.

## Atomic IndexedDB store

Use one dedicated versioned IndexedDB database and a `submissions` object store
with the compound key above. Use its dataset/caller key prefix for bounded recovery
listing and add a target-project index only if needed for same-target blocking.
These keys contain no description or credentials. Upgrade schema without deleting
unknown/pending records. If migration cannot preserve a record, keep it readable
as unsupported and block its dispatch.

Each compare/create/transition/clear is one `readwrite` transaction: read the
current record, validate expected record revision and permitted state transition,
then write/delete. Return success only from transaction `complete`, never from
individual request success. Request `durability: "strict"` and verify the
transaction reports that mode in the supported browser qualification; fail closed
when the selected persistence contract cannot be provided. No localStorage or
memory-only fallback for a protected first dispatch. The browser API documents
readwrite transactions and the strict durability option in
[IDBDatabase.transaction](https://developer.mozilla.org/en-US/docs/Web/API/IDBDatabase/transaction).

Keep each transaction short and local. Do not await .NET callbacks, network,
authorization or arbitrary promises inside it. Transactions serialize only these
small record updates; never hold them across the server operation. Exact owner
operation deduplication remains the server's responsibility.

Proposed minimal store operations, with typed C# inputs/results:

| Operation | Atomic behavior |
| --- | --- |
| `PrepareAsync` | Add only when absent, or return the exact existing compatible record. Same address plus different frozen command/version is a mismatch; never overwrite it. Creates `Prepared` with a new store-wide revision. |
| `CancelPreparedAsync` | Compare exact key/revision and delete only Prepared, before any dispatch claim. If another tab already began dispatch, return RecordChanged and retain the operation. This is the only safe pre-dispatch cancellation path. |
| `BeginDispatchAsync` | Compare record revision, record a fresh attempt ID and `Dispatching` before the owner call. First dispatch requires Prepared. Explicit retry from Unconfirmed or a retained Dispatching record marks prior ambiguity; it never clears it. Accepted, ClosedAbsent, Rejected and dismissed records cannot dispatch. |
| `ObserveAcceptedAsync` | Same operation/frozen command only. Exact accepted bytes are monotonic and can be recorded even by an older attempt after a newer one began. Duplicate exact acceptance is a no-op; different accepted bytes are an integrity failure, not last-writer-wins. |
| `ObserveAttemptAsync` | A current attempt may record Unconfirmed. A known rejection resolves only the sole first attempt with no previous ambiguity; later rejection leaves the operation Unconfirmed. Stale nonaccepted observations do not alter a newer stage. |
| `ObserveClosedAbsentAsync` | Store only validated authoritative closed/fully retained absence for this operation. If another tab already stored Accepted, do not downgrade it; surface contradictory evidence to the host for fresh verification. |
| `ReadAsync` / bounded `ListAsync` | Return current metadata/body only for the server-authorized current binding. A read is not a claim or permission to dispatch. Listing is paged, never deserializes every record before bounding. |
| `ClearResolvedAsync` | Compare exact key, expected record revision and terminal stage in the same transaction. A stale clear cannot delete a newer accepted result or unrelated operation. No unconditional remove API. |

Unresolved dismissal is a separate explicit product action, not `ClearResolved`.
For the pilot prefer hide/dismiss with a minimal retained tombstone rather than
deleting the only operation address. It must not release a target for fresh-ID
retry or claim rollback. If true deletion is later offered, its text must explain
that recovery evidence is being removed and outcome can remain unknown; that is
outside automatic cleanup. No TTL eviction of unresolved records.

Prepared is useful: its durable existence precedes dispatch, but no call can begin
until the atomic Dispatching transition succeeds. A competing tab cannot silently
clear that prepared revision after another tab claims dispatch. If dispatching
transition acknowledgement is lost, the host sends nothing in that attempt and
leaves the durable record for inspection. It does not assume the transaction
failed or mint another operation.

Only an `applied` BeginDispatch result authorizes that caller's dispatch. An
`unchanged` result can refer to a completed attempt with the same attempt ID and
does not authorize another call. The typed adapter validates operation, command
kind, versions and complete submitted-index mapping before passing stable codec
receipt bytes to ObserveAccepted; transport-only `Replayed` is excluded.

Browser/circuit/host restart recovery is supported within this browser origin and
profile. Private browsing, user-cleared data, storage eviction or device loss are
not an indefinite durable backup promise. No automatic permission prompt or
background submission is needed. Same-origin storage remains plaintext to that
browser profile; it does not isolate people sharing it.

## First save and result handling

1. Modal validates current inputs and captures the live `ProjectEditorSubmission`
   exactly once. Host reserves its existing mutation-target slot immediately.
   Fetch current native write context using the explicit LocalWorkspace adapter;
   original project expectation still comes from the read that produced the draft.
   Validate/copy into the bounded immutable owner command before browser serialization.
2. Allocate the operation once, attach supported request versions/context binding,
   and persist Prepared. Quota/unavailable/schema/cancellation failure blocks
   dispatch. Keep the live draft and visible error; offer retrying storage for that
   same capture or an explicit safe pre-dispatch cancellation. No memory fallback.
3. Atomically begin dispatch; only after its committed result call the protected
   owner adapter with that exact envelope. Do not read live Model again for the
   effect. Typing after capture continues to affect only the live draft.
4. On Accepted, mark known commitment immediately and retain exact accepted metadata
   in memory before any browser write, local merge, navigation, portfolio refresh
   or starter creation. This preserves P02a's accepted-before-Acknowledge rule.
   Persist acceptance atomically, then run existing acknowledgement preflight and
   merge using the captured live row references/revisions.
5. If accepted persistence fails, keep the earlier durable pending reference and
   in-memory accepted result; show storage failure separately. Receipt lookup can
   recover after restart. If local merge fails, preserve `SaveReconciliationFailed`
   and exact acceptance. Neither path becomes save-unconfirmed or retryable Ready.
6. Known first rejection can release the target only after the operation's rejection
   is safely reflected in the store or explicitly retained as a blocking local
   storage error. A conflict preserves B/L/S and the existing compare/reconcile
   flow. If an older attempt is uncertain, rejection remains attempt-scoped and
   does not release the operation target for a new identity.
7. On Unconfirmed or transport/circuit loss, preserve operation identity, command
   and target blocking. The same-address explicit retry is permitted only by the
   protected protocol, never by changing the epoch/context or rebuilding from L.
8. Navigation/save-and-open/seed actions remain after confirmed acceptance and an
   active editor-generation check. Replaying definition acceptance never repeats
   starter seeding automatically. A seed interval has its own unknown result;
   definition recovery cannot certify it completed.

When a resolved record is cleared after live reconciliation, clear its exact
revision only. If another tab recorded additional knowledge, reload that record
and reconcile rather than force clearing. Ordinary successful saves need not
retain a growing editor history.

## Restart, auth changes and multiple tabs

Initialize the store only after interactive JS is available; current Page has an
`OnAfterRender` interactive marker, not an async pending-recovery startup pipeline.
Use one guarded async startup operation, with the same disposed/editor-generation
discipline as current reads. Do not run JS from prerender initialization.

First obtain the current **server-derived** recovery context and current read
authority. Only then list/load matching record bodies. A mismatched namespace or
caller shows a content-free retained-record notice; it never reveals another
scope's project text or rekeys the command. Retry context acquisition is a read,
not permission to rewrite the pending binding. Native LocalWorkspace is a fixed
workspace/surface scope under06, not per-person storage isolation; current native
authorization must still be enforced before reading/displaying history.

An explicit recovery panel on the Projects page lists only a bounded set of
authorized pending/accepted records. Opening one is user intent; it does not
automatically submit or seed. For a retained existing project, its unresolved
operation blocks another save through the normal editor. A pending create has no
accepted project ID yet; offer recovery in the pending list rather than inventing
one from a matching project name.

Restoring a full definition submission creates a fresh **in-memory comparison
draft from the exact frozen command** in original submitted order. Create fresh
row objects and capture their mapping before applying the historical ack. Do not
use the current snapshot mapper that adds/reorders default placeholder rows to
reconstruct the original submitted-index map. Never deserialize live FieldIdentifier
or reuse an old circuit's row references.

| Authorized lookup | Native action |
| --- | --- |
| Found | Validate exact operation/codec/result, persist Accepted, and reconcile that frozen submission. Label it as the accepted result for this save. A later current definition may differ. |
| NotObserved in Open/Draining | Remain unresolved. Offer manual recheck; offer explicit exact retry only while the original address is admissible. A fresh absent read cannot erase an earlier uncertain dispatch. |
| ClosedAbsent | Preserve authoritative absence evidence; explicitly finish recovery without claiming earlier receipt detail expired. Offer a separately initiated current-based editing session, never an automatic fresh-ID retry. |
| Expired / DetailUnavailable | Keep historical uncertainty or locally retained acceptance; explain which proof is unavailable. Current read is inspection, not outcome proof. No resend under a fresh epoch. |
| DatasetChanged / CallerContextChanged / denied | Keep original address and hide private content as required. No automatic alias, caller, epoch or expectation substitution. |

Two tabs in one browser context may read the same record. IndexedDB atomic
transitions prevent overwrite/downgrade/stale clear; server receipts protect exact
duplicate dispatch if users explicitly retry from two tabs. Same-operation
Dispatching is shown as already in progress, never auto-resumed after a lease or
timer. A later explicit retry records prior ambiguity before calling the owner.
BroadcastChannel events only refresh state; they never execute a command. For two
independently created operations against the same project, owner CAS remains the
cross-tab guarantee; this store must not falsely promise a global project lock.

Close/dispose cancels presentation reads, detaches notifications and releases JS
handles, but does not clear pending data or assert that an admitted save was
canceled remotely. Each async completion targets its captured operation and draft.
A late accepted response may persist to its own key even if the modal closed;
it cannot mutate a new editor or navigate. If the circuit is disconnected and JS
cannot persist, leave the already durable pending record for later lookup.

## Post-dispatch typing and product honesty

P02a preserves typed field edits and invalid raw date input within the current
live editor. P02b restart recovery preserves only the frozen **submitted command**
and accepted receipt. It does not preserve later typing, raw incomplete native
date fragments, the previous-draft comparison chain or starter draft rows.
Show concise wording when the operator opens restored work: this is the submitted
version; later unsent edits and starter drafts were not recovered. Do not offer
"restore all edits" or silently replace a still-live dirty draft with this data.

Current definition inspection is a separate read-only view. Editing again starts
from an explicitly read current definition, with the restored submitted/accepted
version alongside for copy/reconciliation. No token transplant onto stale payload.
Retain the existing shared Alert/Button/Stack/Grid/FormSection/TextBlock approach;
add no custom CSS/package or extra dashboard cards. A small recovery list and modal
status slot are sufficient. Sandbox fixtures must drive the actual renderer.

## Bounds, tests and closure

### Approved native cutover, 2026-10-16

The native pilot uses 64 retained records, a 16 MiB aggregate browser-store bound,
and 16 metadata rows per page. These independent capacity bounds do not reduce
the retained schema-one request/result maxima. A full store blocks preparation;
it never evicts an unresolved operation. The host builds its target-blocking
index from bounded metadata pages, including records outside the displayed page.

`ProjectNativeDefinitionOperations` is the Projects-owned native adapter. It fixes
`LocalWorkspace` in trusted composition, freezes one captured full command,
dispatches the protected writer and reads receipts. Native receipt access follows
the existing coherent Projects read surface and compares the current target
lifetime/profile; it must not acquire mutation authority merely to disclose a
receipt. Projects registers this adapter and the owner recovery codec. Web alone
registers the scoped browser store and its approved limits.

The host owns one editor-operation session that pairs its existing live
`ProjectEditorSubmission` with `ProjectPendingOperationSession`. It retains the
closed dispatch outcome so named first-rejection errors are not lost behind a
stage enum. Owner acceptance is retained before browser persistence or local
reconciliation. Restored sessions require fresh authorized lookup before applying
accepted values, even if the browser record already contains accepted bytes.

The modal keeps its live draft/EditContext while a save is pending. Normal active
completion may reconcile the accepted result as completion of the original Save
intent. Restored recovery requires an explicit apply action; it never substitutes
a current-definition read for the historical result. Exact frozen full command
rows are reconstructed in submitted order without category placeholders. A scalar
record cannot be reconstructed as a complete editable aggregate by inventing
missing child lists.

The page adds a compact pending-save list and one recovery status/action slot in
the existing Wide modal. Current-read comparison remains a separate explicit
action. Cancel is a CAS of a known Prepared revision, not a promise that a timed
out storage action or missing local record proves remote absence. An uncertain
dispatch or ambiguous missing record retains its original operation identity;
closing/hiding the presentation does not authorize a fresh create operation.
Corrupt/unavailable scoped metadata blocks new definition saves until the host can
establish the pending target index.

Starter-object effects remain separate. Before a live initial non-replayed save
starts seeding, its captured starter rows are marked attempted. Attempted rows
remain inspectable but are excluded from future automatic seed capture; an
unknown seed result must not be converted to `IsSeeded=true`. Recovery/replay and
late completion of a closed editor never invoke the seed service. The persisted
`HadStarterPlan` indicator cannot recover raw starter drafts or certify whether
objects were created; restart text states that limit and routes the operator to
Structure for inspection. Definition reconciliation never claims seed completion.

A known starter admission refusal remains distinct from uncertain completion:
the definition stays accepted, attempted rows are not marked seeded, and the
message requires inspection of the current project. An acceptance already
reconciled before the editor closes may clear its exact browser revision after a
known starter success/refusal. It cannot clear a replacement session or navigate
or rewrite its successor draft. A late acceptance that never reconciled, or an
unknown starter outcome, remains retained. The page fences pending opens against
the generation of an in-flight clear and the store still decides the atomic CAS.

The synchronous host capture callback checks raw field UTF-8, aggregate bytes and
row counts before `ProjectEditorDraft.Capture` clones lists. The native adapter
repeats those shared bounds under the fresh write context before creating command
arrays. Existing-project acquisition uses the coherent definition reader and
fails explicitly for an unavailable target; it never falls back to a creation
template. Context reads bracket acquisition and bind the initial draft, including
creation templates, to namespace/caller before any save.

This slice migrates the native `ProjectsPage.SaveCoreAsync` definition call.
Legacy Projects facade methods, package/maintenance operations, other owners and
sibling SPA callers are not silently disabled by the native cutover. Their
remaining caller inventory belongs to the overall protected-owner closure.

The Components MCP catalog was unavailable when this plan was checked. Existing
shared Alert, Stack, Cluster, FormSection, Grid, Button and TextBlock implementations
and their current Projects usages are the source precedent. No package, structural
CSS or new responsive application layout is planned; desktop proof stays at
1920×1080 with the dialog body as scroll owner.

Browser limits must align with the frozen owner options: request, command and
accepted bytes can be large (07 currently proposes up to 2 MiB request/accepted
payload). Compute serialized envelope cost, including JSON/base64/string encoding
if chosen; do not assume a fixed browser quota or compression ratio. Prefer binary
accepted UTF-8 storage or exact JSON bytes with an explicit encoding contract.
Reject before copy/serialization growth where possible. Define named record/count/
aggregate bounds against the measured candidate; reaching them blocks a new
dispatch without evicting unresolved data. Short IndexedDB transactions must not
repeatedly deserialize all records to calculate capacity; maintain any required
counter transactionally and prove crash consistency, or use a bounded key scan.

Test layers (the source freeze and precise current counts are recorded in
[P02c native validation](../validation/P02c-native-editor.md); the complete closure
remains the parent's serialized runtime/static gate):

- Lightweight Projects.UI: state transitions, exact immutable capture, restored
  ordered row mapping, stale nonaccepted observation, acceptance dominance,
  explicit current/accepted comparison, later typing preserved in a live draft.
- Host focused tests extending current `ProjectsPageTests` / native editor tests:
  storage failure means zero owner calls; accepted storage/merge failure never
  retries; close/reopen and late response fencing; seed never reruns from receipt;
  rejected-after-unknown stays blocked; context mismatch reveals no stored body.
- Web adapter/JS tests: transaction completion before dispatch, duplicate address
  with changed bytes refuses, quota/blocked upgrade/corrupt record fails visibly,
  exact compare-and-clear, strict durability feature refusal and no fallback.
- Browser proof must use **two pages in one browser context** for shared IndexedDB
  races, unlike P02a's independent-context concurrency test. One page's Accepted
  wins against another's stale Unconfirmed/clear; different operation records
  survive. Explicit reload/close-and-new-context with the same persistent profile
  proves restart storage, and receipt lookup proves lost response after real commit.
- Auth/dataset tests: current denied/mismatched binding hides bodies; same subject
  credential rotation retains address; native/HTTP surface switch refuses; dataset
  reactivation refuses old records. Profile alias alone never rekeys evidence.
- Actual server proof: accept then disconnect response, later different edit, fresh
  browser recovery returns historical acceptance plus independently different
  current state; no duplicate children, no starter duplication and no fresh-ID retry.

Use synthetic descriptions and isolated browser profiles/DB leases. Cleanup only
the owned test IndexedDB/profile, pages, listeners and server fixture after proof;
do not clear the user's browser storage. Parent serializes builds/tests, retains
exact counts and runs portability/rendering gates. This outline supplies no new
passing evidence.
