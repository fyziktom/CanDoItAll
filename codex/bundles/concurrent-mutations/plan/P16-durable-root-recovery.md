# Durable root cancellation recovery

Entry gate: Pass, Behavioral tier. P15 qualifies all 312 final cases, with source/runtime identity, full portability and resource reconciliation verified. P13, P14 and P15 are signed as `78d23eb92`, `31cde4ef3` and `24a3017ae`. This design extends the P11 options and does not yet authorize worker activation.

Queue substrate gate: **Pass**, with [136 qualified cases](../validation/2026-10-22-cancellation-recovery.md), including 34 new cases. Four test defects are retained and corrected through exact unchanged-production bridges. The actual finite worker, bounded ordinary notification selection, HTTP/status and transfer integration below remain required before activation.

Coordinator/transfer gate: **Pass**, with [58 qualified cases](../validation/2026-10-22-recovery-coordination.md),
including 25 new cases. Six affected Release builds and final portability pass. One
historical Projects model-count assertion was repaired without production changes;
its failure and exact bridge remain retained. Continue with the real stage/Agent
integration and remaining activation criteria below.

## Ownership and delivery order

Process Application owns typed recovery positions, original intents, scheduling and the
finite coordinator. Process Persistence owns only queue transactions, exact metadata
selection and database leases. Process Runtime remains responsible for business/runtime
transitions. Existing Agent/Workbench child stoppers and outcome readers own their journals,
callbacks and measured work. A small Process module hosted adapter invokes the application
coordinator through a DI scope. No worker or Agent dependency enters Runtime/Persistence;
no new project references or broad coordinator partials are needed.

First qualify the queue/lease/backfill substrate and its migration. Then implement finite
stage selection and actual owner composition, qualify recovery races/restarts, expose
operational pending reasons, and replace the existing unbounded HTTP cascade. These are
checkpoints in one continuing outcome, not a reduction of E2E scope.

## Durable record and original intent

Use one row per root, with restrictive FK to its runtime state. Retain origin
LiveBarrier/LegacySeed, original barrier command when available, due time, lease token,
generation, expiry, progress revision, stage/keyset position, cycle count, last typed
pending reason, and at most one original intent. A completed row remains a historical
recovery marker. Do not delete it while original owner receipts are retained.

The queue is staged in the same existing runtime transaction that first persists a root
cancellation barrier, after persisted authority checks and before SaveChanges. Replays
return the original receipt before staging. The queue cannot extend transaction lifetime
across file, Agent or provider work. A failed flush rolls back root, event, receipt and
queue together. A direct already-quiescent root cancellation can use the same marker.

An intent is a closed typed set: cancel a selected runtime run, account a retained child,
finalize a terminal local decision, transition a shared scope, or finalize the root.
Accounting and finalization retain the entire existing request, including action ID and
original decision/budget expectations. Scope transitions retain their exact expectation.
Every intent retains the original command context and fixed System recovery actor; a
legacy seed never invents a user cancellation receipt. Wire serialization uses primitive
IDs with explicit typed reconstruction, bounded UTF-8 size, strict version/kind/field
checks and canonical round trips. Never deserialize read-only ID wrappers implicitly.

Persist the intent under the lease before invoking the owner. Every successor replays
that exact intent before selecting new work. Lost acknowledgement, cancellation, timeout
and lease loss never authorize replacement or clearing. Clear only after an original
receipt or a definite owner refusal that proves no commit; optimistic concurrency is a
known negative only where the transaction owner guarantees rollback. Corruption and
unknown exceptions retain intent and report a typed pending reason. Unknown external
work does not receive an accounting intent or a fabricated zero settlement.

## Clock and lease ordering

The database owns durable time. PostgreSQL documents that statement_timestamp is fixed
at statement entry, whereas clock_timestamp advances during execution. Therefore lease
validation after a possible lock wait cannot rely on the old statement timestamp.
Use a short exact-row lock with SKIP LOCKED and a subsequent database-clock conditional
write while holding that row. A busy row produces an explicit not-owned/busy result;
no wait on a worker's file operation or another recovery slice is allowed. The timestamp
predicate is checked at the mutation's linearization point, not against a host clock.

Claim one due root from a matching (Origin, DueAtUtc, RootRunId) pending-row index,
FOR UPDATE SKIP LOCKED. Record a new random token and increment generation; set DueAtUtc
to the lease expiry while owned so active leases do not remain at the head of the due
index. Checkpoint requires root, token, generation, progress revision and unexpired
database lease. Advancing twice, stale-token writes and a pre-expiry request delayed
until after expiry must fail. Keep original intent and cursor through a takeover.

