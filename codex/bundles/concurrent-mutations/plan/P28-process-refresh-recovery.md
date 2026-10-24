# P28: capture invocation, returned-work handoff and delivery

Status: the [pure Runtime journal](../validation/2026-10-23-process-refresh-capture-journal.md)
passes its 35 selected cases and final static/source/resource closure. The
[persistent consumer and producer integration](../validation/2026-10-23-process-refresh-capture-journal-storage.md)
pass 124 distinct selected cases, full static/source/output checks and new resource closure.
The [actual previous producer](../validation/2026-10-23-process-refresh-journal-compatibility.md)
passes all ten retained-state cases without changing any of the 36 mapped Process tables.
Normal output restoration and final static checks pass; temporary junction/checkout cleanup
remains blocked: automatic review rejected the exact user-authorized junction retry too.
They follow the
[registered producer](../validation/2026-10-23-process-refresh-capture-producer.md).
The [registered delivery continuation](../validation/2026-10-23-process-refresh-delivery.md)
passes all 110 distinct selected cases, final static/source/output checks and new resource
closure. Durable queue/wake delivery, source revocation, unknown-work resolution, explicit
retry and capacity remain open. Behavioral proof tier; the public refresh/replan gate remains closed.

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

### Delivery continuation entry decision

Continue the Behavioral checkpoint with one registered Application coordinator, selected
only by original run/admission identity. It derives the actor from the validated retained
admission, consults completion receipts before stopping again, delegates original-child
stop/accounting to existing owners, then invokes the journal-aware capture coordinator.
Typed outcomes distinguish completed original work, advanced successor, stopped refresh,
source projection/review, contention, pending child work and unknown/legacy capture work.
The coordinator does not create a replacement admission or infer work from a timeout.

Cancellation owns the original child's terminal accounting. Refresh delivery may finish
its own action without asserting that the Process is cancelled, provided it has no open
capture reservation. With a reservation, Reserved/Returned must drain first; Invoking and
legacy evidence remain pending with their grant held. A nested run waits for cancellation's
runtime sweep to stop it before draining. In the capture resume path, an already stopped
runtime must bypass the live reconciliation query: cancellation may have finalized that
decision while a root capture still needs exact settlement. Original journal/receipt and
budget guards still validate the drain. This is the concrete defect this slice repairs.

No queue, schema, worker, public endpoint, retry authorization, new project reference or
Workbench dependency is introduced here. The later durable queue will consume this
continuation; it must retain admission atomically and supply bounded execution/concurrency.
The accounting owner exposes validated completed-receipt reads, sharing its existing
original-input validation. Delivery reads that receipt once and never repeats a child
stop merely to discover a completed accounting action. Retained runtime JSON is still
part of that receipt; measure it with the queue's capacity gate rather than claiming
constant-history cost. A verified cancellation that wins during stop produces contention
and hands work to cancellation; a live source-authority mismatch requires source projection.
Other invalid evidence/errors propagate rather than becoming successful delivery.

Use registered PostgreSQL/Workbench producers in both snapshot modes. Prove normal advance,
accepted original effects, stop/account/return/successor acknowledgement loss, cancellation
before accounting and during capture, cancellation-finalization before root capture drain,
unknown and legacy grant retention, and typed pending stop outcomes. Assert exact budgets,
single invocation/successor, original actor and no duplicate Project writes. Use the new
`ProcessDecisionRefreshDeliveryTests` and `ProcessDecisionRefreshDeliveryRecoveryTests`
Integration filters, with exact counts recorded before execution. Re-run the changed capture
coordinator and its boundary/concurrency owners. Registering this one scoped Application
service invalidates its DI/real-consumer proof, bounded by those integration paths; it does
not change global composition or authorize an unfiltered Stable run. Build Application,
Modules.Processes and Integration in standard Release; run final portability, documentation,
source/output/resource checks and the architecture gate. Existing broader failures remain.

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

