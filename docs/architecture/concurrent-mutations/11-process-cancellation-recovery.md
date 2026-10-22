# Process cancellation recovery

The durable queue substrate is implemented and qualified. Automatic recovery is not yet
activated: the finite worker, ordinary Agent notification selection, transfer safeguards
and bounded HTTP/status integration remain in progress. The existing cancellation HTTP
path still performs its synchronous cascade. See the [design and activation criteria](../../../codex/bundles/concurrent-mutations/plan/P16-durable-root-recovery.md)
and [136-case qualification](../../../codex/bundles/concurrent-mutations/validation/2026-10-22-cancellation-recovery.md).

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

The worker must enforce finite operations/time, live/legacy fairness and cursor progress,
then use the actual Agent/Workbench stop and accounting owners. Ordinary Agent selection
currently has an unbounded candidate path; limiting the result after loading every journal
does not meet this contract. Qualify selected hydration and cold metadata cost before use.
Prove restart/takeover, unresolved-first-child progress, shared capture handling, transfer
refusal, operational pending status and both snapshot policies before enabling recovery.