No advisory/root runtime lock is held during a queue transaction, and no queue row lock
is held while acquiring a runtime root or Project lock. Thus staging order remains
Project -> runtime root -> new queue row; recovery releases the queue lock before any
owner call. No reverse edge is introduced. Local elapsed budgets use monotonic time and
are cooperative; they cannot preempt a third-party callback ignoring cancellation.

Primary references checked 2026-10-22:
- https://www.postgresql.org/docs/18/functions-datetime.html#FUNCTIONS-DATETIME-CURRENT
- https://www.postgresql.org/docs/18/sql-select.html#SQL-FOR-UPDATE-SHARE

## Finite stages and fairness

Runtime stage visits bounded keyset pages by RunId, including retained terminal runs
whose stop notification may have been interrupted. Do not infer notification from a
Cancelled state. Cancellation and notification completion are distinct; replay the
original cancel intent first, then repeat the idempotent signal before advancing.

Decision stage visits heads by DecisionId using (RootRunId, DecisionId), then individual
children of the selected generation using the existing primary key. Retain the selected
head plus child cursor so one 64-child head cannot reset the per-invocation item limit.
Unknown first children advance the cursor; closed outcomes can prepare exact accounting
intents. A finished child scan attempts P15 finalization where appropriate, then advances
to another head. Historical mismatched/open generations remain explicit corruption, not
silently treated as the current generation. Read only metadata until an exact owner is
selected; never enumerate all child journals to select work.

After local decisions, reconcile/cancel the shared root scope through its existing
complete membership and accounting guard. A pending capture requires actual measured
capture work and explicit abort; no worker can infer zero. Finally query P13's independent
quiescence guard once per completed cycle and finalize the root only when clear. An
already-finalized legacy root still needs that independent check before its queue closes.

Set explicit roots/items/pages and cooperative elapsed limits. A completed scan with
pending work schedules bounded backoff and resets the next cycle, rather than hot-looping
the first unresolved item. Keep a typed reason and selected original address for operator
inspection; do not embed secret payloads or arbitrary exception messages.

Schedule live and legacy classes with an explicit finite ratio (initial candidate 3:1),
querying the other class only when the scheduled class is empty. This is empty-queue
scheduling, not failure fallback. Both new live cancellations and historical recovery
must progress under sustained backlog. Multiple hosts have independent cursors/quotas;
tests must not turn a local quota into an unsupported global latency guarantee.

## Bounded legacy seed and migration

Use one dedicated seed checkpoint with a keyset RunId cursor, completion flag and exact
revision. Under its short row lock, select at most one bounded page of stopped roots
from a partial RunId index where RootRunId=RunId and status is CancelRequested/Cancelled.
Insert missing LegacySeed rows with ON CONFLICT DO NOTHING and advance the cursor in the
same transaction. This bounds scanning by selected roots, not an unbounded anti-join of
all previously seeded rows. A completed empty page records completion. New barriers are
already atomically enqueued, including random IDs below the legacy cursor.

Schema rollout must precede upgraded writers; enable one-time seeding only after all
writers use the barrier transaction. Mixed old writers can create stopped roots behind
the cursor and are not a supported active rollout. Backups must preserve queue, seed
cursor and owner receipts together. Legacy import/restore needs an explicit seed reset
or import-owned enqueue; restoring only the cursor is invalid. No entire-population
startup UPDATE/INSERT or full historical payload hydration is allowed.

Create needed existing-table indexes concurrently with a documented deployment window;
measure construction/write cost. New queue tables are additive. Downgrade refuses
retained recovery records/intents/progress; it must not discard unknown work. Actual
migration and restored old fixtures need proof, not only EnsureCreated.

## Required acceptance before activation

- Atomic barrier/enqueue, duplicate replay, rollback before/after flush and lost commit
  acknowledgement through actual runtime owner and isolated PostgreSQL.
- Two independent owners, finite claim, due ordering, active-lease exclusion, lease
  expiry/takeover, stale token/generation/revision, lock-delay expiry and retained intent.
- Strict typed wire corruption/version/field rejection, exact replay after restart,
  unknown settlement retention and actor/action isolation.
- Seed empty/populated/multi-page progress, failure rollback, conflicting live enqueue,
  IDs below cursor, host restart, migration/restore/downgrade refusal and index plans.
- Finite item/page bounds, unresolved first child/root fairness, live versus legacy
  backlog, cancellation, repeated slices and no lock held across owner calls.
- Actual child journal/Agent callback stop, terminal accounting, empty-generation close,
  shared scope and root completion across lost acknowledgements and host restart.
- Historical root shape and index/write overhead measurements. Healthy local budgets
  allow at most 256 allocations; the risk is many budgets/heads/descendants, not invented
  millions of valid allocations in one budget. Observe full-runtime snapshot load cost.
