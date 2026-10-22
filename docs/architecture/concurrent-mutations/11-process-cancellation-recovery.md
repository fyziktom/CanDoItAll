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

## Remaining activation proof

Ordinary and inherited Agent parent stopping now share one Core protocol with distinct
journal policies. Signals require the exact admitted session/source/profile generation;
publication follows a bounded dispatch-lease wait, reread and repeated current parent
authority checks. Unknown provider/effect work and committed unfinished effects remain
pending. Neither a legacy terminal status nor an empty request-scoped attachment journal
proves that an invocation stopped. The real module factory is registered; Process root/
original-receipt authority still needs its adapter before recovery can invoke it. See the
[focused qualification](../../../codex/bundles/concurrent-mutations/validation/2026-10-22-ordinary-parent-stop.md).

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

The worker must enforce finite operations/time, live/legacy fairness and cursor progress,
then use the actual Agent/Workbench stop and accounting owners. Ordinary Agent selection
currently has an unbounded candidate path; limiting the result after loading every journal
does not meet this contract. Qualify selected hydration and cold metadata cost before use.
Prove restart/takeover, unresolved-first-child progress, shared capture handling, transfer
refusal, operational pending status and both snapshot policies before enabling recovery.
