# Terminal decision finalization design

Entry gate: Pass, Behavioral tier. P14 qualifies all 60 selected cases with unchanged source/runtime and zero new resource residue. P13 signing remains pending independently; preserve its staged index. This work is confined to Process-owned files and a new plan.

## Problem and chosen boundary

The root guard correctly refuses an active decision even if it has no children. Generic
BeginReconciliation requires a waiting/blocked runtime step; changing a Cancelled step
back to Waiting would corrupt the business outcome. Existing final-child closure works
when a last child is available, but cannot represent an empty generation. A durable root
worker must close both empty and previously fully accounted generations without inventing
a child, an allocation, a measurement, or an accepted effect.

Add an explicit ProcessDecisionFinalization command to the existing runtime transaction.
It carries the original step, exact decision expectation, exact local budget expectation
and observed terminal step status. Runtime owns a pure terminal-generation transition;
Persistence verifies retained children, budget/work and the persisted terminal step;
Application constructs and replays the exact command using an explicit original action
identity and actor. The future worker composes this application owner. No Agent,
Workbench, Workflow, provider, timer or queue dependency enters Persistence.

Rejected alternatives: reopening the business step; relaxing root quiescence; fabricating
a zero-work child; silently completing generic reconciling decisions; adding recovery
methods to the already broad Agent recovery coordinator. A separate concrete application
owner needs no new interface until a real substitution boundary requires one.

## State and concurrency invariants

- Original and proposed business statuses are unchanged. The persisted selected step
  must match the requested terminal status. Completed, Failed and Skipped map to a
  Completed accounting phase; Cancelled maps to Cancelled. Completed accounting does not
  assert business success. Active/waiting/blocked steps cannot use this operation.
- No live claim or active step binding may exist. Check persisted rows under the existing
  root fence, as well as proposed state. Do not trust only the caller's OriginalState;
  it could carry the correct version with a false terminal status.
- The generation must be the current exact expectation and Active, WaitingForReview or
  Reconciling. Completed, Cancelled and Superseded refuse new finalization; original
  receipt replay remains legal before current-state checks.
- Current retained child cardinality and closures must agree with generation counts.
  Read at most MaximumChildren + 1 (65), require zero unclosed children, and reject
  missing/excess/corrupt rows. No paged inference that an unseen child is closed.
- Budget ID, root, run, step and revision must match. Reserved work AND replan counts
  must be zero. Check for every allocation lacking OwnerWorkRevision, including a
  zero-work replan grant. Do not turn unknown work into zero or mutate budget amounts.
- Use one exact persisted guard projection for step/claims/budget/open allocations,
  not payload hydration or an allocation list. One indexed bounded child read remains.
  No file/provider/Agent work or wait occurs while holding the SQL transaction.
- The command must carry no settlement, material payload, inheritance or shared-scope
  transition. Shared root scopes are finalized separately after all local decisions;
  an already stopped shared scope cannot prevent valid local accounting closure.
- Timestamp/version advances only to commit the decision receipt and generation phase.
  No runtime business event, outbox message or artifact ledger entry is invented.

## Replay and compatibility

The application request includes an original action ID. Deterministically derive its
runtime command ID from a versioned domain, root/run, complete request and explicit actor.
Retain this exact request before a worker attempts SQL. On an ambiguous acknowledgement,
retry the same request/actor before reading or adopting a newer decision/budget expectation.
Validate retained receipt command, scope, terminal result and absence of accounting or
effect payloads; return Duplicate without another phase, charge or event. A new action
with stale expectations must not borrow another action's receipt.

Extend the existing strict decision receipt codec with a new appended command kind and
version 6. Two optional document fields (budget expectation, terminal status) are omitted
for all older versions; versions 1–5 must round-trip byte-for-byte. Version 6 requires
exactly the new shape. Reject downgraded/mixed commands and extra fields. No schema
migration, table, project reference or new public service interface is required; the
new application concrete service has the existing scope/DI lifetime. This adds a supported
receipt version and therefore requires compatibility proof and later integrated Stable.

## Minimal edits and proof plan

Runtime: new typed command/validation/Expected case and pure terminal transition.
Persistence: small dedicated guard for the persisted terminal/budget facts; integrate
one switch case in the decision store and append strict codec support. Keep SQL root
locking, CAS, rollback and idempotency with the existing unit of work.
Application: request validation plus a small concrete finalization coordinator; wire
through the existing Process module. Reuse result selection without child hydration.