- Operational pending/resolution flow and bounded HTTP cancellation response, followed
  by integrated producer/Workflow/settings/snapshot policy proof and final Stable gates.

The reviewed defaults and implementation acceptance are fixed below. Keep recovery disabled until that actual owner integration qualifies;
the queue substrate alone must never be advertised as completed automatic recovery.

## Concrete substrate contract reviewed against current source

Use one Application port for the cohesive queue: claim one root in an explicit origin
class, checkpoint one owned root, and seed at most one legacy page. The implementation
uses a fresh Process context and short transaction per operation; PostgreSQL is required
for lease operations. Existing in-memory runtime fixtures may stage queue entities but
must not be advertised as proving durable queue concurrency. Do not change runtime
transition constructors to inject a queue service: staging is part of Persistence's
existing transaction, through a small internal row/codec owner.

The position contains a typed stage, nullable RunAfter, DecisionAfter, CurrentDecision
reference and ChildAfter. Validation permits only fields belonging to that stage:
Runtime uses RunAfter; Decisions uses completed-head and current-head/child cursors;
SharedScope, Quiescence and Completed carry no row cursors. A current decision retains
its generation while root cancellation prevents refresh. Historical inconsistent open
generations are reported for owner resolution, never silently closed as the current one.

The checkpoint contains the next position, retained intent, typed pending reason,
optional explicit intent resolution (Committed or RefusedWithoutCommit), and optional
retry delay. Use actual locked stored state for transition validation, not caller-supplied
old values. An unresolved intent cannot be replaced or have its position advanced. A
new intent must preserve the current position. Clearing requires the exact current action
identity and an explicit known resolution. Completed requires no intent, no pending
reason and an already-finished quiescence operation. Release with an uncertain intent
keeps its bytes unchanged. Each successful write increments progress revision; claim
takeover separately increments the lease generation.

Store the bounded strict position/intent wire text alongside indexed root/origin/due,
lease token/generation/expiry, revision and completion fields. Keeping JSON as canonical
text avoids JSONB rewriting receipt identity. The database has length/shape/nullability
constraints and a restrictive root FK; the codec validates full semantic binding and
rejects unknown versions. Queue records carry metadata and expectations, never material
payloads, prompts, credentials or complete runtime snapshots. A progress operation must
use a conditional write with clock_timestamp after its exact row lock, so a slow operation
cannot extend an already-expired lease merely because it entered before expiry.

Legacy seed state is one separate row with a typed singleton identity, LastRootRunId,
revision and completion. Take at most 64 IDs per call (default 16); insert them in one
bounded parameterized statement with ON CONFLICT DO NOTHING and advance the seed in the
same transaction. Do not perform one network round trip per root. Seed contention is an
explicit Busy result. A missing seed checkpoint is corruption, not an empty queue.

Current funding code mirrors every local allocation settlement into its retained root
funding allocation in the same transaction. Recovery therefore reuses terminal accounting;
it must not settle a second synthetic root grant. Local finalization only changes the
generation phase. Root shared capture still requires its separately measured abort.

P15 also exposed and repaired P13's pre-flush ordering for a valid atomic cancelled
strategy result. New queue staging must support that final-root transition too, while
retaining its late full-root quiescence obligation and rollback. A queue record for an
already-quiescent Cancelled root can close without owner side effects after verification.
The queue must never be staged from a returned historical command replay.

For the substrate checkpoint, prove schema/constraints, runtime staging/rollback/replay,
strict original-intent preservation, two independent owners, expiry/takeover and delayed
checkpoint refusal, finite seed progress/conflicts/restart, actual migration downgrade
refusal and relevant index plans. All worker options, finite stage queries, operational
read/resolution APIs and HTTP cascade replacement remain the immediately following
integration in this same ongoing task; they are not silently waived by substrate proof.

## Concrete implementation and rollout decisions

The queue port has three operations: ClaimAsync, CheckpointAsync and SeedLegacyAsync.
Lease duration is bounded to 1 secondâ€“15 minutes (worker default 30 seconds); database
commands have a 5-second timeout. Retry delay is 0â€“1 hour. Seed pages default to 16 and
cannot exceed 64 roots. Positions and intents are bounded to 1024 and 8192 UTF-8 bytes.
Worker defaults are 8 selected items per slice, a 2-second cooperative elapsed budget,
and a 3:1 live/legacy claim quota. No queue lock spans an owner invocation. Unknown
intent outcomes survive expiry and takeover byte-for-byte. Substrate tests may use
short leases; they must never change system/database time or production defaults.

