# 2 · State, effects and mutations

These are invariants to preserve in the chosen slice, not a universal state-manager implementation. Current examples include the CRM/HR workspace contract and Prompt Gallery sessions [S04, S05, S11–S14, S22].

## One authority per fact and lifetime

| Fact | Authority / lifetime |
|---|---|
| Route-significant selection, meaningful view, committed scope | Routed host / existing route contract |
| Active editor or dialog target | Owning host session; distinct from current list selection |
| Unsaved editable values | One editor draft instance, with an explicit replace/reset transition |
| Validation and raw parse errors | The `EditContext` associated with that draft |
| Accepted data, loading, failure, stale status | Independent read session keyed to scope and generation |
| Focus, element refs, transient disclosure | Rendering-local state where this is sufficient |
| Durable execution, operation identity, retry authority | Real backend owner, not a component lifetime |

`EditContext` need not physically live in the host. The actual CRM surface holds it and uses `ContextFor` to keep it while the host's draft reference stays the same [S12]. The invariant is preservation across same-record section changes and rerenders. When a subtree is truly unmounted, keep the context/draft in an owner with the required lifetime, or define the intentional reset. Do not promise retention merely because the draft object survived.

Keep mutable drafts, request generations, secret content and validation errors out of serialized navigation. Preserve existing deep links and back/forward behavior. Use explicit stable tokens for new external route contracts; internal tab indexes or enum-based display labels are not automatically public contracts that must be rewritten.

## Async reads and rendering

Before every asynchronous effect, capture its scope/target, operation identity and generation. Retiring A and opening B, including A→B→A, creates a new lifetime. Guard late **success, error, notifications, final cleanup, focus and dialog close**, not just the data assignment. A callback must not rebind itself to the latest selected ID. An old `finally` must not clear a new busy state or ownership slot.

Cancellation is cooperative. A fake that ignores cancellation is useful to prove the generation fence. Each request owns its cancellation source and disposes it after the operation has unwound, including debounce cancellation and error paths. Retiring an operation cancels it and releases the *current* UI admission slot; it must not prematurely dispose resources still used by that operation. Dispose subscriptions and JS handles at their real owner. Observe faults from owned tasks rather than abandoning fire-and-forget work.

`PromptGallerySearchSession` demonstrates generation checks and separate post-write refresh messaging, but its reviewed implementation cancels/replaces sources without disposing those sources [S22]. Reuse the invariant, not that incomplete resource-lifetime detail. A focused repair belongs with the appropriate module work; it is not performed by this documentation package.

Blazor permits re-entry around asynchronous waits [F03]. Keep renderable state valid before an await, use the renderer dispatcher for UI updates, and retain the current event receiver/render-notification behavior when moving markup. Do not treat `InvokeAsync` as a global transaction lock. The workspace view implementation explicitly requests host rerenders for surface-handled events [S11, S12].

## Independent reads and honest availability

Totals, list rows, selected-record access and action-target resolution can be independently useful. Give them separate loading/error/retry lifetimes when they are separate operations. A failed total must not destroy a valid action target or claim it is unauthorized. One accepted summary supplies its multiple consumers, rather than recomputing contradictory values in cards.

Keep accepted same-scope data visible with a stale indication after a failed refresh. Never label cached data from another profile/project/provider as data for the new desired scope. Missing, unavailable, empty and zero are distinct. Preserve source provenance and reject older/out-of-scope responses according to the owner's real revision rules; do not invent a numeric order for opaque tokens or unrelated owners' versions.

Queries remain bounded. Preserve an existing date window/paging policy when extracting an overview; never replace server filtering with loading all history and filtering inside the renderer. A UI-performance refactor must not silently change analytics meaning or hide a backend query regression.

## Write admission and outcomes

Capture a submission snapshot and its target/version before dispatch. Admission is enforced at the owning editor/command boundary, not only with a disabled Save button. Enter, footer Save and alternate actions must pass the same validation and write gate. A UI busy gate prevents local duplicate dispatch; it is not durable idempotency across retries, restarts or multiple clients.

| Owner evidence | Presentation and recovery |
|---|---|
| Known rejection before commit | Keep draft and validation; distinguish conflict from ordinary invalid input; no success notice |
| Known committed | Adopt durable ID and owner version/receipt before refresh; publish completion once |
| Known committed; projection/read-back/secondary effect failed | Keep identity and visible warning; retry the read or documented secondary action, never replay the successful write |
| Actually unknown outcome | Preserve draft and operation information, block blind replay, offer the owner's explicit reconciliation/recovery path |

Do not classify every caught exception or cancelled view as an unknown write. Conversely, lack of a success response does not prove rollback. Classify using the real owner boundary. Use existing owner result shapes; a universal new `OperationReceipt` protocol is not a prerequisite for moving a renderer. If the touched behavior cannot safely distinguish commit from failure, fix the narrow owner/result seam or leave that operation explicitly blocked rather than inventing a result in UI.

## Post-dispatch editing and concurrency

Preserve the editor's existing deliberate policy: Prompt Gallery currently disables editing during a command, while CRM/HR includes reconciliation for edits made while a write is pending [S04, S05]. Neither policy is a mandate for every module.

Where editing continues, reconcile returned accepted values against the **submitted snapshot**, not the editor's original baseline. Preserve fields changed after dispatch and their raw validation state; update untouched fields and the owner's assigned identity/version. Reconcile collections by semantic row identity, including newly assigned identities. A retired response cannot reset the successor draft. A create may start a new draft only when doing so does not erase the operator's next record.

Concurrency tokens are authority, not presentation decoration. Do not adopt an arbitrary newer read-back token while retaining older draft content; it could authorize overwriting another actor's changes. A multi-statement read's header token is not proof that every returned collection came from that same revision. Prompt Gallery's accepted-content/read-back rules document this exact concern [S04]. Preserve existing proven semantics or add the required narrow owner guarantee; do not opportunistically redesign transactions in a UI slice.

## Nested presentations, context and content

A dialog/overlay belongs to the interaction that opened it. Retiring it closes only its own presentations, not unrelated windows. Verify the surrounding navigation/overlay service too: a correct page can still lose ownership if a global handler closes everything. Keep a bounded opt-in correction when shared behavior must change; do not alter global close semantics incidentally.

Provider/model resolution, project admission, capability assignment, approval and source-read permission are different facts. A same-target parameter echo must not reset editing or reissue every read. A meaningful target/context change invalidates the relevant effect chain. The special treatment of initially unresolved provider options is a documented per-feature policy, not a generic rule to ignore provider changes.

Render user/agent content safely. Do not convert text to raw HTML while moving a component. Preserve sanitization, URL/attachment policy, masking, restricted states and safe failure messages. Exercise long text, empty values, malformed/unavailable references and nested overlays through the real renderer.
