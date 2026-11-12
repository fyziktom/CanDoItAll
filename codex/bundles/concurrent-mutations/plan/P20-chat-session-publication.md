# P20 chat-session publication and title conditions

Status: behaviorally qualified; [validation](../validation/2026-10-23-chat-session-publication.md) records the test-only repair, SPA integration and explicit cleanup blocker.
Proof tier: Behavioral, with the bundle's existing source/output and resource records.

## Findings and selected boundary

The retained P18 Project browser failure occurred while opening its manager chat, before
cancellation. `SaveChatSessionCoreAsync` observes the entire workspace/index state, releases
the short workspace guard to acquire the selected session/agent keys, then rejects any
intervening workspace revision. An unrelated Process execution can therefore prevent a
new chat from opening even though the session and agent remain unchanged.

The current rename path has a separate lost-update risk: Core loads a whole session, changes
its title and submits that snapshot to `UpdateChatSessionAsync`. The store reloads current
state but does not compare it with the submitted original. A newer transcript/approval can
be replaced. The HTTP documentation explicitly describes this overwrite risk. Merely
retrying a failed save or weakening all witnesses would preserve the defect.

Keep the existing Core contract, native file owner and composition direction. Introduce
a focused title-change value with expected original title, requested title, selected agent
and session identity, and operation time. Its pure application validates identity and the
title condition, normalizes the requested title, and changes only title/updated time.
It preserves transcript, compatibility, approvals and latest execution identity. A stale
title raises a typed conflict without disclosing either title in its message. An unchanged
title is a no-op. Concurrent transcript changes need not invalidate a title-only intent.
Existing-run publications must also preserve the current session title, including late
success, failure and cancellation snapshots. Enforce that in native selected-run
publication and in the Core legacy execution adapter; first admission of a new run/session
keeps its existing initial-title behavior. Test a renamed session followed by a stale
terminal run snapshot, not only rename in isolation.

Replace the session-store whole-record update with the focused rename port. Remove the
unused broad Core save helper; migrate the four direct test callers to focused renames
or their existing selected run-detail fixture mutation. Do not keep a production snapshot
replacement escape hatch merely for fixture setup. The legacy document-store adapter
applies the same title-change value to its current selected session inside its existing
mutation boundary. The native path owns its current catalog/session validation directly.

## Publication and client behavior

For creation, retain witnesses for catalog, absence of the selected session, legacy
source and pending recovery journals. Rename captures catalog/legacy/recovery evidence,
acquires the requested session and agent keys, then reads the current session and applies
the original title condition. It does not compare an earlier whole-transcript snapshot.
Retain pending-publication target checks for the selected session and every shared index.
Re-read aggregate indexes while holding the reacquired workspace guard;
they are derived outputs, so unrelated index revisions do not invalidate the selected
session intent. Keep exact session/agent key ordering and the existing bounded acquisition.
Do not relax run, deletion or whole-workspace publication witnesses incidentally.

Rename must compare the expected title against the session read after the selected keys
are acquired; while waiting for those keys a transcript append is allowed, but deletion,
agent change, missing session and a different current title fail explicitly. Creation
still requires absence of its selected identity. The current catalog witness keeps agent
deletion/configuration changes conservative. Index updates use current counts and revisions;
no full history scan, retry loop or additional workspace-wide long lock is introduced.

Require the original title through the public rename service and HTTP request. Native
hosts capture it from their loaded immutable session before awaiting the write. Missing
HTTP expectations fail before mutation; stale expectations return 409. Preserve current
authority and stale-selection guards. Failure retains the draft and asks for explicit
reload, without automatic retry or reloading a newer title as the expected base.

Allow an initial title on new-session creation as a trailing optional argument; an existing
session ID plus a supplied initial title is invalid. The Process manager-chat path creates
its already-titled session in one publication, removing the create-then-rename partial
success. This does not yet supply durable same-source identity across two simultaneous
browser openings; that remains in the owner ledger rather than being confused with a
conditional rename. No visual layout, renderer, CSS or Components contract changes are
required. SPA has no implemented caller for this route in the feature worktree; regenerate
its reviewed API client on that branch with the final API candidate. Never merge SPA main.

## Proof and risks

Pure tests require title-only changes, preservation of all other fields, same-value no-op,
identity/blank-title validation and stale-title conflict. Native file tests hold exact
publication keys while an independent store appends a selected transcript or unrelated run,
then require preserved transcript, title and aggregate counts/revisions. Competing title
edits permit one winner and a precise conflict; deletion and pending-journal races remain
blocked. Creation of independent sessions must not lose index counts. Reopened stores and
existing usage-projection/read-scaling tests prove durable results without historical reads.

HTTP proof covers required expectation, conflict, unchanged state on refusal and continued
privacy of internal compatibility fields. Component tests capture the displayed original
title and preserve selection/lifetime behavior. The two actual native Process browser
scenarios and the existing chat submission browser topic qualify the changed producer.
Confirm exact discovery after edits; build each changed owner and affected test output
serially in Release. Refresh rendering review for touched hosts, full portability and
maintained API docs. Compare scoped Core/Persistence/Web/module architecture snapshots;
there must be no new project references or cycles and no new business partials.

Global catalog contention, whole-history index publication cost, same-source manager-chat
identity, default recovery capacity and other mutable owners remain explicit follow-ups.
The new condition is an intentional API contract change; an unmodified legacy client
must fail visibly rather than overwrite a concurrent title.

## Reviewed SPA generation adaptation

The actual exported API passes the protected Projects preflight but its current TypeSpec
importer fails on inline discriminated schemas and on HTTP response-model intersections
with named unions. Keep the reviewed OpenAPI input immutable. Lift inline discriminated
schemas into deterministic named components during preparation, retaining every variant,
required field and discriminator. The existing preflight must still pass after preparation.
Repair generated TypeSpec response intersections only when their body is a declared union
and the status model is a recognized HTTP response: emit an explicit status-code property
and body property, which preserves the wire response. Do not strip discriminators, replace
protected shapes with strings or hand-edit generated TypeScript. Qualify preparation and
TypeSpec repairs with regression tests, actual import/compile, generated serializer review
and the SPA's relevant checks. Keep all generator/client changes on its feature branch.

The first full app build also identifies obsolete consumers. Correct the export fixture
to include actual Runtime mappings and enabled optional access management, preserving the
partial export and its failed build. Repair generated cast access, discriminator optionality,
type-only imports and string-enum syntax centrally, with actual serializer roundtrips.
Keep typed API errors explicit at read boundaries. The retired unconditional Project
deletion route must surface its refusal, never report deletion or navigate as though it
committed. Its durable lifecycle producer remains a separate integration obligation.
Use typed Project status selections and the current Workflow template field names.

Workspace defaults must use their conditional snapshot contract. Keep the original
expectation paired with the draft, submit copies, and install only the returned accepted
snapshot. Rejection or lost acknowledgement retains the draft and disables repeat save
until explicit discard/reload; reading current values does not prove an uncertain commit.
Disable editing while the request is in flight and retain the form on failure. Preserve
the provider field even though this page has no provider picker. Keep decision-policy
editing with its existing native owner; the six-default route preserves that policy.
Prove the state owner with held requests, rejection and uncertainty, then check the actual
page at the existing large desktop viewport. No authentication redesign or new global
client retry layer belongs in this integration repair.