Intent cases retain typed cancel-run, account-child, finalize-decision, change-scope and
finalize-root requests. Context actor is the fixed System recovery owner, correlation
is the root ID, and the exact action/timestamp survives replay. Scope intents permit
BeginReconciliation and Cancel only; measured capture abort remains with its owner.
A checkpoint validates the actual locked state, not the caller's cached intent. Root
completion requires a resolved final-root intent from Quiescence; the executor must
perform the independent quiescence check even for a legacy already-Cancelled root.

Build the stopped-root partial index concurrently before transactional new-table DDL.
A retry may reuse only a valid index with the exact table, columns, predicate and index
kind. An invalid or conflicting same-name index raises an actionable migration error;
IF NOT EXISTS alone is insufficient. A failed concurrent build may leave such an index
for explicit deployment repair. Downgrade first refuses retained queue rows or seed
progress, then removes its owned index concurrently before transactional table drops.
Migration/down execution requires stopped writers and occurs only on an isolated test
lease during proof. No ordinary application database is a migration test target.

## Architecture and testability contract

Application owns queue contracts, intent/position validation and later scheduling.
Persistence owns two EF entities, their mappings, a strict primitive wire codec, the
short-transaction queue implementation and atomic staging. Existing UoW adds one staged
row obligation after persisted authority validation. Runtime remains queue-unaware.
Process module registers the queue; it starts no worker in this checkpoint. There are
no new project references, generic repositories, broad coordinator partials or Agent
dependencies. Pure intent contracts/codec have direct tests; queue behavior uses two
independent contexts and the actual PostgreSQL database, not a simulated lease clock.

Owning filters: ProcessCancellationRecoveryContractTests (9 facts),
ProcessCancellationRecoveryCodecTests (7 facts), ProcessCancellationRecoveryQueueTests
(7 facts), ProcessCancellationRecoverySeedTests (3 facts),
ProcessCancellationRecoveryAtomicTests (4 facts), and
ProcessCancellationRecoveryMigrationTests (4 facts): 34 planned new tests. Build-backed
discovery must confirm these before execution; any changed inventory is recorded before
launch. Regressions are ProcessDecisionCompletionPersistenceTests (38),
ProcessRootQuiescenceTests (19), ProcessRootWorkAdmissionTests (14), and
ProcessDecisionPersistenceTests (31), covering the staged owner and late rollback.
Expected total is 136 cases. Queue/model/codec/UoW changes invalidate their owning lanes;
any production repair repeats all affected lanes. Migration generation/model parity,
actual upgrade/re-entry/down refusal, Release builds for changed projects, scoped
CodeAnalytics dependency review and final portability enforcement are required. The
unfiltered Stable gate is reserved for the integrated E2E frozen candidate; this substrate
checkpoint alone does not trigger it or claim final product closure.

Corrupt retained wire evidence is an explicit typed root failure. Claim records InvalidEvidence, releases the lease and defers that row for one minute without changing cursor/intent bytes, then throws the typed error. This avoids a malformed first row starving healthy roots; it does not reinterpret or repair unknown work. The finite worker must count that failure against its slice budget and expose it for owner repair. One additional SQL test covers this negative path, bringing the planned new cases to 34.

## Finite coordinator and transfer integration

The queue substrate is signed as `13de74201`. The next implementation keeps scheduling
in a small Application coordinator and one real stage-operation boundary. A stage prepares
an intent when none exists, or replays the saved intent. The coordinator checkpoints each
result before calling the next stage. Its tests must prove that no owner action runs before
the prepared intent was saved. This replaces neither Runtime business transitions nor the
existing child stop/accounting owners. No new project reference or partial class is needed.

Use typed validated slice limits: eight operations, two seconds cooperative elapsed time,
30-second database lease, 25 ms yield and two-second failure retry by default. Maximum
operations is 64; maximum slice duration is 30 seconds and must be shorter than the lease.
On item/time exhaustion, release the unchanged position and intent. A NotOwned result
ends the slice. Caller cancellation propagates and leaves the lease to expire. Unknown
owner exceptions preserve the original intent and report a typed pending reason; a failed
checkpoint is never interpreted as proof of no commit. Log root, stage and exception type,
without external exception messages or journal contents. The hosted adapter will own the
per-host 3:1 live/legacy turn schedule and one seed page per legacy turn.

Transfer inspection must recognize pending recovery even when runtime status is already
Cancelled. Pending queue rows and stopped legacy roots without a queue marker block source
export and target adoption. Completed markers still make a target nonempty but do not by
themselves block source history export. Inspect metadata only; corrupt intent bytes must
not be parsed or discarded by transfer. Add the recovery entity to the target lock set.
Do not lock the seed singleton: inspection does not read it, and seed-to-queue order must
not be reversed. The actual exclusive import uses ACCESS EXCLUSIVE NOWAIT before its
Serializable snapshot, so an active runtime/queue writer makes import fail and release
its acquired locks instead of waiting in an inverse order. Prove this with independent
PostgreSQL connections and preserve the ordinary source snapshot contract.

