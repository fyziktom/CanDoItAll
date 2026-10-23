# P28: capture invocation, returned-work handoff and delivery

Status: the [pure Runtime journal](../validation/2026-10-23-process-refresh-capture-journal.md)
passes its 35 selected cases and final static/source/resource closure. Persistence and producer
integration below remain designed, not implemented or qualified. It follows the
[registered producer](../validation/2026-10-23-process-refresh-capture-producer.md).
Behavioral proof tier. The public refresh/replan gate remains closed.

## Problem and bounded outcome

The current producer can reconcile a lost settlement acknowledgement and inherit completed
root material after restart. A reservation alone cannot establish whether invocation began,
returned, or is still running. Replaying that reservation as permission to capture would
duplicate work. Treating its grant or elapsed wall time as measured cost would invent
evidence. Adding only a result table does not eliminate the interval between invocation and
persisting its outcome.

The next checkpoint must distinguish three recoverable observations:

| Durable observation | Permitted action |
| --- | --- |
| New-protocol reservation with no admitted invocation | Compete atomically for the first invocation, or close as never invoked if cancellation wins |
| One invocation with an immutable returned result and measured work | Retry settlement from that result; never invoke capture again |
| Invocation admitted without a returned result, or legacy reservation without protocol evidence | Report unknown work and retain its grant; lease expiry alone authorizes no replacement |

Unknown work is an explicit operator-visible outcome, not a zero-work abort or automatic
retry. It remains a separate resolution gate. A future conservative charge would need a
distinct accounting kind, retained provenance, approved policy and compatibility proof;
the existing `DecisionBudgetSettlement` represents exact closed owner work and must not be
reinterpreted. The default remains review with the original grant held. This design does
not assert eventual automatic completion for a lost invocation.

## Ownership and dependencies

Runtime owns typed stage identity, invocation/returned-result invariants and pure transition
rules. Application owns the capture lifecycle and its narrow journal read contract.
Persistence owns the retained rows and participates in the existing Process root fence.
Workbench continues implementing the content capturer; it does not acquire a Process
dependency or call a recovery queue from inside the Project transaction. No provider, Agent
journal, content read or external callback runs under a Process SQL mutation fence.

Do not add a file-based ledger or an independent reservation transaction. Reservation and
its new-protocol journal identity must commit together through the runtime UOW. Invocation
admission and returned-work recording are separate bounded metadata transactions because
the external read must occur between transactions. They must share the exact existing
PostgreSQL root advisory key and lock order, without loading or replacing runtime history.
The original invocation token and immutable result provide their idempotence; do not add
general runtime command receipts merely to obtain a retry key. A returned result must remain recordable after root cancellation or
source revocation so known work can drain; it must not thereby become publishable.

The existing cancellation recovery queue demonstrates atomic due selection, lease generation,
progress revision and checkpoint validation. Reuse those semantics at the refresh owner,
without refactoring an unrelated queue into a new generic framework. The generic Process
outbox does not yet provide the required claim/expiry behavior or original refresh identity.

## Original invocation protocol

At most two stage rows belong to one admitted refresh: root capture plus local inheritance
for RunSnapshot, or local capture for StepSnapshot. Their keys bind original root/run/action,
stage and allocation. They retain protocol version, accepted-input fingerprint, content
contract/material identity, original grant and one invocation token. Mutable budget revisions
and retry timestamps must not create another stage. Existing stage identity vectors remain
unchanged. Each journal row is selected by its original reservation command; its invocation
token identifies one actual invocation, rather than changing the reservation or material ID.

1. Reserve through the existing budget/scope transaction and create a Reserved journal row
   in the same commit. A missing row beside an older reservation remains legacy unknown;
   recovery must not backfill it as never invoked.
2. Under the root fence, admit one immutable invocation token from Reserved. Recheck the
   exact original grant and nonterminal root. Only the caller that definitively wins this
   command may begin the read. A lost invocation acknowledgement remains unknown unless
   the same live owner can prove its own invocation has not begun; a new owner cannot infer
   that from the stored token.
3. Invoke outside SQL under the grant deadline, measuring monotonic work. Classify a known
   return as captured, refused, expired, cancelled, deadline exceeded or faulted. Persist
   typed refusal/failure categories, never exception messages, credentials or provider output.
   Capture protocol validation still checks original scope, contract, material and freshness.
4. Record the immutable returned outcome and exact closed work under its original invocation
   token. An identical record replays; any changed token/body/work conflicts. A late original
   return may resolve unknown work, provided that no distinct resolution has superseded it.
   Root cancellation changes publication eligibility, not the historical fact of returned work.
5. Settle or abort from the retained result. The existing budget, successor and shared-scope
   guards remain authoritative. The settlement commit must verify the journal's exact
   allocation, outcome, material and work, then mark its handoff resolved atomically.

Cancellation that wins while the journal is still Reserved can establish NeverInvoked
atomically and settle zero work. If invocation admission wins first, cancellation must wait
for its actual returned result or report unknown work. A lease timeout is scheduling evidence,
not evidence that the invocation stopped. These two orderings require real SQL races.

## Data, retention and performance

Keep scheduling/progress metadata separate from material payload. Indexed progress reads
must never load all runtime history or all capture payloads. The maximum two stage keys are
selected by the exact admitted action; no catalog scan is needed. The UOW compares current
expectations when mutating even if earlier metadata was read consistently.