Unit proof: all three allowed generation phases and four terminal statuses; no open child;
stale expectation; terminal/superseded refusal; invalid command/budget/status; strict new
receipt round-trip and older-version bytes; malformed version/shape refusal; application
identity/actor/replay/rejection paths using existing small test seams where practical.

Real PostgreSQL proof: empty and already-closed generations preserve state, work and
effects; cancelled root can then finalize; completed/failed/skipped step remains exact;
stale decision and budget, open child, unclosed zero-work allocation, inconsistent child
counts, live claim and forged original terminal status all refuse with zero mutation;
rollback before/after flush; two independent owners; lost acknowledgement and restart
replay; shared-scope compatibility. Retain exact command/result identity in proof.

Inspect existing test helpers before selecting classes. Derive rebuilt discovery counts,
refresh affected standard Release outputs serially, run narrow relevant regression lanes,
full portability-static, scoped architecture review, complete source/runtime identity and
resource observation. Reuse the isolated PostgreSQL fixture. No new Docker stack or host.
Measure SQL shape/row bounds for the new projection; historical allocation-population
capacity still needs the separate root-worker/index qualification.

## Remaining recovery design

This command is a prerequisite, not a durable root worker. Queue creation with the barrier,
database-clock leases, fair bounded stage/keyset scans, exact retained accounting intents,
legacy backfill and historical-volume indexes remain separate reviewed work. Unknown
Agent work/effects remain pending. Original-generation snapshot refresh is distinct from
root cancellation and cannot reuse its irreversible business barrier. SPA stays separate.

## Entry review and exact validation ownership

The persisted guard also checks that each retained closed child in the selected generation
has its exact settled allocation and matching owner revision. Missing/mismatched allocation
evidence is corruption, not permission to finalize. Existing root fences and CAS serialize
the exact decision/budget checks; no lock is held across external callbacks.

Architecture baselines: Persistence snap-20261022055650-4d5e01f9; Runtime
snap-20261022055700-b3c18571; Application snap-20261022055706-d27bf2c1. Ten existing
Persistence package-advisory diagnostics are recorded separately; Runtime/Application have
no diagnostics. Final scoped comparisons must retain no new project/type cycles.

Production refresh: Runtime, Persistence, Application, Process Module and Web, then the
Unit and Integration output assemblies, all standard Release and serialized. Select exact
classes below with FullyQualifiedName filters. New finalization classes must declare their
case matrix before execution and rebuilt discovery must match it; mismatched discovery
blocks execution. Source changes to commands, codec, persistence guard, coordinator, DI or
selected fixtures invalidate dependent qualification. Broad Stable belongs to the later
frozen integrated candidate, not every incremental receipt edit.

| Existing regression class | Source-declared cases |
| --- | ---: |
| Unit-ProcessDecisionGenerationTests | 29 |
| Unit-ProcessDecisionCompletionTests | 33 |
| Unit-ProcessDecisionTerminalAccountingTests | 57 |
| Integration-ProcessDecisionPersistenceTests | 31 |
| Integration-ProcessDecisionCompletionPersistenceTests | 25 |
| Integration-ProcessRootQuiescenceTests | 19 |
| Integration-ProcessRootWorkAdmissionTests | 14 |
| Integration-ProcessDecisionTerminalAccountingIntegrationTests | 10 |

## Frozen-candidate selection

The implemented candidate declares 81 new cases: 38 pure transition/codec cases, 20
application-owner cases and 23 real PostgreSQL cases. Together with the 218 source-
declared regressions above, the required rebuilt discovery and execution count is 299.
The new exact classes are Unit.ProcessDecisionFinalizationTests,
Unit.ProcessDecisionFinalizationCoordinatorTests and
Integration.Processes.ProcessDecisionFinalizationIntegrationTests. The manifest pins
their full namespaces, individual method names, source hashes and all resource owners.
The unit of work additionally rejects combined settlement/shared-scope/effect payloads
and business run status changes for this command. There is one new scoped concrete
Process service registration; no public interface, project reference or schema change.

## Codec repair before qualification

