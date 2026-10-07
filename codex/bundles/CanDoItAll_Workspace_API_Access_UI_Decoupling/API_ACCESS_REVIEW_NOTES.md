# API Access source review and implementation obligations

The findings here concern the still-deferred API Access UI, not failures introduced by Core.
All reproductions must be confirmed with current source and failing-first deterministic tests.
The existing passing user-list read-back and access-denial tests must be retained. AP13, AP14.

## Inventory

The whole shipped feature is `WorkspaceApiAccessHost`, `ApiTokenAdministrationPanel`,
`ApiTokensDialog`, `ApiScopePickerDialog` and `ApiUserAdministrationPanel`. The route activates
this tree only through the API slot. Preserve status unavailable/auth-disabled/key-missing/
ready variants and the safe configured-administrator notice. AP01–AP05, WS06.

The owners are `ApiTokenAdministrationService`, `ApiUserAdministrationService`,
`ApiTokenService`, the real Web access adapter, `IApiTokenRegistry` and `IApiUserStore`.
Control-plane stores own private files and their durable-write/coordination policy. They are
not business PostgreSQL tables. AP06–AP12.

## A-01: issuance submission can change across the access await

The token form passes its mutable `model` to `Administration.IssueAsync`. That service awaits
access before `issuer.IssueToken(request)`. Subject, display name and lifetime inputs can be
edited while the first action is pending; scope parsing happens at a different point. A
single issued credential can therefore reflect a hybrid rather than the admitted form.
Capture all values, including a copy of collections and the raw lifetime validation result,
before the first incomplete await. Do not disable the entire feature to avoid reconciliation.
AP02, AP06, AP08.

Test with a held permission check and actual unblurred field events. One issuance must use
exactly the first captured request; newer draft text remains for a later deliberate issuance.
Double submit and alternate invocation paths must be rejected by operation admission, not
only button disabled state. Do not extend token lifetime or grant scopes to make a test pass.

## A-02: token reads, paging and confirmations share unqualified busy state

`ApiTokensDialog.LoadAsync` directly overwrites result/error/isBusy. Enter search can initiate
another read while a previous read or mutation is pending. A stale completion can publish the
wrong page and clear another operation's busy state. A failed read replaces data with an empty
page rather than distinguishing unavailable data. `ApplyActionAsync` has no independent
single-flight guard and reads confirmation state shared with the dialog. AP04.

Use captured query identity (search, offset, size and credential kind), independent read and
mutation lifetimes, and a separate confirmed target/action. A previous/next action commits
its query only with the matching accepted page; retained stale rows must be labelled with
their actual query. Page clamping after deletion must be bounded and belong to that same
request. Do not permit an older clamp/retry to overwrite a newer search.

Nested confirmation close/reopen, parent close, caller retirement and A→B→A must retire only
the appropriate presentation. A confirmed Revoke/Delete never changes target because the
list refreshed. Completion cannot close a newer confirmation. Preserve explicit confirmation
and lazy loading; merely opening the issuance form must not enumerate the registry.

## A-03: user persistence can be confirmed before a logging failure

Create/Update/ResetPassword persist through the real store, then call logging before returning
safe `ApiUserDetails`. Delete similarly logs after storage deletion. An exception from the
logger can hide a completed mutation and its ID/version; generic UI failure permits a blind
retry even when the owner knows the result. AP07, AP11.

Expose or preserve known safe owner facts at the true durable boundary. A narrow internal
result/committed exception or safe logging isolation is acceptable; choose the smallest
consistent design. Do not infer completion from exception type or a username search. A
failure of the durable writer's acknowledgement remains genuinely unknown unless the owner
has stronger evidence. A generated candidate GUID is not proof of storage.

Adopt the returned account ID/version in the matching live editor/receipt before any list
refresh. Preserve newer fields, expected-version behavior and missing-account refusal. Keep
password reset's actual authentication invalidation; it is not an ordinary profile edit.
Existing HTTP request/response schemas, error semantics and no-store must not regress because
an internal outcome type crosses a filter. Add compatibility handling at the real endpoint
adapter if necessary, not a new public protocol. AP14, AP15.

## A-04: one-time bearer and password lifetimes

The existing components retain bearer/password fields and do not establish caller/disposal
retirement for their asynchronous operations. Initial `canManage` is only a presentation
check; the owners recheck each method and must continue to do so. AP02, AP03, AP06, AP10.

An issued bearer is a sensitive one-time result, not a history item. Separate safe metadata
receipts from the actively disclosed value. It must never appear in a successor editor after
navigation/caller change, hidden inactive DOM, logs or generic exception payloads. Clearing
UI state cannot revoke an already registered credential; no automatic revocation or reissue
is authorized. A user may explicitly review/revoke an exact known registration after revalidation.
Do not reconstruct a lost bearer or issue again as “refresh.”

Account password belongs only to its current create/reset editor and the required captured
call. Do not retain whole password-bearing commands in generic receipts or a scenario recorder.
An uncertain password reset is not safe to repeat solely because the password field is still
available. See [sensitive state](SENSITIVE_STATE.md).

## A-05: scope picker and user paging

Preserve one canonical scope source, machine/user-selectable flags and sensitive markers.
Empty ordinary-user grants are valid; empty machine grants are refused. Reserved administrator/
session capabilities stay unavailable. Keep raw unknown/incomplete scope text on cancel or
validation failure; confirm is explicit and cannot patch a replaced owner editor. Do not
use arbitrary string normalization as permission enforcement. AP05, AP09.

User list reads already have a generation check, and the existing component test covers a
known save followed by failed list read. Preserve those protections. Complete the captured
query/pager state and retirement behavior, rather than removing tests or assuming no safeguards
exist. Both lists use 25-item UI pages; current owners cap requested page sizes at 100. User
search is capped at 128 characters. Do not invent an unbounded “load all to filter in UI” path.
The token list defaults to Machine; exposing new session-management UI is not in scope.
AP03, AP04, AP07, AP12–AP16.

## A-06: strict composition and availability

Do not inject the complete `IOptions<ApiAccessOptions>` into the extracted user renderer merely
to display the configured administrator's username. Project only the safe value and effective
status fields needed by the surface. Never include signing keys, hashes or credential bindings.
Source metadata and actual permission can independently fail; unknown status is not Open,
and a previously successful check is not permanent authority. Preserve Core's repaired
unavailable-status callback and no eager API status read from other settings sections. AP01,
AP03, AP08, AP10.

## Limits not to expand

Registry search currently enumerates its private records; user search reads its bounded account
catalog. Changing the stores to an index, DB, cache or distributed service is not this task.
Do not introduce polling on each render, revalidate entire catalogs per keystroke or eagerly
mount all dialogs. Test explicit reads/calls; measure real costs before selecting optimizations.
No new SDK/package upgrade, actor model, event bus or persisted idempotency protocol is required.