Source inspection found that `EfProcessRuntimeUnitOfWork.BuildStateQuery` includes all steps,
claims, result receipts and artifact collections, while `ReplaceState` removes and replaces
those collections even for timestamp-only mutations. Routing two additional journal marks
through that path would add history-dependent reads and writes. The journal's Begin/Return
owner must instead read the root status, original allocation and its one retained row under
the existing root fence. Reservation and final budget/scope/successor changes remain atomic
runtime UOW operations. Extract or reuse the existing advisory-key calculation without
changing its bytes; prove serialization with actual existing runtime/publication writers.
Do not turn the metadata owner into a second general runtime writer. Existing full-runtime
history cost remains a separate measured capacity gate; this checkpoint must not amplify it.

The returned row may temporarily retain one bounded canonical material payload until the
existing material/successor transaction accepts it. After settlement, retain its original
hash, identity, result category and work; clear the duplicate payload in the same commit
only when the canonical material owner or an explicit aborted-result record preserves the
required proof. Do not introduce an unbounded cleanup backlog or replay by reading the
current Project again. Refused/expired/cancelled returns need no publishable payload.

Measure the added invocation/result writes and row/payload growth against the current
producer before enabling a worker. Bound due selections and concurrent in-memory material.
Keep transaction/statement/lock limits, cancellation and resource ownership explicit. A
metadata-only preparation benchmark cannot qualify sustained concurrent capture throughput.

## Delivery and compatibility

Refresh admission must atomically retain its delivery identity so a crash before enqueue
cannot strand an otherwise eligible action. Use an indexed due queue with bounded leases,
atomic competing claims, explicit pending reasons and bounded retry delays. A delivery lease
may be reclaimed; that does not authorize repeating an admitted capture invocation. Stop,
accounting, capture and successor must each reconcile original receipts before new work.
Poisoned evidence must remain visible and throttled rather than busy-looping or disappearing.

An additive migration needs exact check constraints, indexes and foreign-key ownership.
Unknown/new protocol records must not become legacy never-invoked work. Run actual older
binary readers against retained old and new records; document whether each safely refuses
or remains pending. A downgrade must refuse to discard unresolved new evidence. No public
API, worker default or automatic Replan admission changes in the persistence checkpoint.

## Explicit retry after a known abort

The original admission currently has one deterministic capture attempt. Its abort receipt
cannot be ignored or overwritten to retry. A later manager retry needs a distinct durable
authorization linked to the same admitted action and known closed prior attempt. It must
retain original policy, source, child accounting and remaining local/root budgets. It cannot
create another Process, reset consumed work or bypass unknown work. Stable existing stage
identities remain valid for attempt zero; a versioned retry identity domain must distinguish
later authorized attempts while retaining one final successor for the original admission.

An expired completed root material plus a local abort also needs this explicit protocol:
reopening shared reconciliation must verify the current scope, not reuse its old expectation.
Retry actor/audit evidence must be retained separately from the original admission actor.
Attempt count and allocation/generation capacity remain bounded. This retry contract is
separate from re-delivering a known returned result and must be qualified before exposing
a Retry action in the UI. The journal checkpoint does not introduce it implicitly.

## Required proof and stop conditions

The first implementation slice defines the pure Runtime reservation/invocation/return/
settlement protocol. Its entry prerequisite is signed producer `3f5415f62`; it adds no schema,
queue, public API or automatic retry. The planned owning Unit filter is
`FullyQualifiedName~CanDoItAll.Tests.Unit.ProcessDecisionRefreshCaptureJournalTests.` with
35 cases (all discovered and passing): three capture/inheritance lifecycles, three never-invoked closures, five known
failure kinds, eight changed reservations, eight invalid state shapes, exact return replay,
four changed returns, invalid never-invoked cost, unknown invocation, and changed settlement.
Pure budget transitions provide the accounting consumer check. Runtime/model/test changes
invalidate this selection. The storage/UOW consumer must pass its real SQL and producer
qualification before this protocol can authorize durable recovery. No unfiltered gate is
needed for an as-yet-unconsumed pure protocol; the later schema/UOW slice has the broader
invalidation described below.

Pure tests must cover legal/illegal stage transitions, changed identities/work, duplicate
returns, cancellation versus invocation and unknown-state handling without database or host
dependencies. PostgreSQL tests must cover atomic reservation/row rollback, competing first
invocation, stale tokens, retained outcome replay, exact budget settlement and bounded SQL
shape. Registered producer tests must inject interruption after reservation, invocation
admission, returned-result commit and settlement commit; restart with a capturer that fails
if a completed invocation is repeated. Exercise both snapshot modes and both cancellation
orderings. Runtime and source authorization changes must block publication while allowing
known-work draining.

Declare exact owning filters and discovery counts after the contract inventory identifies
the affected suites. A schema/UOW change invalidates migration and shared persistence proof;
record the corresponding broader gate once at its frozen checkpoint. Source/output identity,
portability, compatibility, resources and dependent producer checks remain required.

Do not implement a guessed settlement for unknown work, weaken quiescence, introduce a
second source owner, or expose Replan merely because delivery can retry a queue item.
Public manual refresh, automatic policy admission, UI recovery and sustained capacity must
consume the qualified protocol and retain these limitations explicitly.
