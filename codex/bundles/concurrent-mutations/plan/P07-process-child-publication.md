# Process child identity, lookup and publication fence

This is the next dependency of [generation storage](P07-process-generation-persistence.md).
Its [108-case validation](../validation/2026-10-19-process-child-publication.md) now passes.
It does not activate a new executor. The existing unsupported-policy guards stay closed
until the actual producer, inherited input, review authority and child journal use the
same binding. A decision fence is one condition of publication, not source permission.

## Evidence and decision

Agent background reservation currently calls `FileSandboxWorkspaceExecutionSliceStore.ListRunsAsync`,
which enumerates every run directory and opens every `run.json`, while holding workspace
coordination. It then filters by Process lineage. Reusing that path for every generation
would make dispatch cost grow with unrelated retained history. The source scan remains a
known limitation of the legacy path; this slice does not claim to fix it.

For governed execution, the Process ledger will issue and retain the child identity
before the child owner does work. A deterministic, versioned occurrence identity combines
the original decision ID, generation, child owner and bounded logical ordinal. Claim
rotation, transport attempts and mutable input do not participate. A retry resolves the
same enrolled child and original allocation; a successor generation has a different child.
The producer must retain its logical ordinal. The existing assignment executor uses
ordinal 1; future parallel/nested producers must admit their occurrence mapping before
dispatch and cannot infer it from today's child count or completion order.

The child's ID becomes the exact downstream execution ID. A new Agent/Workflow adapter
will look up that exact ID after verifying the canonical enrollment and original profile.
It must reject changed binding/input, never silently allocate another ID. This avoids a
second durable source index and its cross-store migration/recovery protocol. The generic
legacy Agent entry point cannot accept a new metadata flag as proof of this authority.

CodeAnalytics Agent baseline `snap-20261019191241-a71eac0e`: Core, Models, Persistence;
1,803 types, 13,598 members, 537 findings, 189 existing load diagnostics. The three scoped
projects have no project cycle; four existing type cycles and existing large storage
classes remain. Actual builds are required because the snapshot reports load blockers.
The preceding Process baseline is `snap-20261019185941-d1ce2c09` (four projects, no cycles).

## Ownership and minimal change

| Responsibility | Owner | Constraint |
| --- | --- | --- |
| Versioned occurrence identity and exact child address | Process Runtime | Pure values; no storage, clock, Agent or Workflow references. |
| Canonical child plus original allocation read | Process Persistence | One selected SQL query; include historical generations; no event-history enumeration. |
| Publication fence contract | Process Application | Explicit partial condition; source/manager permission and runtime contract selection remain mandatory. |
| Enlisted publication check | Process Persistence | Existing effect transaction and same root advisory lock as runtime commits; no new transaction or outer lease acquisition. |

Extract the existing generation/child row conversion into a cohesive internal mapper
shared by the mutation store and selected query. Do not add runtime partials, provider
dependencies, a registry or a general mutation-hook framework. No schema change is needed:
the current head/generation/child/allocation keys already address the original binding.

The fence compares the complete original address, owner, material, budget, allocation
and grant. It rereads current head, child closure, allocation settlement/overrun and actual
run/step state inside the root fence. Sibling enrollment changes head revision without
revoking an already enrolled child. Supersession, reconciliation, cancellation, terminal
steps and settlement revoke publication. Manager review requires the actual waiting
state and a released dispatch claim; it must also pass a future explicit manager action
authority check. A snapshot returned before locking never authorizes a later effect.

Require the caller's PostgreSQL ReadCommitted transaction. Reject a missing transaction,
different database and snapshot isolation. Otherwise a snapshot established before waiting
for the root lock could observe an old generation. The lock remains held until the actual
effect transaction commits or rolls back. No provider call, journal read or user wait may
occur inside this boundary.

## Acceptance and validation

Pure proof covers stable identity, generation/owner/ordinal separation, fixed cross-host
test vectors, invalid/bounded inputs and complete admission binding. PostgreSQL proof uses
the real runtime unit of work to start/enroll/transition decisions; it checks exact lookup,
restart, changed/foreign bindings, closure, overrun, runtime cancellation, review phase,
missing transaction, isolation and both publication/quiescence orderings. An unrelated root
must progress while the selected root is blocked. SQL observation must show one selected
read and no run/event-history scan. No provider is invoked in these storage tests.

Build changed Runtime, Application and Persistence plus owning tests with standard Release
outputs. Confirm exact discovery before execution; run complete portability-static,
rendering and documentation gates and compare the scoped Process dependency graph.
The subsequent activation must additionally prove actual Agent/Workflow producer retries,
provider input and effect commit, the capability/version handshake, original policy/budget
inheritance, authorized manager review and browser recovery after host restart.
