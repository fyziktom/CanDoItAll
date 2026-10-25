# Process result and original child accounting

Status: entry design accepted after signed owner-outcome checkpoint bd1bc1991.
This design does not activate the Process producer or close R5/R6.

## Responsibility and dependency decision

The existing dispatcher and interrupted-execution coordinator submit a bare runtime
result. The existing runtime engine normalizes artifacts/recovery, releases the claim
and appends result/events. The existing Process unit of work can also settle a child
and budget, but no caller joins those responsibilities.

| Responsibility | Owner after change | Boundary |
| --- | --- | --- |
| Bounded immutable input | Runtime result snapshot helper | Pure validation/copy, no provider or storage |
| Artifact/recovery normalization | Existing Runtime engine | Expose existing pure preparation; no decision-owner dependencies |
| Original child selection | Application query port, Persistence implementation | Actual execution ID, original generation/material/allocation |
| Journal interpretation | Existing registered outcome reader | Workbench implements the task-plan contract |
| Result/accounting orchestration | Application result coordinator | Compose one request; independent unit seam |
| Atomic acceptance | Existing Persistence unit of work and focused guard | One existing root transaction, no new lock |
| Production wiring | Processes composition and its two callers | Required service; no optional bypass |

No project reference is needed. Runtime already references Driver Abstractions;
Application references Runtime; Persistence implements Application ports. Workbench
remains outside the coordinator and Persistence. Neither Drivers nor Agent Core gain a
dependency on Process Runtime. The contract-keyed reader is the existing extension seam.
Do not add provider cases to the dispatcher or a new runtime partial.

Application adds the repository-versioned Logging.Abstractions package for bounded
original-child read failure diagnostics. It does not acquire a host, provider, storage
or logging implementation dependency.

Use the existing command/receipt and explicit enum state model. A two-commit sequence
was rejected because a crash could expose a finished step with live child accounting.
A generic transaction coordinator or new orchestration framework is unnecessary.
An outcome-specific Application service and indexed query port are justified by this
real ownership boundary and their isolated tests. No interface is needed around the
single Application coordinator itself.

Move bounded envelope validation/copy from the existing result partial into a cohesive
top-level Runtime helper. Preserve existing invalid-receipt/invalid-host diagnostics.
Expose the existing pure result preparation. This removes validation responsibilities
from the partial instead of growing it with provider-specific result policy.

## Original identity and bounded reads

Snapshot result collections before the first await. Validate counts before copying,
then validate the captured bounded arrays and all elements, including host capabilities.
Immutable scalar records can be shared. The command result key must equal the envelope
key. Unsupported/oversized input fails before storage or identity derivation.
Host profile/capability identifiers retain their existing constructor bounds and syntax;
snapshot validation also rejects default or malformed identifiers.

An indexed run/step read identifies the durable local policy budget, optional head and
capture state. No budget means legacy behavior; a present current policy with a missing
budget is corruption. A retained budget remains authoritative after assignment repair.
No current policy may replace the original accepted policy.

For a supplied actual execution ID, resolve its child through the unique
(DecisionId, ChildId) index, across original generations. Read the original accepted
assignment projection and material metadata, never material payload. Use the existing
child query's already selected balance rather than another budget read. A noncurrent
generation is readable for replay/accounting but cannot lend authority to a new result.
Retries pin the original identity and refresh only revisions/closure observations.
Compare the actual step head reference for this purpose. The existing child observation's
IsCurrent flag also includes shared-scope dispatch permission, which is intentionally
false during root reconciliation. That flag must not prevent closure of already incurred
work. A root reconciliation/capture can require a manager wait while exact child accounting
still proceeds; it never grants fresh dispatch authority.

Read pending local allocations by OwnerRevision absence, including zero-tick grants.
Identify the original shared capture specifically; another step's open child allocation
in the same root is not a reason to block this step. Queries stay bounded and indexed.
Legacy result acceptance adds two indexed governance reads: selection before submission
and an authoritative acceptance check inside the transaction. The latter also protects
direct runtime callers. Selected results read the binding in four bounded metadata
queries; the existing outcome reader separately validates its original source.
Record actual selected query counts and latency before product activation.

## Replay and result normalization

A versioned deterministic command identity includes run, step, original child/material/
allocation binding, claim/owner, result key and every field of the captured input result.
ResultHash alone is insufficient. Do not include newly generated command IDs, retry
timestamps or other mutable context. Pin identity bytes/digest in golden tests.

Look for this command's durable receipt before opening the owner journal. Exact replay
must survive revoked access or missing original files. Check the receipt's original
decision, material, child, allocation and result identity; the returned runtime state
may legitimately have progressed. A strategy-result Duplicate without this compound
receipt is a refusal, not permission to skip accounting. Changed result metadata under
the same result key cannot reuse the old outcome.

Read owner evidence before acquiring any SQL transaction, root/Project gate or catalog
lease. File recovery refusal becomes an explicit reconciliation result with a bounded
diagnostic and logging of typed identity/failure kind. Corrupt original evidence
(InvalidDataException) propagates. No missing/unknown receipt becomes a no-effect closure.

| Evidence and normalized runtime result | Atomic decision |
| --- | --- |
| Known closed child; completed/failed step; no other unsettled work | Exact settlement plus Completed generation |
| Known closed child; cancelled step; all work closed | Exact settlement plus Cancelled generation |
| Known child but required artifacts missing or manager result | Exact settlement plus WaitingForReview |
| Known child but another allocation/child/capture remains open | Exact settlement plus Reconciling; explicit manager wait |
| Approval or changed-basis wait | WaitingForReview, reservation retained |
| Open work, unknown effects, unstable/missing journal | Reconciling, reservation retained; suppress unverified outputs |
| Runtime recovery would return Ready | Explicit review; no implicit replacement policy child |
| Missing child identity with existing governed work | Pause under the exact current runtime claim, never settle an inferred child |
| No head/capture/allocation and definite preflight failure/cancellation | Terminal refusal allowed; no fabricated execution |
| No admitted execution but claimed success | Explicit manager wait |

