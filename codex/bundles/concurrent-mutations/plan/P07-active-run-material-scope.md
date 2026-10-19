# Active run material scope ownership

Design before implementation, following root funding and shared business content.
Status: implemented in the runtime transaction boundary; the
[68-case repair checkpoint](../validation/2026-10-19-process-shared-run-scope.md) passes.
Executor activation still requires actual inherited child journal integration and the
application capture/reconciliation producer. The immutable material store alone is not an
active publication authority.

## Original admission

A RunSnapshot root declares one material scope before execution. Its immutable admission
binds a predetermined scope ID, real root run, selected database profile, workspace identity
and neutral business content contract. Initial root preparation includes a distinct hash
domain. Step admissions bind the exact scope through a new required wire schema, so old
readers cannot silently ignore parent enforcement. Existing content bytes retain their
protocol meaning; legacy material is not upgraded implicitly.

The scope declaration is part of root admission schema 2; funded step admission schema 5
requires the same scope. This reuses original policy provenance and initial command replay
instead of adding a second root admission channel. Prepared scope launches use their own
hash domain. Local inherited decision receipts use schema 3. Historical wire encodings
remain byte-identical when these required fields are absent.

The genuine root head begins awaiting capture, with no fabricated material generation.
The first capture uses a root budget reservation and commits its closed usage, canonical
material and active head atomically. The original capture time remains authoritative.
Root scope commands belong to the root runtime, never to a synthetic step. Scope identity,
content schema and initial admission remain immutable.

## Joining and publication

A step or descendant inherits a canonical material stamp rather than supplying another
payload. Under the root fence, its owner verifies the original admission, current active
scope, content identity, workspace/profile and root funding. A local preparation allocation
accounts for actual orchestration; inheritance performs no fresh business read and charges
no independent replan. Existing capture terminology must distinguish this operation in the
new protocol while preserving historical encodings.

Canonical membership binds the parent scope generation to the exact local decision
generation. Bound memberships across the root lifetime by the root's 256 allocations;
each join requires its own preparation allocation. Preserve the existing 32-generation and
64-child limits. Do not create an unbounded collection of nominally free memberships.

Every dispatch, effect and manager publication rechecks both the local decision and the
active parent head in the actual effect transaction. Use exact metadata joins, without
copying payloads into receipts or reading journals under SQL. A completed step can retain
an old material reference for history, but that reference cannot authorize new publication.

## Refresh sequence

1. The root transitions the scope to reconciling under its existing fence. New joins,
   enrollments and publication stop atomically.
2. Read the bounded membership set and real child-owner journals outside SQL. Close each
   exact child and funded allocation only from retained owner evidence. Unknown provider,
   journal or effect outcomes keep their grants reserved. Accepted effect receipts remain
   historical facts.
3. Reenter the root fence and compare the exact scope expectation and complete membership.
   Require no open children, no relevant live claims and no unknown root allocations.
   Root accounting is bounded to 256 allocations, so this completeness check is bounded;
   a truncated page or assignment count is not evidence of closure.
4. Settle the root capture/replan once, append one canonical material, and advance the
   parent head. Do not mutate arbitrary child runtime states from the parent store.
5. Finished steps remain historical. An unfinished decision moves through its own runtime
   owner to reconciliation without a live claim, then explicitly rebinds to the new parent
   material. It charges local preparation, with zero additional replans. Old-parent
   publication stays refused while it catches up.

Before implementing the final quiescence conditions, inspect actual claim release and
manager-review transitions. Do not invent a successful provider stop or refund an unknown
allocation merely to advance a material head. Direct root preparation reservations are
part of the same completeness check, including reservations with zero measured usage.

### Capture admission refinement

Freezing joins and publication is insufficient if generic reservations can continue during
reconciliation. Add an explicit capturing phase with one original pending capture allocation
and capture kind. Local reservations require an active parent. Root reservations while
awaiting capture or reconciling require the matching typed begin-capture command and exact
head expectation. Validate this before staging accounting, then apply the scope transition
in the same unit of work. Funding arithmetic remains independent of providers and journals.

The same fence also rejects acquiring or reopening a dispatch claim while the scope is
not active. A claim can otherwise appear after the completeness check while source work is
outside SQL. Existing claims must be released through their real runtime owner; expiration
alone is not closure. Claim comparison uses sets, so it is linear in the runtime state.

Begin-capture verifies complete quiescence and absence of unknown root allocations,
reserves one grant and records its exact identity before source work outside SQL. Completion
requires that allocation's closed usage and original material stamp, then atomically
activates the next material. An unrelated generic command cannot settle the pending capture
allocation. Unknown outcome leaves capturing/reserved state intact for exact recovery.

Explicit abort needs closed owner evidence, retains consumed work and the retired allocation
count, and returns to the original awaiting-capture/reconciling state. Successful automatic
refresh consumes one replan. An explicit abort preserves the actual reported work/replans,
including overrun; it cannot relabel measured work as zero. An overrun cannot activate new
material and remains visible after abort. The finite work and allocation limits prevent
unbounded retries. Test new-reservation/quiescence and lost-capture-acknowledgement races.

## Boundaries and failure recovery

Runtime owns immutable scope admissions, commands and pure transitions. Persistence owns
the active head, membership and exact transaction checks. Application coordinates capture
and owner reconciliation. Workbench owns business content, current permissions and source
comparison. Agent and Workflow retain their original child identity and closed usage;
their provider work and journals remain outside the Process SQL transaction.

Preserve source/Project-before-root lock order. A crash after enrollment recovers the exact
child ID before preparing new input. A crash after material commit returns the original
receipt. No current-source fallback may repair missing original material or owner evidence.
Downgrade must refuse new scope admissions even when only prepared and not yet captured.

Persistence retains one active head and indexed membership metadata in addition to the
existing canonical material rows. Membership joins read metadata, without copying or
rehashing the business payload inside the root lock. A root refresh reads at most 257
membership/allocation rows to detect overflow, checks actual child counts and claims, and
requires the complete set to agree with retained counters. Publication uses indexed joins
to the active head and original membership. No provider or journal callbacks execute in
that transaction. This establishes bounded work, not a measured throughput qualification.

Inherited Agent material requires a new journal/decision origin with its original capture
time. Keep schema-6 interactive chat and approval checks intact. Inherited children cannot
autonomously refresh their parent's scope; they must explicitly return control for parent
reconciliation. A parent successor starts a new stable child occurrence, preserving the
old journal. Inspect each existing schema comparison before extending it; an open-ended
version comparison is not a compatibility strategy.

## Validation and capacity

Verify immutable preparation and old-reader refusal; first-capture/accounting rollback;
mixed Agent/Workflow and nested consumers of one original material; absence of payload
duplication and recapture; complete bounded membership; join/quiesce and effect/refresh
races; unknown outcome refusal; historical accepted receipts; one root replan charge;
independent-root progress; exact query/index bounds; restart and migration safety.

Per-record bounds do not establish workload capacity. Measure material size, retained
generations, child journal amplification, root-lock duration and unrelated-root throughput
before claiming capacity or latency qualification. Runtime activation additionally requires
actual inherited Agent/Workflow input and effect paths, rather than a declared contract alone.

Atomic funding does not by itself provide fair scheduling: a first child reserving the whole
available ceiling can delay siblings. Before activating parallel producers, define bounded
grant sizing using declared child ceilings and finite concurrency. Explain aggregate work
time separately from wall-clock duration in settings. An atomic reservation refused before
enrollment may be retried; an admitted grant or an unknown allocation cannot be replaced or
topped up silently. Measure same-root contention separately from independent-root progress.