### Queue design refinement from the continuation review

The continuation's typed outcomes make a single undifferentiated retry delay unsuitable.
Unknown capture work can persist indefinitely. Repeatedly loading its full runtime and
receipt every two seconds would turn an unavailable original owner into permanent database
load. The queue checkpoint must distinguish runnable/transient work, waiting for the
original invocation, operator review and a completed original delivery. It must not label
unknown work completed or authorize a replacement invocation.

Use one small row per original run/admission, with protocol version, original root,
database-time due/lease timestamps, token, lease generation, progress revision, a wake
revision and typed outcome/pending fields. Material and runtime JSON remain with their
existing owners. A partial due index excludes completed and parked rows. Claim uses
`SKIP LOCKED`; checkpoint requires the exact token/generation/revision and a lease that
is still live by database time. No worker holds the claim transaction while calling the
continuation. The receipt FK prevents delivery identity from surviving removal of its
original admission. Replay never resets due state or creates another row.

Park an unknown new-protocol invocation until its original Return wakes delivery in that
same narrow journal transaction. Increment a wake revision even while delivery is leased;
a pending checkpoint that observed an older revision must release as runnable rather than
overwrite that wake with a parked state. A terminal business receipt remains authoritative
even when cancellation later changes the Process. Claim/checkpoint never acquire the root
fence, so Return's root-fence-then-delivery-row order does not gain a reverse lock path.
Prove Return versus park/checkpoint, expired leases and a late original return with actual
SQL races before relying on this mechanism. Legacy unknown reservations require review;
there is no fabricated Return or zero settlement for them.

Original child work has no cross-module completion notification contract here. Retry that
typed pending outcome with a bounded, capped delay and fair due selection. Do not introduce
an Agent/Workbench dependency on the Process queue. Explicit transient failures may back
off; invalid identity/protocol evidence must be visible as repair-required. Do not add an
unbounded timer/task per item or repeatedly scan terminal history. The worker owns a finite
number of DI scopes and in-memory capture payloads. Confirm actual stop/account/capture
duration before fixing its lease/slice defaults: 25/60 seconds remain a candidate, not a
qualified bound for the entire sequential continuation.

Activation must also address admissions and in-flight invocations created before the new
delivery protocol. A replay of an old admission must not silently manufacture authorization.
An old producer does not publish the new wake revision; current older-reader retention
proof alone does not qualify mixed-version writers or allow an event-only queue to assume
every old Return wakes it. Keep the worker disabled until the rollout/retained-admission
plan and actual older-writer behavior are qualified. A bounded explicit adoption command
may reuse a validated original admission, but its ownership and idempotence require their
own proof. No such command or automatic backfill is implemented by this checkpoint.

Source revocation/profile changes are a separate gate from cancellation. A current profile
must not be substituted for the original owner to make accounting succeed. Exact returned
work remains retained if the original owner cannot be reopened. Exercise the registered
Workbench receipt reader and capturer under revocation and changed profile generations,
and distinguish an explicit repair requirement from transient contention before public
activation. The current cancellation tests do not claim to cover those cases.

An additive migration needs exact check constraints, indexes and foreign-key ownership.
Unknown/new protocol records must not become legacy never-invoked work. Run actual older
binary readers against retained old and new records; document whether each safely refuses
or remains pending. A downgrade must refuse to discard unresolved new evidence. No public
API, worker default or automatic Replan admission changes in the persistence checkpoint.

### Delivery owner analysis after storage implementation

`ProcessDecisionRefreshCoordinator.AdmitAsync` currently commits only the original
decision/scope intent. Delivery must be staged by that same UOW commit, as cancellation
already does through `ProcessCancellationRecoveryStaging`; enqueueing afterwards leaves
a crash gap. Use a refresh-specific row keyed by run/admission, foreign-keyed to its
original receipt. A request replay must retain the original row and due state, not reset
progress or manufacture another authorization.

