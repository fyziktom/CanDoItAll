# Process cancellation recovery

The durable queue, finite coordinator and transfer safeguards are implemented and
qualified. Automatic recovery is not yet activated: actual stage composition, ordinary
Agent notification selection and bounded HTTP/status integration remain in progress. The existing cancellation HTTP
path still performs its synchronous cascade. See the [design and activation criteria](../../../codex/bundles/concurrent-mutations/plan/P16-durable-root-recovery.md)
and the [queue qualification](../../../codex/bundles/concurrent-mutations/validation/2026-10-22-cancellation-recovery.md)
and [58-case coordinator/transfer qualification](../../../codex/bundles/concurrent-mutations/validation/2026-10-22-recovery-coordination.md).
The [30-case paged Agent lookup qualification](../../../codex/bundles/concurrent-mutations/validation/2026-10-22-process-execution-pages.md)
proves selected journal bounds and explicit pending evidence. Its full metadata scan still
reads 82 MB and takes 1.0–1.3 seconds at 100,000 entries. An owner-maintained compact index
and cost/contention proof are required before this read port can serve an active worker.

## Transaction and ownership

Process Application owns the queue protocol and typed original intents. Persistence
implements short PostgreSQL transactions through fresh contexts. Runtime retains business
transitions and is unaware of the queue. The Process module registers the scoped adapter.
No Agent callback, file publication or provider call belongs inside a queue transaction.

The first committed root cancellation barrier stages one recovery record in the same
transaction as runtime state, events and the original receipt. A replay returns its receipt
without another enqueue. Cancellation is a durable admission barrier; it does not prove
that all descendant work, callbacks, captures or accepted effects have finished.

Recovery records retain the original root, origin, barrier command where available,
stage/cursors, cycle, pending reason and at most one exact original action. An owner must
persist that action before invoking its mutation. Unknown acknowledgements, cancellation
and lease loss retain the action. Only an original receipt or a definite no-commit refusal
can resolve it. Unknown work cannot be converted into a zero-cost settlement.

## Lease and cost contract

Claims select one due row by origin with `FOR UPDATE SKIP LOCKED`. Database time controls
expiry and due time. Checkpoint validation occurs after obtaining the row lock, using a
fresh database-clock value and the exact token, generation and progress revision. A lease
cannot be implicitly renewed by progress writes. No queue lock spans a Project/root lock
or an external owner call; staging order remains Project, runtime root, then new queue row.

The queue accepts leases from one second to fifteen minutes, retry delays from zero to
one hour, positions up to 1024 UTF-8 bytes and intents up to 8192 bytes. Commands have a
five-second database timeout. Legacy seed pages default to sixteen and permit at most
sixty-four roots. Claiming corrupt retained evidence preserves its bytes, records
InvalidEvidence, releases it for one minute and throws a typed root-specific failure.

The pending-row index is `(Origin, DueAtUtc, RootRunId) WHERE NOT Completed`. An active
lease sets DueAtUtc to its expiry, keeping owned rows out of the leading due backlog.
Legacy seeding uses a stopped-root partial RunId index, a singleton keyset checkpoint and
one bounded insert page. Cursor and inserted rows commit together. It does not hydrate
runtime plans or scan already-seeded roots through an anti-join.

## Rollout, retained evidence and repair

The Application coordinator admits one root per slice and checkpoints every stage result.
Defaults are eight operations, two seconds cooperative elapsed time and a 30-second
database lease. Item/time exhaustion releases the unchanged cursor and intent. An owner
failure preserves uncertain work and defers retry; a checkpoint failure propagates
without guessing whether it committed. Lost ownership ends the slice. Caller cancellation
leaves the lease to expire. This is orchestration, not a hard deadline on an individual
owner call; stage I/O and cold metadata costs require separate qualification.

Project transfer rejects pending queue rows and unseeded stopped roots as unsupported
source history and target residue. A completed marker only blocks target adoption.
Inspection neither parses nor discards recovery payloads. Its target lock set includes
the queue, and ACCESS EXCLUSIVE NOWAIT refuses conflicting writers before the import
snapshot. The seed singleton is outside this lock set.

Apply the schema before upgraded writers and stop old writers before completing legacy
seeding. Mixed old writers can create stopped roots behind the saved cursor and are not
a supported active rollout. No full-population backfill runs in application startup.

