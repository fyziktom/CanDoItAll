# Shared lifetime corrections

WC-C1, WC-C2 and WC-C3 are authorized closure repairs. Their historical attribution remains
unproven; do not spend the campaign rewriting their history. Reproduce the actual current
failure and preserve the business invariants. A matching exception class alone is not a
causal reproduction.

## Common approach, not a common new framework

For each path write a small ownership table: initiating UI lifetime; admission; actual owned
resource; operation lifetime; cancellation signal; result publication; disposal completion.
Use deterministic TaskCompletionSource or an existing owner/interoperability seam to stop at
specific boundaries. Dispatch actual bUnit events on the renderer and await their tasks.
Capture the first failing ordering before implementing its correction.

Track the tasks whose exceptions otherwise escape as detached work. Do not solve the task
by making every operation fire-and-forget, swallowing all cancellations, extending all
deadlines or converting scope-local services to singletons. UI retirement suppresses late
publication, not the truth of an already admitted mutation.

Microsoft's primary documentation says SemaphoreSlim.Dispose must not run concurrently with
its other operations (EXT01), and Blazor can re-enter components at incomplete awaits and
dispose them before initialization completes (EXT02/EXT03). These are lifecycle constraints,
not a prescription to block the renderer until arbitrary I/O finishes.

## WC-C1 — Simple Chat contributor, runner, database context and service scope

### Source and observed chain

Reported chain: conversation shell initialization -> LlmChatConversationShellContributor
catalog read -> scoped Simple Chat application/store -> disposed SimpleChatsDbContext or
LlmChatProfileScopeRunner semaphore Release. The maintained log does not establish the
precise preceding browser navigation. Sources R05 and R17-R19 establish the reviewed code.

The runner serializes ExecuteAsync, acquires a profile lease, pushes an operation context,
invokes a delegate and releases the semaphore in finally. Dispose immediately disposes the
semaphore. A held operation can reach Release after it was disposed; a queued waiter can
also be affected. A profile lease checks identity; it does not necessarily own the injected
application DbContext or all scopes through which the delegate reaches it.

The contributor stores an initialization task but its DisposeAsync only marks disposed and
unsubscribes. Its catalog method can proceed from the definition await to the conversation
read after the source lifetime retired. ConversationShellHost already guards the captured
lifetime token and defers releasing that CTS until initialization settles, but that alone
does not keep all dependency scopes alive. Do not undo this existing S0 protection.

### Design work required

Map actual DI registrations, construction order and scope ownership. Inventory runner
callers: query application services, mutation services and hosted operation dispatch.
Choose the smallest ownership correction that can prove all of the following:

- A retired owner admits no new work. Pending admissions either complete under their original
  authorized operation or cancel deterministically; they do not hang on a disposed gate.
- Actual contexts/operation scopes used by accepted work remain alive until that work has
  completed or been safely canceled and observed. Do not rely on incidental DI disposal order.
- Gate and lease release occur once. An older completion cannot release/dispose the successor's
  resources. The original database identity and authority remain fixed throughout.
- A contributor cannot call another scoped service after retirement, publish into a new shell,
  reattach invalidation handlers, or reopen an old history/archive dialog in its successor.
- A UI close does not mark a committed mutation rejected or replay it. The existing hosted
  operation and durable receipt/cancellation protocols remain authoritative.

An explicit narrowly owned operation scope may be justified. Reusing a factory without
carrying original identity can silently route to the new database and is forbidden. Simply
changing the runner to IAsyncDisposable is insufficient unless the real dependent context
and waiting operations are owned correctly. Do not create an application-wide lifetime bus.

### Controlled tests and production controls

Test retirement while waiting for the gate, after acquiring it, during the first query,
between definition and conversation reads, during result publication and during scope
teardown. Vary cooperative and cancellation-ignoring reads. Exercise component replacement
without scope shutdown separately from scope/circuit shutdown. Require no late service call,
no disposed DbContext, no disposed semaphore, no indefinite waiter and no duplicate release.

Use real DI/application persistence for at least the owning-scope test and real PostgreSQL
where its service contract requires it. A fake contributor alone cannot prove context lifetime.
Then verify ordinary Simple Chat create/send/stream/cancel/reopen, original-profile rejection,
no duplicate durable turn and the existing S0 shell tests. Include the actual broader route
sequence that originally exposed the failure, not merely an isolated Collaboration page.

## WC-C2 — Dialog and DialogInterop in Components

### Source and failure