`EfProcessCancellationRecoveryQueue` supplies the applicable lease mechanics: a partial
due index, `FOR UPDATE SKIP LOCKED`, database time, monotonically increasing lease and
progress revisions, and conditional checkpoint writes. Reuse those semantics in a small
refresh owner. Its identity is one admitted refresh, not a cancellation root; do not add
refresh stages to cancellation's position/intent codec or extract a generic queue framework.
Checkpoints describe delivery progress only. Existing stop, accounting, capture and
successor receipts remain the authority about business work.

The cancellation coordinator's default two-second slice is unsuitable for a refresh
capture with a ten-second grant. Blindly copying it would repeatedly interrupt valid
work and convert queue pressure into failed captures. Size the capture stage's outer
deadline to include the original grant, bounded Return and settlement; an initial
25-second stage / 60-second lease is a design candidate, not a measured capacity promise.
Queue SQL keeps its separate short bounds. A lease may expire while the original owner
returns known work: the journal still accepts its exact invocation outcome, while the
expired delivery owner cannot checkpoint over a newer lease. The next delivery reconciles
that result and never treats lease ownership as invocation permission.

The source audit identifies a concrete cancellation handoff. Refresh accounting's
`RequirePending` and `EfProcessDecisionRefreshAuthorityReader` require the original live
reconciliation; they must not be weakened to accept a cancelled runtime. Cancellation's
existing decision recovery owns stopping/accounting the original child after its root
barrier. Refresh delivery must inspect cancellation before attempting that live-only stage
and continue to drain its own capture journal: close Reserved as NeverInvoked, settle the
exact Returned outcome, or retain Invoking as unknown. It can finish its delivery only
when its capture allocations are closed; cancellation's decision/scope sweep then observes
the released funding and performs finalization. Cover both orderings and a crash between
those commits so neither owner waits for work only the other abandoned.

`ProjectTaskPlanProcessChildStopper` already uses the original receipt authority and
stable profile generation for both stop contracts. Its refresh entry additionally checks
the original live refresh admission; its cancellation entry checks the durable root
barrier. Keep those contracts distinct. A revoked source is not authorization to capture
new material, but historical receipt/known-work draining must retain its original identity
checks. The delivery test must prove this through the real Workbench owner and its
outcome reader, including source revocation and profile-generation change, before treating
that path as qualified. These are audited design dependencies, not passing delivery proof.

Root cancellation cannot simply mark refresh delivery complete. Reserved work can drain
as NeverInvoked; Returned work must settle/abort; Invoking remains explicitly unknown.
Otherwise the cancellation queue would wait indefinitely for capture funding that the
refresh queue had abandoned. Source/authority refusal is also distinct from corrupt
evidence: it must surface a typed review reason and bounded backoff. It does not permit a
new content read or a guessed child accounting result. Audit the existing child stopper
and accounting reader's source checks before declaring revocation recovery complete.

Delivery proof must interrupt after admission, each owner commit and checkpoint, reclaim
a lease while the original capture is still running, and exercise both snapshot modes
through the next real scheduler attempt. Measure indexed due selection with a large
retained terminal history, unrelated-root progress beside a slow capture, bounded material
memory and retry fairness. Keep worker/default activation off until those tests and actual
older-binary retention pass. The current storage tests' old-shaped request is not a
substitute for an older Application/Persistence assembly reading retained SQL evidence.

### Actual older-producer retention entry

Storage is signed at `e906b22ad`; the immediately preceding producer at `e46625b5a`
has the pure journal types but no persistent journal consumer. The existing attached
runtime and cost-baseline worktrees precede that producer and contain retained historical
work. Preserve them and prepare an isolated checkout of `e46625b5a` for this comparison.
Use standard Release outputs, the existing local-library repositories and the same
isolated PG18 server; do not copy a full output graph or start another database stack.

