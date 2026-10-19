# Atomic Process budget persistence

Design before implementation, following the [runtime protocol](P07-process-decision-runtime.md)
and [shared arithmetic boundary](P07-shared-decision-budget-boundary.md). This increment
implements durable accounting, not governed execution or permission to publish.
Implementation and its [41-case validation](../validation/2026-10-19-process-budget-persistence.md)
now cover this local persistence increment.

## Boundary and transaction

Process Persistence owns one normalized balance per policy-bearing logical step and
one indexed allocation row per retained allocation identity. Runtime owns the typed
reservation/settlement command and exact receipt; SharedKernel owns the arithmetic.
No project reference changes, Agent/Workflow dependencies, business material, clock
measurement, provider calls or external journal reads belong in this store.

Create the balance with the immutable assignment in the existing runtime unit of work.
Its owner-issued budget identity and original effective ceiling survive claim rotation,
rework and restart. A unique run/step key prevents creation of a replacement balance.
Legacy assignments create no balance; existing admitted runs are not backfilled from
today's settings. Missing historical accounting fails explicitly. This is a local step
lineage ceiling; enclosing allocations still require the later child enrollment path.

Add one explicit optional accounting command to a runtime commit, not a general hook
framework. A cohesive persistence collaborator stages the head/allocation mutation in
the same DbContext and transaction as state, events, outbox and command receipt. The
existing root SQL fence and runtime state CAS remain in force. The collaborator owns
no transaction and calls no SaveChanges. A failed state transition or SQL write leaves
both accounting and runtime state unchanged. New reservations require a nonterminal
run/step; closed evidence may settle after cancellation so actual work is not lost.

The command carries the exact step, budget ID, balance revision and allocation ID, plus
a typed grant or closed-owner settlement. Recompute the transition from the locked
stored values. Never accept a caller-supplied replacement balance. Settlement values
remain internal contracts: the future Agent/Workflow adapter must verify actual closed
owner evidence before submitting them. There is no new HTTP mutation endpoint.

## Replay and bounded cost

Retain the exact command and resulting balance/allocation in the existing runtime
idempotency receipt, with a versioned bounded codec. A retry validates its original
accounting parameters and returns that original accounting result even if the runtime
state has advanced. The runtime state remains a separate current observation. Reusing
the command ID while adding, removing or changing accounting is refused. The ordinary
legacy receipt keeps its existing behavior.

The accounting hot path reads one head and at most one exact allocation; it never loads
all children. The shared 256-allocation cap includes settled identities. Generation
changes must retain this same ledger. Separate normalized generation/child history,
root-wide capacity, enclosing budgets, material references and execution-contract
activation remain in the subsequent runtime increment. No capacity claim is inferred
from these storage bounds.

The additive migration creates the two indexed tables and nullable receipt column.
Do not rewrite existing runtime JSON or backfill budgets. Downgrade must refuse to drop
retained accounting. Schema application requires the existing migration window; it
is not zero-contention merely because the new tables are empty.

## Proof and architecture gate

Before snapshot: `snap-20261019172048-d1ce2c09`, scoped to Process Runtime, Application,
Persistence and module. Existing workspace diagnostics require actual builds as well.
The unit of work delegates accounting rather than acquiring another responsibility;
no new partial, service locator, generic coordinator or interface is needed for this
persistence-local collaborator. A query port will be introduced only with its actual
application consumer.

Prove PostgreSQL restart/replay, changed-command refusal, stale/foreign identity,
competing reservations, settlement/overrun, rollback after staged accounting, and
independent-root progress. Exercise real initial assignment and runtime commit paths;
do not seed the accounting tables as proof of producer wiring. Confirm legacy admission
and command replay remain unchanged. Inspect the generated migration/model and verify
bounded indexed accounting reads. Run affected Release builds, confirmed discovery,
focused tests and full portability-static enforcement before the signed checkpoint.