Update the Process owner-model expectation in its transfer test from the historical 22
entities to the current 34, based on the qualified scoped model; do not suppress model
isolation checks. Pure coordinator tests cover item/time limits, saved-intent ordering,
unknown acknowledgement, cancellation, lost ownership, corrupt evidence and empty classes.
The owning transfer class covers all current inspection scenarios plus live/legacy pending,
completed, unseeded legacy and concurrent import/writer cases. Final exact discovery is
required before execution. Build Application, Process module, Workbench, Web and the two
owning test assemblies in standard Release outputs. Reuse the fixture, run portability
and review scoped architecture. Broader Stable remains at the final integrated checkpoint.

Automatic worker activation remains dependent on the real stage adapter, bounded ordinary
Agent notification lookup/cursor, shared-scope/root completion, HTTP/status, fairness and
actual restart proof. Candidate lookup currently ignores Take and must not be presented
as bounded. A cache can reduce repeated metadata reads but does not establish cold
history-independent cost; qualify that owner choice before enabling it.

### Notification lookup decision to qualify

Keep the canonical Agent journal with its existing file owner. Introducing a second
canonical journal or a Process-owned copy would add a cross-store commit obligation and
would not establish whether a provider action completed. A bespoke filesystem tree is
also disproportionate to this lookup. The next prototype will page the existing owner
metadata and hydrate only the selected journals. Its contract must distinguish a current
page, exhausted selection, incomplete legacy projection and invalid/missing index evidence.
It must never call the current automatic full-journal index rebuild in a recovery slice.

This reduces journal work; it does not make cold metadata reads independent of history.
Measure current and proposed reads at representative larger histories, warm and cold,
with concurrent publication. Record bytes, journal reads, elapsed time and allocation,
not only returned-item counts. Cache reuse must be tied to the owner's actual publication
evidence, and invalidation under sustained writes must not cause silent omissions or
starve all recovery roots. If the measured cold/read-contention cost does not meet the
slice/latency budget, this design remains unqualified and requires a separate durable
index decision before worker activation. No arbitrary low history cap is an acceptable
replacement for the measurement or for original journal evidence.

For each selected ordinary execution, signal its exact registration and use its journal
owner's dispatch lease, original Process lineage and current root cancellation barrier.
An absent registration is not proof of stopped work. Open provider dispatch, unknown
effects, committed effects awaiting completion, and inherited planning children require
their existing owner outcomes. The broad compatibility observer's direct force-failure
write cannot serve as this recovery owner. Its replacement must retain these negative
cases and source/profile generation fences in the actual producer tests.

### Stage cursor requirements discovered during integration

The stage adapter must distinguish the last operator-facing pending reason from a
sticky indication that this cycle observed unresolved work. A later successful item
must not erase an earlier unknown Agent outcome and allow root completion. Restart the
cycle explicitly after quiescence if any selection was unresolved; reset the cycle
indication only on that restart. Keep successful receipt replay independent of this
diagnostic state. Qualify omission-compatible wire additions against the already-signed
canonical v1 records before changing the position contract.

Runtime selection also needs a selected run and its ordinary notification cursor before
advancing RunAfter. Cancellation must retain a prepared original runtime command until
its receipt or definite refusal is established. Exact stop notifications may repeat only
through their existing idempotent, lease-protected owner; they cannot invent accounting
or terminalize unknown work. Selected Agent/child outcomes must be verified before any
accounting intent is prepared. The final root check includes persisted local root work
as well as descendants, and a clean SQL guard alone does not substitute for completion
of the ordinary Agent notification scan.

### Paged Agent lookup implementation entry

Prerequisites are signed as `13de74201` and `8cd7ebab8`. Implement the lookup as a new
Core read port with typed Process/continuation GUIDs, a page limit of 1–64, explicit
Ready/IndexUnavailable/PublicationPending/LayoutUnavailable outcomes, and an exhausted
marker that is valid only for a Ready page. Persistence owns a small metadata selector;
the existing store supplies only its workspace guard and delegates. No new partial,
project reference, second journal, implicit fallback or automatic index repair is needed.

The selector reads a narrow projection of the canonical execution and chat indexes,
checks their version/revision/count agreement and unique nonempty run identities, then
retains only the smallest Take+1 eligible IDs after the cursor. Hydrate at most Take run
records, including old projection rows conservatively because their Process lineage is
not yet trustworthy. A future projection version or incomplete index is explicit pending
evidence, never exhaustion. Read selected records afresh; file timestamps alone are not
an authority for cache reuse. No journal details, effects or unrelated run files are read.
Pending publication journals are reported before selection, without invoking recovery.