The migration constructs the existing-table partial index concurrently, outside the new
tables' transaction. Re-entry accepts only the exact valid index definition. A failed
concurrent construction can leave an invalid index; inspect its actual definition and
deployment ownership before explicit repair. The migration rejects it instead of silently
dropping a possibly unrelated object. Migration/downgrade testing uses isolated leases.

Back up and restore queue rows, seed progress, runtime state and owner receipts together.
Importing historical stopped roots requires import-owned enqueue or a deliberate reseed;
restoring only a completed cursor is invalid. Preserve completed queue markers while
their original owner receipts remain retained. Downgrade refuses any queue evidence or
seed progress. Stop writers before downgrade; a failed downgrade after concurrent index
removal can require explicit index restoration.

Do not delete an unresolved intent to force progress. Investigate the original owner
using the retained action and selected root/child. Pending callback work, unknown effects,
missing journals and incomplete capture accounting remain explicit recovery states. Final
root completion needs an independent persisted quiescence check, including historical
Cancelled roots; terminal status alone is insufficient.

## Finite recovery stages

The registered Application dispatcher composes four independently testable stages. It
does not start a hosted worker. Runtime selects one Process run or Agent candidate;
Decisions selects one original generation or child; SharedScope retains one original
scope transition; Quiescence independently verifies persisted work before finalization.
Every mutation retains its original intent before invoking the existing transaction owner.
The scoped coordinator can be explicitly resolved for bounded slices; HTTP cancellation
has not yet switched to this path.

Decision metadata uses root-bound keysets without material or runtime payloads. Children
remain bound to their original generation and execution contract. The registered child
stop owner supplies readiness; the terminal accounting coordinator retains actual
measured work, accepted effects and root funding. A pending first child advances with
sticky cycle evidence, permitting later children to progress. Historical unfinished
generations remain InvalidEvidence. Empty terminal decisions use the existing finalization
owner without publishing new business effects.

Shared scope cancellation moves Active through Reconciling, or cancels AwaitingCapture
directly. Each transition has its own retained action and expected revision. Capturing
remains CapturePending until the original capture owner settles measured work through
AbortCapture. Recovery cannot infer that settlement from absence of a process or journal.

The final persisted query includes root-local claims and running steps as well as
descendants, decisions, allocations and shared scope. Any unresolved cycle restarts from
Runtime with bounded backoff. A legacy Cancelled root receives an explicit no-effect
confirmation under the same runtime root transaction and full guard. Confirmation requires
unchanged business state, no other command/effect and a later microsecond timestamp; its
request flag is omitted by default from existing wire data. New root cancellation also
advances state time if accounting committed after the retained final intent, while its
event retains the original action occurrence. Either path recovers a lost acknowledgement
through the exact existing owner receipt before attempting another mutation.

## Remaining activation proof

The finite runtime stage now selects one run, retains/resolves one original cancellation
intent or inspects one Agent candidate per advance. It includes terminal runs and the
root, preserves sticky pending work, recovers original native cancellation receipts and
delegates inherited Agents only to retained Process children. The inherited stop adapter
also captures profile identity/fingerprint values across source observation. See the
[87-case qualification](../../../codex/bundles/concurrent-mutations/validation/2026-10-22-runtime-recovery-stage.md).
The stage is composed by the scoped dispatcher; no worker or HTTP path is activated.

Recovery positions retain the selected Process run, last inspected Agent execution and
sticky pending-cycle evidence across release and takeover. Advancing past unresolved
work cannot lose that evidence; later success cannot authorize completion. Only an
explicit pending cycle restart clears it. Old canonical v1 wire positions remain
byte-identical. Uncertain owner acknowledgement retains the exact original intent and
selection, including final-root receipt recovery. The [54-case qualification](../../../codex/bundles/concurrent-mutations/validation/2026-10-22-recovery-cursors.md)
does not itself enable automatic stage execution.

Ordinary and inherited Agent parent stopping now share one Core protocol with distinct
journal policies. Signals require the exact admitted session/source/profile generation;
publication follows a bounded dispatch-lease wait, reread and repeated current parent
authority checks. Unknown provider/effect work and committed unfinished effects remain
pending. Neither a legacy terminal status nor an empty request-scoped attachment journal
proves that an invocation stopped. The real module factory is registered. See the
[focused qualification](../../../codex/bundles/concurrent-mutations/validation/2026-10-22-ordinary-parent-stop.md).

