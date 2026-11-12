# Finite runtime cancellation recovery stage

Status: Behavioral gate passes, 87 qualified cases. The runtime stage and its registered
Agent bridge are ready for composition with the remaining stages. Automatic recovery,
HTTP cutover, capacity and final E2E remain open. SPA stays on its separate branch.

Application owns the finite runtime step. Each advance selects one Process run, retains
or resolves one original cancellation intent, or inspects at most one Agent candidate.
Persistence supplies metadata-only next-run/exact-status queries with a five-second
command timeout. The Process module bridges the existing compact page owner and exact
ordinary stop port. Agent models stay outside Application and Persistence. Terminal
runs and the root itself still require Agent inspection; the root never receives a
descendant cancellation intent. All advances require its durable original barrier.

Recovery retains the original action before invoking cancellation and checks its exact
idempotency key before re-executing. The existing owner returns a completion marker
with current state, not historical events. Recovery validates its original run/root,
immutable Cancelled state and absence of unrelated decision/scope/accounting receipts.
New cancellation commits must supply the expected event/context and matching projection
outbox. No historical event is invented or recovered by matching timestamps. Unknown
acknowledgements retain the original intent; definite refusals advance with sticky
pending evidence. A missing index also remains pending while allowing later runs to
progress. The page reader never repairs or creates the index.

Inherited Agent selection delegates only to a matching persisted original Process
child. Its existing Workbench stop adapter now captures profile ID and fingerprint as
values, so an in-place profile mutation at the final original-source observation cannot
change both sides of an authority comparison. Both late-change regressions preserve
the original journal and restore the test profile before fixture disposal.

| Class | Project | Expected / discovered / qualified |
| --- | --- | --- |
| ProcessCancellationRuntimeRecoveryStepTests | Unit | 22 / 22 / 22 |
| ProcessExecutionRecoveryTests | Unit | 20 / 20 / 20 |
| ProcessCancellationRecoveryCoordinatorTests | Unit | 22 / 22 / 22 |
| ProcessCancellationRuntimeRecoveryTests | Integration | 2 / 2 / 2 |
| ProcessExecutionStopperTests | Integration | 7 / 7 / 7 |
| ProjectTaskPlanProcessChildStopperTests | Integration | 14 / 14 / 14 |

Exact class-prefix discovery precedes each execution. Native PostgreSQL proves root,
terminal-descendant and unrelated-root selection without loading runtime payloads. It
also proves original cancellation receipt recovery after lost commit acknowledgement,
new context and new lease, without duplicate events or receipts. Registered queue-to-
page-to-stop composition uses actual new Agent admission through its original producer.
Inherited selection proves both retained-child delegation and missing-child refusal.

Eight initial Release builds passed, but source review corrected the event-list
assumption before execution. Eight refreshed builds qualify Application, Persistence,
Workbench, Processes module, Composition, Web, Unit and Integration. The first 64 Unit
cases passed. Both initial native cases failed because the new fixture omitted the
parent dispatch claim and used rejected descendant admissions without asserting success.
Only that fixture changed: reuse the existing claim/clock owner and assert every seed
commit. Integration was rebuilt and all 23 cases passed. The retained 64 Unit passes
are bridged by 27 unchanged source pins and all 26 unaffected binary identities. The
final candidate verifies all 28 owning/copied DLL/PDB outputs. Both failures are retained;
there was no production repair or broader test rerun for this fixture correction.

All builds use standard Release outputs, `--no-restore --no-dependencies
-p:UseLocalCanDoItAllLibraries=true /m:1`, in dependency order. Architecture checks find
zero cycles in all three changed scopes. Application grows 402→407 types and 4178→4195
members with the same 107 findings and no diagnostics. Persistence grows 197→198 types
and 1489→1492 members with the same 66 findings and ten existing diagnostics. The Process
module grows 360→361 types and 2917→2919 members with the same 143 findings and 124
existing design-time diagnostics. Diagnostic codes/severity/messages remain unchanged;
these snapshots are not compilation proof. No finding targets a new owner. There are
no project-reference or partial expansions.

Full portability passes after the final test edit, without baseline changes (15670
reviewed findings); rendering review passes 1212 inputs. Documentation/whitespace gates
pass. All seven test commands are observed across the two candidates, with zero new
process, database-name or temporary-path residue. Existing PostgreSQL and standard
outputs are reused; no new host/container is provisioned.

Compact proof: `proof/2026-10-22-runtime-recovery-stage.json`, 437275 bytes, SHA256
`56198FB16AFB902C17F57938EA9C84F974807B13D6936A15F71757A706BFD821`.
Secret-pattern/private-password checks pass. Each candidate's redundant scan JSON/CSV
was removed after exact ownership/proof/hash checks and verified absence; required
commands, TRX, identity and architecture evidence remain. Historical denied cleanup was
not retried and remains unresolved.

Next: decision accounting/finalization, shared-scope transitions and independent persisted
quiescence, including root-local work and a real no-effect confirmation for historical
Cancelled roots. Add/measure composite indexes and qualify late publication, bounded
I/O, fairness and the compact-index cost regression before host activation. Refresh,
Workflow/settings, remaining writers and final Stable/E2E are still open.