The owning proof is a new FileSandboxWorkspaceProcessPageIntegrationTests class (planned
18 cases, including two diagnostic history sizes) plus the existing twelve-case
FileSandboxWorkspaceProcessQueryIntegrationTests class. Cover restart pagination at
three page sizes, unavailable/stale/legacy layouts, old/future projections, duplicate
identities, same-stamp edits, pending publication, invalid input, missing/corrupt selected
records, empty storage and profile isolation. Diagnostic metadata populations exercise
cold/warm cost and selected-read counts; they are not producer-throughput evidence.
Confirm exact discovery before launch. Build affected Core/Persistence, the Agent module
registration, Web composition and the owning Integration assembly in standard Release,
with final portability and resource closure. Build them in that dependency order; existing
qualified unchanged dependencies allow `--no-dependencies`, and the final identity check
must verify that the Integration output contains the refreshed production assemblies.
No automatic recovery activation follows from these thirty selected cases alone.

### Lookup result and compact projection decision

All 30 cases now pass; the first prototype's content-version comparison was incorrect
and its 15 failures are retained. The final reader still consumes 82,104,264 metadata
bytes at 100,000 unrelated entries, allocating about 44 MB and taking 1.0–1.3 seconds.
These are two diagnostic samples with uncontrolled filesystem cache, not distributions
or cold-cache/concurrent-writer proof. Keep worker activation blocked by this finding.

The next change belongs to the existing file-store projection owner. Derive a compact
selection projection from each exact committed chat-index target and publish it before
that owner clears its existing prepared journal. Retain only the execution identity,
parsed Process identity or explicit unknown lineage, and completeness/version evidence.
Do not copy authority, effects or outcomes into the projection. The read port continues
to hydrate original selected records and reuses no timestamp-based record cache.

Prefer this single derived projection over a second canonical journal, a database bridge
with a cross-store commit obligation, or a bespoke filesystem search tree. The prototype
still has O(history) metadata cost; quantify the compact file's size, allocation and lock
hold at 20,000/100,000 entries before accepting it. Compare actual owner writes and reads
under contention, including failure/restart between canonical, chat and compact index
publication. If compact projection cost still violates the slice/latency budget, reopen
the index decision rather than declaring paging alone sufficient.

Audit every publication path: initial/bulk save, run creation, existing-run update,
chat-backed creation, session update, Agent deletion, projection rebuild and retained
prepared-journal recovery. A revision/count mismatch, missing projection, future format,
duplicate or invalid identity is explicit unavailable/corrupt evidence. Never return
exhaustion from it. Legacy upgrade is a distinct owner preparation step from trusted
complete metadata; it may not hydrate all historical journals inside a recovery slice.
The page reader performs no migration, repair or publication.

For the write-cost comparison, preserve the already-qualified `4a35d5311` production
DLL/PDB identities before building the changed production source. Add a four-case
diagnostic using only its existing public store/read APIs: histories 0/2000 with one
writer, plus 2000 with 8/32 writers. Populate actual run records through the owner,
warm up 20 writes, then measure 100 single-attempt writes and 100 concurrent page reads.
Record accepted/conflicting attempts, p50/p95/p99, elapsed/throughput, allocations and
physical read bytes; verify each accepted update through its original final revision.
Do not retry conflicts or infer queue wait/WAL/server CPU. Repeat the identical class
after the compact-index build, with unchanged source and configuration. A test-only
baseline build uses the previously qualified production binaries intentionally; retain
their exact provenance and never label that baseline as validation of the new source.

The compact-index candidate selects 66 cases before execution: 23 new index-owner cases,
18 updated page cases, 12 unchanged query regressions, the four identical cost diagnostics
and nine existing publication-recovery cases covering chat creation, generic creation,
existing updates and Agent deletion. Build Core, Persistence, Agent module, Web and
Integration in dependency order; confirm exact discovery in Release. New owner cases
include legacy preparation, incomplete/future source refusal, missing required fields,
same-stamp membership corruption, orphan projections, explicit repair, real bulk/session/
Agent deletion writes, and actual filesystem failure between chat and compact publication
with retained-journal roll-forward. Reuse `tool-admission` and
`workspace-publication-recovery` temporary families; no new host or database is needed.

### Compact index result and progression

The final 66 cases pass. The first owner's 22/23 result was a test setup error: Agent
deletion correctly refused the fixture's active run. The corrected terminal fixture and
all selected tests were rebuilt/discovered/rerun. The four-case baseline used exact signed
production binaries from 4a35d5311; cost-test source is identical across both versions.
See [the checkpoint](../validation/2026-10-22-process-selection-index.md) for the full
publication audit, negative proof, source/runtime identities and retained measurements.