The Processes module now supplies the typed Application stop port. Its adapter verifies
the original accepted claim/source, selected run/root, admitted fingerprint and stable
current profile identity/fingerprint/generation before any signal or journal publication.
Historical generation is retained across renewed current generations. The registered
adapter's native persistence proof is [qualified here](../../../codex/bundles/concurrent-mutations/validation/2026-10-22-process-stop-adapter.md).
Hosted activation and bounded elapsed/fairness proof are still required.

Process execution pages now select from an owner-maintained compact projection. The
existing publication journal retains its exact chat target until both projections are
published; interrupted publication rolls forward through that same owner. The page port
only reads, validates completeness and hydrates selected original journals. Legacy
preparation is a separate explicit owner operation; no full-journal startup migration or
reader repair is permitted. Projection identities cannot authorize effects or establish
terminal outcomes.

At 100,000 synthetic history entries the compact page reads 11.2 MB and took 103–133 ms
in two uncontrolled-cache samples, versus 82.1 MB and 1.0–1.3 seconds previously. It still
scans O(history) metadata. Actual 1/8/32-writer comparisons retain every accepted revision,
but tiny-history throughput falls 13.8% and write p95 rises 29.8%; workspace contention
still creates multi-second waits. See the [qualification and limits](../../../codex/bundles/concurrent-mutations/validation/2026-10-22-process-selection-index.md).
Do not interpret the lookup correctness pass as capacity, bounded elapsed time or fair
worker scheduling. Repeated cost investigation and cancellation-aware owner waits remain
activation prerequisites alongside the real stop/stage adapters.

Runtime/decision keysets now use composite root/identity indexes, replacing their
root-only prefixes. The real migration builds replacements concurrently before removal
and checks exact valid definitions on restart and downgrade. Invalid/conflicting named
indexes require explicit operator repair. Inspect `pg_index` validity/readiness and
`pg_get_indexdef` against the migration before taking any repair action; do not blindly
drop a conflicting operator-created object. The [24-case qualification](../../../codex/bundles/concurrent-mutations/validation/2026-10-22-recovery-selection-indexes.md)
includes all interrupted command boundaries and writes during construction. On 50,000
synthetic rows, four actual queries touch four buffers without sorting. Index size rises
from 0.45 MB to 2.60 MB each; seven insertion-batch medians show no measured regression.
These local samples do not qualify production-volume migration or whole-journal capacity.

The worker still needs measured live/legacy fairness,
bounded owner waits and publication-race qualification. The existing HTTP cancellation
cascade remains unbounded; its cutover must use the finite dispatcher and expose accepted
versus pending completion. One exhausted keyset scan does not prove that a producer
admitted before the barrier cannot publish a journal behind the cursor. Hold publication
and registration at their actual boundaries and prove safe refusal or retained pending
evidence before activation. New service scopes and lease takeover do not by themselves
qualify an operating-system process restart.

Ordinary initial Agent publication is now fenced against the root barrier. Its transient
Core reservation capability is acquired by File persistence only after the sorted
Agent/run key set, before reacquiring the workspace gate. The Process module retains
its own dispatch-guard transaction through the journal and index write. No Process
implementation dependency enters Core or File persistence. The callback checks original
SQL claim/source and current profile; it does not reacquire source Agent keys out of
order. Existing policy checks still precede publication admission and dispatch.
Standalone ordinary starts use the same reservation owner; original reservation lookup
does not demand fresh publication authority. The [87-case qualification](../../../codex/bundles/concurrent-mutations/validation/2026-10-22-held-publication.md)
reproduces the old race, proves both transaction orderings and preserves original errors
and journals after publication/release failures. It does not qualify late full-producer
registration, inherited initial publication, hard I/O bounds or capacity. The transaction
covers initial publication only; prompt preparation, file-key waits and provider execution
remain outside it. Existing global workspace contention still needs its separate gate.

Late cancellation registration is now qualified through the real Core and MAF producer.
MAF already refused provider dispatch after the saved stop; Core's stale failure handling
previously replaced cancellation. The existing admission exception now carries internal
original-session evidence, checked against the current execution. Atomic current-state
publication separately refuses every stale completion over saved background cancellation.
The entire original detail remains intact. Foreign session or code-only failures cannot
cancel another execution. The [93-case qualification](../../../codex/bundles/concurrent-mutations/validation/2026-10-22-held-registration.md)
includes both registration orderings, stale successful/failed completion, continuation
and real Process recovery. It does not qualify OS restart or inherited initial publication.

