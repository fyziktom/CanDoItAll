# API implementation review and bounded prerequisite

Evidence is explicit-commit source reading, not a local product reproduction. Preserve the
new independent API leaf, public success shapes, existing authorization owners and scoped
safe receipt ledger. The planned fixes stay in the UI lifetime family and its tests unless
new failing proof establishes a genuinely necessary adjacent change.

## Confirmed positive implementation

WS01 separates `TargetRevision` from the whole Files draft revision. Known Save adopts its
exact destination after path-only edits, while changed target/New/selection does not inherit
it. AP01 reports the corresponding real-form and owner regressions; this review did not run
them again.

AP09 copies issuer request fields/collections before the access await. AP16 preserves real
account version checks and isolates post-store logging. AP08/AP14 project safe metadata
without adding a reverse Foundation edge. AP15/AP17 contain actual boundary tests and a light
direct project reference. API authorization remains with the real owner; the findings below
do not establish successful unauthorized writes or a JWT-validation bypass.

## AP-R1 — read cleanup is incorrectly conditional on publication eligibility

Files: AP05 (primary), AP07; inspect adjacent retained CTS fields in AP02 when fixing.

`ApiPageController.LoadAsync` owns a CTS with `using` and assigns it to `reading`. Its finally
clears `reading` only if `Current(origin)` is true. A current UnauthorizedAccessException
disposes the supplied lifetime, making `Current(origin)` false. The using scope then disposes
the CTS, but `reading` still points to it. A later `Dispose()` calls `Invalidate()`, which
calls `reading.Cancel()` on that disposed source. This can throw ObjectDisposedException
through dialog/host retirement or retry. `IsLoading` may also retain an unfinished appearance.

Deterministic reproduction, no credentials required:

1. Create a page with a live lifetime and an owner that denies its current read.
2. Await RefreshAsync, allowing the using scope to finish.
3. Dispose the page (and repeat disposal). The currently retained disposed source is used.
4. Also cover external lifetime retirement while a noncooperative read is held, then complete
   it before disposing the page. Existing AP11 test disposes the page before completion and
   therefore does not cover this ordering.

Detach the exact owned handle regardless of permission to publish UI state. Use reference/
request identity so an old finally never clears a successor's `reading`. Keep cancellation
and disposal ownership explicit; do not swallow ObjectDisposedException globally, stop
cancelling reads, or leak sources to avoid the exception. Publication/error/loading changes
still need their proper generation checks. Make retirement idempotent, including repeated
session Dispose after an in-flight Load returns. Capture tokens before releasing their source
where needed; do not rely on retrieving Token from a disposed source.

## AP-R2 — a current list denial retires only the child view

Files: AP02–07, AP12–14.

ApiAccountController and ApiTokenListController create `life = new(authority)` and pass
`life` to ApiPageController. Its current UnauthorizedAccessException disposes that child,
not the management authority owned by ApiAccessSession. Cancellation flows downward only.
ApiAccessSession subscribes to the shared authority, not each child's lifetime.

As a result, a denied account/token-list read can leave ManagementFailure at None, the
other management child live, and an already issued bearer disclosure still available.
Other current-denial paths correctly retire the shared authority. This inconsistency also
exposes AP-R1 when Retry status/access later disposes the failed page.

Reproduction through the shared session and actual renderer:

1. Load a granted session; issue a synthetic one-time value. Optionally open a password editor
   in the other child to prove complete sensitive-state retirement.
2. Make the next current AccountSearch return UnauthorizedAccessException; await the actual
   Refresh action. Repeat parametrically for TokenSearch and bounded page correction.
3. Require ManagementFailure=Denied, no active issuance/accounts/owned dialogs or disclosure,
   sensitive values cleared, and no new dispatch before explicit authorized retry.
4. Retry after access is restored; create fresh children without reviving old disclosure.
   Verify close/tab-away/disposal completes without AP-R1.

Keep the operation's view lifetime distinct from its authority invalidation callback/owner.
A normal close of the token dialog must NOT retire all management. Only a denial belonging
to the still-current request and still-current authority may do so. An old A→B→A read,
closed child, or prior activation's late denial must not retire a successor. Preserve the
existing AP10/AP11 stale-denial tests and add current-denial propagation tests.

## Combined acceptance

Prove both independently, then the combined production-host path. Do not repair only the
button or catch the exception in the route. Keep original safe receipts and already admitted
writes; cancellation/denial must not be described as revoking a committed credential. Do not
add or weaken backend grants. A controlled access checker with real owner adapters is valid
for host error injection; keep separate existing secured HTTP tests for actual enforcement.
These two findings are a prerequisite within the same next bundle, not a new standalone
repair-only assignment.