The candidate fixture will retain Reserved, Invoking, Returned and fully Settled refresh
states in both snapshot modes, plus two legacy reservations without journal evidence.
The previous Application/Persistence binaries must read the actual saved runtime and
stage receipts. Their existing producer should report Pending for the first three and
legacy reservations, and Advanced for a completed successor, without another capture,
settlement or row/payload change. Preserve before/after SQL and receipt fingerprints.
This is ten planned cross-binary scenarios, not deserialization through current types.

A bounded test-only probe may be compiled in each checkout; pin the older production
sources/binaries separately and prove that the probe does not modify them. Transfer only
typed case identity/context through an owned temporary input and pass the private database
connection through the child environment, never command arguments or logs. The parent
owns and bounds the child process and retains its lease until that child exits. Confirm
the exact owning discovery counts before execution. Remove both temporary probe sources
after proof and rebuild their standard outputs before claiming candidate identity restored.

Do not infer mixed-version writer safety, automatic retry, delivery or unknown-work
resolution from this read/replay check. A mismatch stops activation and requires an
explicit compatibility correction. Record exact new resources before creating them and
archive the newly created compatibility worktree after its last required use; previously
denied cleanup targets remain untouched.

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

### Durable storage and producer entry

The next coherent checkpoint starts at signed `e46625b5a`. Persist compact typed stage
columns separately from the temporary returned payload. Foreign keys retain the original
runtime admission, reservation, budget allocation and material scope; no JSON copy of the
runtime history or material catalog is needed. The original reservation command is the
stage key. Scope and budget owners are immutable dependencies and must agree with the
retained descriptor when read. Returned capture timestamps use exact UTC ticks so storage
does not change the material fingerprint. A protocol version rejects unknown row formats.

An optional typed capture command accompanies only the existing reservation or settlement
UOW command. Its absence preserves legacy request bytes. A retained journal allocation
cannot be settled by the current writer without closing its journal in the same transaction.
Begin/Return use their invocation token, not another runtime idempotency receipt. A small
shared advisory-key function retains the existing byte calculation for every participating
Process fence; transaction ordering is unchanged. Metadata mutations have a five-second
operation bound and PostgreSQL transaction/statement/lock bounds of 5s/2s/1s.

The producer reads the journal beside existing stage receipts. A missing journal for an
older reservation remains pending. Reserved can compete for Begin; only its definitive
winner invokes. Returned can settle from retained material/work after restart. Invoking
without a return remains unknown. A lost Begin acknowledgement therefore cannot start a
second capture. Cancellation before Begin can close NeverInvoked; cancellation after Begin
requires the original outcome. A captured return may later abort because source, scope,
freshness or runtime eligibility changed; its original outcome/work remain immutable.

Entry selection is the existing 62 producer/query/successor Integration cases, 35 journal
and three identity Unit cases, plus the five existing publication-fence cases in
`A_transaction_snapshot_cannot_authorize_a_generation_after_waiting_for_its_fence`,
`An_admitted_effect_holds_the_root_until_its_commit_or_rollback_while_an_unrelated_root_progresses`
and `Quiescence_committing_first_refuses_the_waiting_effect_without_a_write`.
New owning targets are `ProcessDecisionRefreshCaptureJournalPersistenceTests` (16 planned
cases) and `ProcessDecisionRefreshCaptureJournalMigrationTests` (three planned cases), all
under `CanDoItAll.Tests.Integration`. Planned total: 124. Confirm discovery from the final
test inventory before execution. The new cases must exercise actual registered producers,
reservation rollback, both cancellation orders, lost Begin/Return acknowledgement,
restarted returned-work settlement, changed invocation/result evidence, bounded metadata
SQL, allocation bypass refusal and migration retention/downgrade guards. Schema/UOW/producer
changes invalidate their owners' proof. The focused shared-fence and migration cases bound
the additive change; any shared serialization or unrelated writer regression promotes the
corresponding wider gate. Actual older-binary retention proof remains required before
recovery activation; a current reader of old-shaped rows is not equivalent.

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