R06 records TaskCanceledException during BaseLib Dialog.DisposeAsync -> DialogInterop.CloseAsync.
R20/R21 confirm that Close catches JSDisconnectedException only. Dialog then awaits module
release and only afterwards disposes its DotNetObjectReference. A failed close bypasses both.
The module-import path also has an open/dispose handshake that must remain correct.

### Required behavior

Separate explicit active close from teardown. Capture the exact owned interop instance,
module/import, dialog ID, callback reference and open-request generation. A close failure
must not skip independent cleanup. Cleanup must be idempotent, including a late import and
repeated renderer disposal. Release each owned object once; do not close another dialog.

Use finally-style resource ownership or an equivalent explicit state machine. Contain known
teardown/disconnection cancellation at the correct boundary. Do not suppress unexpected
active JS errors, turn an unacknowledged live close into a success, or broaden generic catches
across the app. If DOM cleanup must move to the browser side, keep it scoped to the actual
instance and preserve native top-layer/focus behavior; no general dialog redesign.

A late open import cannot register a removed element or already released .NET callback.
An old close/finally cannot release the newer module/open session. No StateHasChanged call
should be required to complete disposal. Browser resource cleanup must not wait forever.

### Required cases

| Ordering | Proof |
|---|---|
| Cancel close while disposing | Owned module/.NET release still happens; no circuit-fatal escape of expected teardown |
| Disconnect before/after import | No late native open and no leaked callback; settled task observed |
| Unexpected JS exception during active close | Visible/diagnosed failure, not a false pass |
| Import completes after dispose | Exactly one module release, no double-open or successor contamination |
| Dispose called twice or overlaps explicit close | Stable result, no duplicated release/close |
| Module disposal itself fails | Remaining managed cleanup still occurs and diagnostics preserve the original context |
| Two parents and nested children | Child-only closure; sibling remains; Escape/backdrop and focus restoration work |

Run existing DialogModuleLoadRaceTests, DialogNavigationOwnershipTests and applicable BaseLib
interop tests, plus true product Dialog consumers and all published Workspace sandboxes.
Keep ordinary geometries, keyboard and full-log assertions. No copied Dialog in CanDoItAll.
See DEPENDENCY_DELIVERY for source/package/CI consumption.

## WC-C3 — MainLayout and browser state

### Source and failure

R07 reports a disconnected circuit in BrowserWorkspaceStateStore.SaveAsync during
MainLayout.OnAfterRenderAsync -> ResolveAndTrackCurrentTabAsync -> Workbench.TrackTabAsync.
R22/R23 show first-render operations and detached location-change work without a full layout
retirement generation. The existing `collaborationDisposed` only protects that badge path.
R24 persists under a profile-keyed browser storage key and must not silently certify a save.

### Required correction

Give the actual layout initialization/navigation work a narrow lifetime and generations.
Capture the URI/profile/source before asynchronous resolution and revalidate before any
navigation, tab-state publication, listener registration, browser save or final rerender.
Unsubscribe and retire queued work on close. Observe its completion without re-entering a
retired renderer to finish. A late missing-project result must not redirect a newer route.

Retain exact browser state profile keys/fingerprints, canonical runtime selection and
cross-tab notification semantics. Saving the future restart profile is not switching
canonical services. A snapshot from an old request must not be stamped as a new profile.
When JS becomes unavailable, report an unacknowledged browser-state effect rather than
returning success from every store operation. Distinguish expected retired-layout disconnect
from an unrelated active-store error. Do not disable persistence or silently recreate empty
state on every navigation.

### Tests

Hold initialization, project lookup, tab tracking, browser save, listener registration and
profile UI read one at a time. Retire and replace the layout, then complete each old task
with success, cancellation and failure. Require no obsolete redirect, cross-profile key,
late listener/callback, duplicate notification, unobserved fault or logged circuit error.

Add active positive cases for startup prompt, back/forward and reload, tab open/close/reopen,
layout-only persistence failure and retry, independent circuits and profile restart. At
least one browser test must navigate quickly across Settings/Data Sources, Agents/Simple
Chats, Collaboration, TestLab and Project Structure with real shell services.

## Closure proof for the three together

Controlled tests establish ordering; repeated composed browser execution establishes that
product integration reaches the corrected owners. Keep UTC timestamps and host/circuit IDs
so a late previous test is not attributed to the page that merely observes its log. Inspect
all new server/circuit errors. Classify an expected owned shutdown using actual lifecycle
signals, not a global substring allowlist for ObjectDisposedException or JSDisconnectedException.

No claimed data leak/unauthorized write has been established in these reports. Add authority
and persistence negative controls to prevent an over-broad lifetime repair from creating one.