The first frozen candidate built all seven projects and passed static enforcement, but
the first unit lane passed 34 and failed four version-6 round trips. Execution stopped
before the other 261 cases. Its source/runtime identities and one-command observation
were verified, with zero new resource delta. The failure is production serialization:
read-only budget/step identifier wrappers deserialize through their default struct
construction when embedded directly in the wire document. Version 6 now uses a private
primitive Guid/revision budget document and explicitly reconstructs typed identifiers,
matching the existing budget receipt codec. Versions 1–5 remain unchanged. Repeat all
299 selected cases on a newly frozen candidate; do not reuse the 34 passes as closure.

## Reopened root-result ordering, 2026-10-22

The codec-fixed candidate passed 79 of 81 executed cases. Two test-only corrections
reload the shared-scope revision after membership changes and identify the bounded
child query by its 65-row parameter. An exact source/runtime bridge proves those two
methods were the only changes. The continuation passed 175 of 177 executed cases and
stopped before 43 regressions. Its two failures are a production interaction with P13:
an existing cancelled strategy result atomically closes its child, budget and decision
and makes a quiescent root Cancelled, but the root guard inspects work before that flush.
The unchanged existing assertions exposed the regression; do not weaken them.

Refine the guard's transaction ordering for this one typed outcome. An original root
that is not already Cancelled may defer its final quiescence check only when the request
contains a new cancelled strategy result, its matching child closure with Cancel
completion, and its local budget settlement. Existing admission and monotonic barrier
checks remain before staging. The guard returns an explicit post-flush obligation to
the unit of work. After persisted result acceptance, execute the independent full-root
pending query against the same transaction, plus the reloaded root claims/steps. If any
work remains, throw before commit so the complete result/settlement/receipt rolls back.
No root or Project lock is released between staging and this check; no external effects
occur in that interval. Existing non-result cancellation still refuses pending work
before writing, and legacy Cancelled roots can continue exact terminal accounting.

This fixes ordinary atomic cancelled-result acceptance; it does not relax quiescence,
permit new work or authorize cancelling an active child without accounting. Returning a
normal rejection after a flush would leave staged writes in an enlisted caller's
transaction, so the late failure deliberately uses the established exception/rollback
contract. Validate independent remaining descendants, budgets, decisions and shared
scope work; rollback and lost acknowledgement; original duplicate replay; and unchanged
early-refusal behavior. Add no schema, public interface, project reference or new owner.

P13's blanket restriction against combining the final settlement and root cancellation
is superseded only for this validated atomic result. Its cancellation guard integration
is reopened until the repaired full selection passes. Because production changes,
none of the prior candidate passes qualify the new candidate; rebuild affected outputs
and repeat all 299 existing cases plus the added post-flush regression cases.

The repaired selection is 312 cases: the earlier 299 plus 13 regressions in
ProcessDecisionCompletionPersistenceTests (now 38). The additions cover six independent
pending-owner rollback cases, four requests that must not defer the check, two cancelled
flush-failure cases and one cancelled lost-acknowledgement replay. Run that 38-case lane
first, then all remaining lanes. Refresh Persistence, Process Module, Web, Unit and
Integration serially in Release; the unchanged Runtime/Application assemblies remain
included in the exact runtime manifest. No earlier successful test result is reused.

## Qualification

Behavioral and scoped C# architecture gates: Pass. The final candidate discovers and
passes all 312 cases, including 94 new cases, without prior pass reuse. The additional
13 cases close the post-flush root-result ordering regression while the original six
atomic result assertions retain their required business outcomes. Persistence, Process
Module, Web, Unit and Integration Release builds pass; full portability enforcement
passes with all 15665 reviewed findings unchanged. All eleven final test commands are
observed, and all five candidates reconcile with zero new resource residue.

Persistence retains 184 types, 1400 members, 210 dependency edges, 32 entities, zero
cycles and 62 findings. Ten pre-existing package-advisory diagnostics remain. Runtime
and Application retain their already-reviewed P15 snapshots and no cycles. The root
guard owns the post-flush obligation; the unit of work only carries and discharges it.
No project reference, schema, public interface or partial was introduced. The private
budget wire DTO is serialization within the existing receipt owner.

See [validation](../validation/2026-10-22-terminal-decision-finalization.md) for all five
attempts, precise inputs, failure evidence and proof. This requalifies the affected P13
guard behavior; automatic recovery and integrated activation remain downstream work.