The [20-case inherited publication qualification](../../../codex/bundles/concurrent-mutations/validation/2026-10-22-held-inherited-publication.md)
now closes that initial-publication window for RunSnapshot and StepSnapshot. SQL enrollment
keeps the root and original allocation pending while the producer waits on the child's
first publication key. After release, provider dispatch refuses the stopped parent and
registered recovery accounts the original measured preparation before root completion.
Production was unchanged. OS restart, cooperative owner deadlines, fair scheduling and
the existing whole-journal cost gate remain activation prerequisites.

The coordinator now owns a linked cooperative deadline across claim, stage and checkpoint.
It awaits cancellation unwind and returns TimedOut without guessing or writing cleanup
state. Caller cancellation propagates; noncooperative operations remain awaited. Unknown
commit outcomes stay with the database until the next owner reads actual retained state.
The [53-case qualification](../../../codex/bundles/concurrent-mutations/validation/2026-10-22-recovery-deadlines.md)
covers lost checkpoint acknowledgement and real File/SQL waits through expiry/takeover.
These local samples do not qualify hard I/O bounds, hosted fairness or OS restart.

The module now registers a finite scoped hosted adapter, suppressed on MCP lanes and
configured through `Processes:CancellationRecovery` with Enabled=false by default.
Validated nested Limits retain the eight-operation/two-second/thirty-second defaults.
Preferred live/live/live/legacy turns advance even after failure. Seeding follows a
legacy recovery turn with one bounded page and its own scope/deadline; no seed-completion
cache can outlive its durable owner. Positive pacing and awaited shutdown prevent spinning
or disposal beneath an active owner. Logs contain typed status/address/count/failure type.

The [64-case qualification](../../../codex/bundles/concurrent-mutations/validation/2026-10-22-recovery-worker.md)
includes four actual OS termination/restart cases in both snapshot modes. The second
process reuses original accounting actions and receipts without duplicate charges, even
when acknowledgement is interrupted or subsequent leases expire. This closes that
specific restart boundary. Native sustained backlog fairness, whole-journal capacity
and HTTP activation remain required; the worker is not enabled automatically.

The [native backlog qualification](../../../codex/bundles/concurrent-mutations/validation/2026-10-22-recovery-backlog.md)
now exercises one/two independent providers over one database with 32 live and eight
historical roots. Every valid root progresses; the corrupt first root is deferred with
its original intent intact. Both initial 3:1 cycles are observed per worker. This finite
same-process native backlog proof does not establish sustained editor throughput,
maximum history size, multi-OS-process fairness or a latency SLA. Capacity and HTTP
activation remain open.

## Controlled index cost and next repair

The [controlled comparison](../../../codex/bundles/concurrent-mutations/validation/2026-10-22-process-index-controlled-cost.md)
reproduces 12.714% median throughput loss across three alternating pairs, despite all
600 writes and 600 reads retaining correct results. Separate tracing attributes a useful
target to 123 additional JSON publications, with only 7.25 ms of compact serialization.
The [selection publication reuse design](../../../codex/bundles/concurrent-mutations/plan/P16-process-selection-reuse.md)
keeps canonical completeness and original-journal recovery while avoiding physical
replacement of unchanged selection bytes. It must pass compatibility and repeated cost
proof before acceptance. This does not activate recovery or settle contended capacity.

The [selection reuse qualification](../../../codex/bundles/concurrent-mutations/validation/2026-10-22-process-selection-reuse.md)
implements that design inside the existing Persistence owners. Compact schema 2 uses an
optional canonical membership revision while schema 1 retains exact ordinary revision
binding. Exact persisted-content comparison suppresses only unchanged derived publication;
business journals, flushes and pending/evidence checks remain. Actual pre-index binaries
produce both interrupted original targets for successful current-binary recovery.
The 82-case selection and unchanged full portability pass. Repeated small-history median
throughput is 3.35% below pre-index production, within the original threshold. Same-workspace
32-writer tails and large allocation remain; sibling-root progress under contention and
HTTP status/activation still require qualification. Automatic review has blocked temporary
handoff cleanup; the detailed resource record remains open. Enabled stays false.
