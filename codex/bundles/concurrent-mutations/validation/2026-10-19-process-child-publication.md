# Process child identity, canonical lookup and publication fence

Status: implemented foundation; **108/108 selected cases pass**. The
[design](../plan/P07-process-child-publication.md) preserves Process/Agent/Workflow
boundaries and leaves actual governed executor activation closed.

## Behavior and limits

The Process runtime derives a stable versioned child ID from decision, generation,
owner and admitted ordinal. Retry/claim rotation does not change that identity; a
successor generation does. An exact child admission retains scope, material, owner,
budget, allocation and grant. The registered persistence reader uses one selected SQL
query and returns historical closure separately from current generation status.

The registered publication condition joins the actual effect transaction, requires
PostgreSQL ReadCommitted, takes the existing root advisory lock and rereads the exact
child. Supersession, reconciliation, closure, sibling overrun, stopped runtime and
invalid review/claim state prevent publication. The outer transaction retains the
lock through commit/rollback. Original source/manager authorization, child execution
contract, inherited provider input and the actual effect caller must still compose
this condition before Process/Workflow policy settings can be activated.

Five existing row-conversion methods moved into one internal persistence mapper; the
mutation and query owners share validation. No project reference, schema migration,
runtime partial, provider dependency or shadow source index was introduced. The old
Agent reservation path still scans run history. The new governed producer must use
the canonical child ID for direct downstream lookup; that producer is not yet wired.

## Executed proof

Candidate: `.artifacts/concurrent-mutations/validation/c4-process-child-publication`.
Standard Release builds passed for Runtime, Application, Persistence, Process module,
Web, Integration and Unit. The first Integration build caught a test calling an
internal implementation member; the test was corrected to exercise the public reader
through its factory. Initial README whitespace failed diff-check and was repaired.
Both initial failures remain in the evidence. All execution used the final frozen
source and refreshed outputs, with no repair bridge or failed execution lane.

| Exact selector suffix | Expected/discovered/passed | Purpose |
| --- | --- | --- |
| `CanDoItAll.Tests.Unit.ProcessDecisionChildIdentityTests` | 23/23/23 | Fixed cross-host vectors, culture independence, generation/owner/ordinal separation and invalid bindings. |
| `CanDoItAll.Tests.Integration.Processes.ProcessDecisionPublicationFenceTests` | 33/33/33 | Registered composition, actual host restart, selected SQL read, changed bindings, sibling/claim progress, review, closure, overrun, isolation and both lock orderings. |
| `CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceTests` | 31/31/31 | Generation owner and extracted real-UOW fixture regressions. |
| `CanDoItAll.Tests.Integration.Processes.ProcessDecisionBudgetPersistenceTests` | 21/21/21 | Shared allocation mapping and durable accounting regressions. |

All filters use `FullyQualifiedName~` with the complete suffix above. Each owning
project was discovered explicitly before execution. The lock tests demonstrate both
effect commit and effect rollback before quiescence, quiescence before a waiting
publication, and an unrelated root making progress while the selected root is held.
They prove the composed SQL condition; they do not claim a real Agent/provider/Workbench
publication or manager browser path. No provider or external API was called.

Complete portability-static passed with **15,623 unchanged reviewed findings**; the
baseline was not rewritten. Portability baseline/secret-scanner tests, rendering tests,
rendering register, documentation and final diff-check passed. Broad Stable was not
rerun: no release/merge closure is claimed and the previously recorded broad failures
remain open.

## Architecture gate

Pass with follow-up. CodeAnalytics `snap-20261019193102-d1ce2c09` uses the same four
Process projects as `snap-20261019185941-d1ce2c09`: 1,008 types, 9,035 members, 27
entities, 328 findings, **zero added/removed findings and zero cycles**. Its 190 existing
load diagnostics and prior AddSingleton collector ambiguity remain; direct builds and
the executing composition test supplement the incomplete static DI collection.

The implementation separates pure occurrence/admission values, canonical reads and
enlisted publication checks. Integration tests exercise the new owners through their
registered contracts. No source/manager authority or schema-handshake capability is
inferred from the new interface. Actual producer/child-journal integration remains the
next gate, including replay after lost child reservation and original material age.

## Identity and resource closure

- Source: 9,459 files, manifest SHA-256 `21FC89C12564BEA48DF5167C0DFB82811182C4682D3D2BF245502D96FD76EA24`.
- Runtime: 6,065 files, manifest SHA-256 `51EA189E7B966D0EE29790F4C7FDAB5545B7B7D27698F3D5464E78BD575E3494`.
- Observer: 317.9468509 seconds, covering all four complete execution commands.
- Reconciliation: zero remaining observed processes, zero database-name delta; 16
  unattributed TEMP entries preserved without claiming ownership.

Ownership was recorded before the candidate was created. The existing isolated
PostgreSQL18 fixture and standard outputs were reused. The actual restart TEMP prefix
is explicitly inventoried. Complete source/runtime identities were verified unchanged
before releasing the freeze. Earlier denied cleanup was not retried; the shared fixture
remains required for continuing work, so this is not full bundle cleanup.

Compact proof: `proof/2026-10-19-planning-authority/c4-process-child-publication.json`,
SHA-256 `56B1277D9A926996B594645DD214552B5213ECF40A418C60FBC5F4C0F099A8D1`.
Signed checkpoint staging is queued behind the already-running Agent recovery GPG
operation; this record does not claim the queued checkpoint has been committed.