Compact 100,000-entry metadata is 11.2 MB instead of 82.1 MB; page samples are 103–133 ms.
All 400 candidate writes/reads preserve accepted revisions, but the tiny-history sample
loses 13.8% throughput and adds 29.8% write p95. This exceeds the investigation thresholds;
do not close performance or activate the worker. Existing workspace serialization still
produces multi-second waits. Next qualify bounded cancellation-aware owner operations and
sibling-root fairness, investigate small-history cost with controlled repeats or a measured
redesign, and implement exact ordinary stopping and the real stage adapter. Preserve
sticky pending-cycle state, original intents and notification cursors across takeover.
This checkpoint permits dependent correctness integration only; R1/R4/R5/R6 remain open.

### Ordinary selected Agent stop entry

Prerequisite: signed compact-index checkpoint 6131bd540 (behavior only; capacity remains
open). The current compatibility cancellation observer force-writes active runs and
cannot prove stopped provider work. Do not use it in durable recovery. Core will own an
ordinary background-journal stop policy, and a shared internal parent-stop protocol will
own the existing exact signal, 250 ms dispatch-lease acquisition, current-state reread and
authority-checked publication sequence. The inherited policy retains its planning work
and terminal-generation checks. This is one protocol with two semantic policies, not a
second execution runtime or a new project/interface hierarchy. Use explicit delegates at
this internal seam; neither policy may delegate behavior back into a runtime partial.

The existing unreleased inherited-only outcome enum becomes a shared parent-stop outcome
across its Core/Workbench/test callers; no persisted wire field uses that enum. Ordinary
authority binds the exact admitted session/source/profile and execution fingerprint, and
requires a caller-owned stopped-parent observation before signalling, after lease wait
and at publication. The Process adapter must later supply original receipt/root-barrier
and stable current profile-generation checks; the Core policy does not reference SQL,
Process runtime, Workbench or UI. No new project reference or partial file is planned.

Absence of a registration or a released dispatch lease is not stop proof. Missing journals,
open provider dispatch, executing/reconciling/unknown effects and committed effects awaiting
completion remain typed pending outcomes. An original terminal outcome is accepted only
with consistent terminal fields and a closed journal. Legacy background schema without
provider-dispatch evidence may stop only before invocation began; abandoned started legacy
work remains unresolved. Rejecting undispatched proposals uses the existing cancellation
transition and retains all original inputs, results, approvals and accepted effects.
Inherited planning cannot be interpreted as an ordinary background run. No external work,
model call or replay is introduced by stopping.

Behavioral proof uses new ordinary parent-stop Unit and Integration classes, existing
inherited parent-stop Unit (20) and Integration (9) characterization tests, and background
identity Unit tests (15). Reuse pure in-memory store/lease seams and the real background
journal fixture; do not construct the full runtime for policy tests. Prove current
authority loss at each boundary, wrong original bindings, cancellation/lease contention,
late changed journal state, never-dispatched cancellation, known terminal replay, unknown
provider/effect outcomes, committed-but-unfinished work and lost publication acknowledgement.
Declare the final new-class discovery counts before running. Build affected Core and
downstream owners in standard Release, verify copied binaries, then perform architecture,
portability and resource closure. Existing broader capacity and final Stable gates remain.

Before execution, the selected discovery count is 84: ordinary parent-stop Unit 29 and
Integration 11, inherited parent-stop Unit 20 and Integration 9, and background identity
Unit 15. Exact class-prefix filters are declared in the owned `c4-ordinary-parent-stop`
manifest. The composition case resolves the real Agent module registration over the
selected isolated store and shared cancellation registry across two scopes; it does not
claim a running HTTP host. Refresh Core, Workbench, Agent module, Web, Unit and Integration
in that dependency order. The existing background fixture is made assembly-visible for
reuse; no production behavior branch or second disposable host is added.

Signal-path review found that the existing registry selects only by execution GUID.
Parent recovery additionally needs the original admitted session, including source and
profile generation. Add an exact-session overload; the registry captures the immutable
session at registration and the shared parent protocol uses that overload. Preserve the
existing GUID/broad compatibility APIs for their existing callers. A legacy or mismatched
registration cannot count as a successful parent signal; normal dispatch-lease/current
authority checks still govern any subsequent stop. Add negative source/profile/generation
registry cases and an end-to-end protocol case. This extends the selected proof to 105
cases (30 ordinary Unit plus the 20-case cancellation-registry topic); do not activate
the compatibility observer as the recovery worker. The initial Integration build's
public test parameter exposed an internal boundary enum; retain that build failure and
use the existing xUnit integer-to-typed-enum pattern for that test boundary.