A successful strategy result requires a successful closed owner outcome. Contradictory
success goes to review, retaining known effects/work. Ordinary cancellation still stops
dispatch first; later reconciliation can account for already incurred work.
Missing child identity may pause only after the runtime validates the exact active
claim. Pin the observed head for that pause; do not follow a replacement generation
on retry. A missing journal after enrollment cannot prove that a provider never ran.
Producer preflight retirement will need explicit durable no-effect evidence; absence
alone keeps the allocation reserved.

## Closure transition contract

Extend the existing compound closure with explicit review/reconciliation dispositions.
Terminal Complete/Cancel may start from Active, WaitingForReview or Reconciling only
when exact owner settlement and the complete bounded child set prove no child remains
open. This broadens the prior staged-only terminal rule deliberately; it does not turn
an unknown effect into cancellation. Plain phase cancellation still requires Reconciling.
Nonterminal reconciliation cannot regress to review.

If another reconciler already closed the exact child, the same closure/settlement can
join an explicit parent phase change without charging work again. That changes only the
generation revision. Same phase and exact closure is unchanged; conflicting closure,
terminal phase or owner revision is refused. MaximumChildren remains 64 and the complete
child set/count is checked for compound phase transitions.

Preserve canonical v1-v4 receipts byte for byte. Only new review/reconciliation closure
values use v5; cross-version values, extra members and incompatible resulting phases
must be rejected. Existing persisted records need no migration.

## Persistence acceptance and concurrency

Inside the existing fenced transaction, stage budget/decision/runtime/events/outbox,
SaveChanges, reload the persisted runtime state, and validate each newly added
strategy-result receipt before commit. Reuse the existing post-save state reload;
validation must not trust only the proposed in-memory state.
For a policy-owned terminal result require the matching actual closed child/generation
and no unsettled local allocations or original shared capture. A definite preflight
failure/cancellation with no generation or unsettled work is the bounded exception.
Pending results must leave an explicit non-dispatchable runtime state and valid review/
reconciliation phase when a generation exists.

The guard is scoped to new strategy results. It must not prevent ordinary cancellation,
subsequent accounting, or an unrelated shared-root child from making progress.
The transaction rolls back state, claims, results, accounting, events and outbox together
on any refusal. Existing Project-before-root lock order remains unchanged.

The first PostgreSQL candidate exposed legacy diagnostics serialization losing an
execution identity when diagnostics and host evidence were both empty. Use the existing
version-one object envelope for exactly that case. Keep existing legacy array bytes
otherwise, including receipts without an execution identity. No migration, new format,
inferred host fact or safety attestation is needed. Independent mapper tests and
post-flush identity-loss injection must prove round-trip integrity and transaction rollback.
Optional decision-head/generation/budget joins explicitly retain nullable keys and
guard correlated predicates when an owner row is absent. Legacy runs without accounting
must remain readable and committable through the same query on PostgreSQL and InMemory.
Do not bypass the guard for a particular provider or add an early database probe.

Distinguish stale budget revision from mismatched budget identity with a typed optimistic
refusal. Retry at most three times, reloading only the original selection. No provider,
publication or snapshot capture is retried by result submission. Unknown commit
acknowledgement recovers through the stable original command receipt.

## Proof and architecture gate

Before snapshot: snap-20261020194602-5874c340, four projects, 1094 types,
9587 members, 350 findings, 190 existing diagnostics, six scoped project edges and
zero scoped cycles. Dashboard and exact SubmitStrategyResult/caller symbols were
inspected; the pure method excerpt was truncated and its local file was read.
Existing tool diagnostics are not a healthy-build claim.

Pure tests cover immutable input, bounded refusal, full identity/golden digest,
normalization, pending vs terminal decisions, exact replay, changed metadata,
already-settled work and no provider/storage side effects during pure preparation.
Transition/codec tests cover all open phases, siblings, monotonic review/reconciliation,
closed-child handoff, terminal conflicts, v1-v4 compatibility and strict v5 refusal.

Real PostgreSQL tests cover original lookup after repair/supersession, no payload read,
atomic success/failure/cancellation/review, overrun, zero-tick outstanding work,
shared capture vs unrelated root children, direct bypass, rollback after flush,
competing final results, restart/lost acknowledgement and cancellation-before-accounting.
Registered integration uses the actual saved-tool/provider fixture and the production
dispatcher/recovery route. No new Docker stack or fixture family is justified.

Build Runtime, Application, Persistence and Processes module directly, then affected
Unit/Integration assemblies with standard Release/local dependencies. Declare exact
case filters/counts before execution. Repeat affected runtime/dispatcher/recovery,
completion/budget and original reader cases; do not claim an unfiltered Stable pass.
Require full portability scan/enforcement without baseline writes, architecture diff,
source/runtime identity, secret-reviewed proof and owned resource reconciliation.

Progression requires production callers using the new coordinator, independent tests
for the new Application behavior and unchanged project direction. Durable capture,
producer/manager activation, Workflow, settings/SPA and sustained capacity remain open.
Manager continuation after a persisted wait needs its own authorized runtime transition:
the ordinary result path still requires its exact active claim. It cannot reuse an old
result key with changed outcome metadata, revive the old claim, or borrow a replacement
child. Keep this producer-activation dependency explicit.
