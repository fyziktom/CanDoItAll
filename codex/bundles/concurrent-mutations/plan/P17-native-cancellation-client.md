# P17 native cancellation client and legacy transition

Status: native implementation qualified in [the P17/P18 validation](../validation/2026-10-23-native-workflow-cancellation.md); legacy transport and activation remain open.
The original design followed signed admission checkpoint `95a39fb24`.

## Findings

The native `ProcessRunCancellationAction` already fences a late mutation response by
selected run/generation. It still calls the synchronous operator cascade through the
broad workspace projection client, does not read recovery state and has no read-only
refresh after an uncertain result. Its renderer already uses shared Alert and Button.
The parent keys the action by run and Project binding. Keep that ownership; do not add
another selection store, timer loop or dependency from the renderer into Application/Web.

The HTTP `/cancel` route and generated SPA client have an incompatible old completion
contract. The route claims that Applied means cancelled, while the generated client only
accepts an empty HTTP 200 even though the route returns a body. A 202 root admission must
not be quietly projected into that old completion promise. The SPA Process detail page
is still a stub; no native cancellation renderer should be moved into the SPA repository.

The configured Components MCP executable is missing. Its source references an unavailable
Sandbox package and an obsolete Scenario display property. A local tool-only build uses
current sibling component/catalog source and maps that obsolete display field to the
actual example title. The adapter, source hashes and process ownership are retained in
`c4-cancellation-client`; no sibling tracked source or installed settings are changed.
Query the MCP and inspect the exact reused components before editing markup.

## Native boundary

Introduce one focused cancellation client port at the Processes module's native host
boundary. Web implements it because current interactive identity belongs to Web. The
implementation reuses `WebCurrentPrincipalResolver`, including trusted local operator
handling and current token/session grants, and creates a fresh operation scope for
Process services. The existing canonical profile is host-bound; no user-supplied profile,
actor label or root identity reconstruction is accepted.

The trusted local interactive operator receives explicit Process read/execute grants
through the existing transport trust check. It receives no Process definition-write or
general API grant. An authenticated restricted principal remains restricted; local
transport does not augment that principal or authenticate the HTTP request.

A read returns the selected progress plus current execute permission and configured root
admission availability for presentation. Read permission is required on every refresh.
A mutation rechecks current read/execute authority and captures one requester. A root
uses the qualified finite admission service; a child uses only the selected child's
existing cancellation owner. Preserve their distinct typed receipts instead of mapping
pending root admission into a completed child or legacy Applied result. Current caller
checks remain outside SQL transactions; accepted background recovery is independent of
later caller access. Denial has an explicit shared client failure, not an unknown commit.

The module host owns initial read, one in-flight action, manual refresh, cancellation
tokens and request generation. Read/mutation results, errors and callbacks are applied
only to their originating selection. Selection change/dispose cancels and retires its
read lifetime; an uncertain mutation is never assumed rolled back. No automatic mutation
retry or background polling is introduced. A read does not clear an unresolved write or
silently re-enable it. A new deliberate selection starts a new lifetime.

The renderer receives only presentation fields and callbacks. Reuse Alert and Button
for status, cancel and refresh; no new wrapper, CSS, package, service asset or scroll owner.
The run detail remains the primary surface. Cancellation pending, confirmed complete,
historical untracked stop, reconciliation, unavailable worker and unconfirmed response
have distinct text. Child status explicitly concerns the selected child; it never claims
root cleanup is finished. A stale projected Cancelled status cannot suppress pending
cleanup observation. Projection-refresh failure is secondary to a known admission.

Native screenshot review found that refreshing only the cancellation footer left the
surrounding run summary showing its earlier Active projection after cleanup completed.
The explicit Refresh status action therefore has a separate `StatusRefreshed` callback
to its native parent. It refreshes the parent's existing projection without publishing
another cancellation-commit notification or repeating a mutation. Initial lifecycle
reads do not trigger it; retired reads cannot notify a later selection. A projection
failure preserves the verified cancellation status and remains manually retryable.

## Legacy and activation sequencing

First qualify the native client and view lifetime against the new owner. Keep worker
availability explicit; do not enable it as an incidental UI edit. Then retire the legacy
root cascade with a reviewed transport migration. A legacy route must either return the
new explicit accepted/pending contract to a migrated client or refuse root mutation with
a precise migration outcome before writing. It must never claim Applied/completed for a
pending barrier, nor fall back to a synchronous cascade after an error. Preserve selected
child-only compatibility separately. Inventory direct application callers and their
tests, including MAF acceptance, before removing the old cascade implementation.

Refresh generated SPA contracts only on `codex/concurrent-mutations`, with its own tests
and colleague review; never merge into SPA main. Sustained/separate-process capacity and
the supported deployment envelope still gate default worker activation. The existing
finite worker, restart, backlog and locked-child tests are prerequisites, not a sustained
capacity claim.

## Acceptance

Build affected UI, module, Web and test projects with standard Release outputs. Confirm
discovery before execution. Unit/Components cases cover initial pending/complete/legacy
reads, permission/availability, delayed reads, double dispatch, selection replacement,
dispose, uncertain mutation, read failure, retrying reads only, child semantics and a
known admission followed by projection failure. Use actual host and PostgreSQL tests for
interactive authority, service scopes, original receipts and read-only observation.

Add HTTP lost-acknowledgement coverage for the already qualified native SQL uncertainty,
and prove the legacy migration outcome independently. Browser evidence must include the
real native action, pending under a held child, manual refresh to completion and selection
changes at the large desktop viewport. A renderer-only sandbox is supplementary. Retain
source/output identity, complete observed resource cleanup, dependency review, full
portability and artifact scans. Do not claim client cutover from fake-only UI evidence.