Final pre-execution review adds the supported effect-journal schema to both idle-stop and
incomplete legacy-segment cases: 32 ordinary Unit cases, 107 total. A later schema number
alone cannot prove that a legacy provider invocation was recorded. The initial Unit build
also attempted a with assignment to a read-only session-reference property; the fixture
now uses its explicit immutable constructor. Both compile failures remain retained.

The 107-case candidate passed, including static, identity and resource closure. Final
source review then found a quiescence gap: its terminal-record return preceded the legacy
provider-evidence check, so an old compatibility observer's forced terminal status could
be accepted without original invocation proof. Reopen that candidate. Move legacy
uncertainty before terminal replay and add both Cancelled and Succeeded terminal-record
regressions. The confirmed candidate selects 109 cases (34 ordinary Unit, other topics
unchanged), refreshes all six affected outputs and reruns the complete focused selection.
The original 107 passes remain evidence of the earlier candidate, not final qualification.

Recovery review also traced request-scoped attachments through the actual MAF adapters:
their admission context is enabled only for Recoverable support, so request-scoped runs
can invoke a provider without durable segment/dispatch evidence. A released dispatch
lease or a terminal compatibility status cannot establish quiescence for them. Return
UnresolvedEffects before terminal replay for RequestScopedInput and cover active,
succeeded and cancelled records. The 109-case build passed but was not executed; retain
it as an intermediate build. The final selection is 112 cases (37 ordinary Unit).

The final 112-case candidate passes with six refreshed Release builds, twenty unchanged
owning/copied binaries, unchanged architecture cycles/findings, full portability and
zero new process/database/temporary residue. See the
[ordinary parent-stop qualification](../validation/2026-10-22-ordinary-parent-stop.md).
The earlier 107-case pass and two compile failures remain retained. This closes only the
Core protocol and module factory prerequisite; the actual Process adapter, stages,
bounded waits/fairness, cost regression investigation and activation remain open.

### Original Process execution stop adapter entry

Signed prerequisite 914c78922 qualifies the Core protocol. Add one narrow Application
port taking typed root, Process run and Agent execution identities, returning the
existing recovery pending enum (None means a verified closed execution only). The
Processes module owns the bridge to Core; Application remains independent of Agent
models and file storage. No new project reference or runtime partial is needed.

Before signalling, after lease wait and at publication, require the durable root barrier,
original Process claim/source receipt, selected run/root relationship, admitted owner
fingerprint and original database profile/fingerprint. Capture the current profile
generation once per call and reject changes during observation/publication; allow a
renewed current generation without rewriting the original session. Current dispatch
permission is deliberately not required for stopping an already-stopped parent. Missing
journals and unknown effects remain pending. Inherited planning uses its separate child
owner and cannot enter this adapter. No automatic worker or HTTP path is enabled here.

Use independently testable authority/store seams for wrong root/run/claim/profile,
revocation at each boundary and renewed-generation cases. A real composition/persistence
case must create a new background journal through the actual admission owner before
saving it, stop the original Process root through its runtime owner, then stop/replay
the selected Agent through the registered adapter. Reuse the existing isolated fixture
and source-authority seed helpers; do not fabricate admission for an existing run.
Confirm final exact class discovery before execution. Refresh affected Application,
Processes module, dependent composition/Web and tests in standard Release, inspect
dependency cycles, run static gates and reconcile resources. This is Behavioral proof;
bounded stage orchestration, elapsed waits and sustained capacity remain separate gates.

Final source selection before launch: 55 cases, comprising ProcessExecutionStopperTests
Unit 27 and Integration 6 plus 22 ProcessExecutionAuthorityPersistenceTests regressions
for the reused seed helper. The latter keeps its existing behavior by default; the new
opt-in admits a new unsaved Agent through the real registered source policy and journal
owner. Integration uses the native root cancellation operation and registered stop port,
including new-scope replay, renewed generation, active-root refusal, source corruption,
unknown provider dispatch and legacy missing journal. No host restart is claimed.

All 55 selected cases pass, with six affected Release builds, twenty verified runtime
outputs, full portability and zero new resource residue. The initial discovery/observer
helper defects were corrected before execution and remain retained. See the
[Process stop adapter qualification](../validation/2026-10-22-process-stop-adapter.md).
The entry and Behavioral closure gates pass for this adapter; actual recovery stages and
performance/activation remain open. During current-profile review, also flag the inherited
Workbench adapter's capture of a mutable profile object: bind identity/fingerprint values
there and add late-change regressions before activating the shared worker.
