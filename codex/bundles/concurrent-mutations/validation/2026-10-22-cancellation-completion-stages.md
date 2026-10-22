# Decision, shared-scope and root cancellation recovery

Status: Behavioral stage-composition gate passes, 141 selected cases. The registered
dispatcher can finish a durable cancellation cycle through the original runtime,
accounting and scope owners. Automatic scheduling, HTTP cutover, capacity and final E2E
remain open. SPA stays on `codex/concurrent-mutations`; its main branch is untouched.

Application owns the finite stages; Persistence owns metadata selection and persisted
quiescence. Decision recovery retains the original generation and child, obtains its
original stopper outcome, then delegates measured accounting and finalization to the
existing coordinators. Pending children advance with sticky evidence so later children
can progress. Historical open generations remain explicit invalid evidence.

Shared scopes use their existing transactional transitions. An interrupted capture stays
pending until its actual owner records measured abort; recovery never invents zero work.
Quiescence includes persisted root-local claims and steps as well as descendants,
decisions, budgets and scopes. Legacy Cancelled roots receive a real no-effect receipt
under those same guards. Shape validation precedes replay/transaction entry and rejects
business-state changes or unrelated effects. The optional flag preserves old wire bytes.
Finalization keeps the state timestamp monotonic while preserving its original event time.

Every mutation retains its exact action first. Lost acknowledgements recover through the
existing receipt owner before revalidation. A sticky cycle restarts with bounded backoff;
empty selection alone does not authorize completion. Registration starts no worker.

| Selected class | Project | Expected / discovered / passed |
| --- | --- | --- |
| ProcessCancellationQuiescenceRecoveryStepTests | Unit | 19 / 19 / 19 |
| ProcessCancellationScopeRecoveryStepTests | Unit | 11 / 11 / 11 |
| ProcessCancellationDecisionRecoveryStepTests | Unit | 16 / 16 / 16 |
| ProcessRootCancellationConfirmationTests | Unit | 19 / 19 / 19 |
| ProcessCancellationRuntimeRecoveryStepTests | Unit | 22 / 22 / 22 |
| ProcessCancellationRecoveryCoordinatorTests | Unit | 22 / 22 / 22 |
| ProcessCancellationBarrierTests | Unit | 3 / 3 / 3 |
| ProcessCancellationQuiescenceRecoveryTests | Integration | 4 / 4 / 4 |
| ProcessCancellationDecisionRecoveryTests | Integration | 3 / 3 / 3 |
| ProcessCancellationScopeRecoveryTests | Integration | 1 / 1 / 1 |
| ProcessCancellationCompletionIntegrationTests | Integration | 2 / 2 / 2 |
| ProcessRootQuiescenceTests | Integration | 19 / 19 / 19 |

Exact class-prefix discovery matches all 141 cases. The two full-stage tests use actual
StepSnapshot and RunSnapshot Agent producers, PostgreSQL, registered DI owners and new
leases after injected lost commit acknowledgements. They verify original measured work,
local/root accounting, Agent and root cancellation, scope closure, durable queue
completion and no extra provider requests, business publication or receipts on replay.
These tests do not claim operating-system process-restart or sustained fairness proof.

Nine affected Runtime/Application/Persistence/Workbench/Processes/Composition/Web/Unit/
Integration projects were refreshed sequentially in standard Release with
`--no-restore --no-dependencies -p:UseLocalCanDoItAllLibraries=true /m:1`.
The initial Integration build found one test-only result-shape typo; its correction and
refresh passed before discovery/execution. No runtime test failed. All 32 owning/copied
DLL/PDB identities and 45 source pins match after execution.

Architecture review passes for stage composition: no cycles or reference changes, no
new implementation partial files. Three new informational size findings describe the
cohesive decision/scope owners and confirmation guard; splitting these only to satisfy
a member-count heuristic would obscure their invariants. Runtime diagnostics remain 0,
Application 0 and Persistence 10; module design-time diagnostics fall from 124 to 123.
These existing diagnostics are not substitutes for the passing compilation checks.

Full portability passes with all untracked protected source included and the unchanged
15670-finding baseline. Rendering and all three validation-tool self-test groups pass.
Documentation and whitespace checks pass. Broad Stable is reserved for the frozen
integration candidate under the existing named invalidation gate.

The observer had two setup refusals before test execution: a missing unchanged helper,
then a missing machine-readable ownership adapter. Human-readable ownership preceded
creation; the adapter honestly records its later creation and verifies that ledger.
Both failures remain retained. The corrected observer covers all 12 execution commands;
reconciliation finds no new process, database-name or temporary-path residue. Existing
isolated PostgreSQL and standard outputs were reused, with no new host/container.

Compact proof: `proof/2026-10-22-cancellation-completion-stages.json`, 177346 bytes,
SHA256 `A279F4C32589A2969829E27CC7B6B7E60CA244911DFFE0A64DB9C387AC759375`.
Required command/TRX, identity and architecture evidence remain. Historical cleanup
denials remain unresolved and were not retried.

Secret-pattern and private-password scanning passes. Both redundant scan JSON/CSV files
were removed after exact ownership and proof/hash verification; their absence is recorded
in the candidate's `redundant-scan-cleanup.json`.

Next: migrate and measure composite recovery indexes; prove held late publication and
registration windows, process restart/takeover, bounded owner I/O and live/legacy fairness;
resolve the compact-index cost regression before worker activation and HTTP cutover.
Refresh, Workflow/settings, remaining writers and final Stable/E2E are still open.
