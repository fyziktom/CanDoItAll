# Root cancellation quiescence and new-work fence

Status: **Pass for the persisted guard**, following signed `94a4d1ac9`. This is not
durable worker, inherited producer or end-to-end activation.

## Behavior and boundary

One small internal Persistence owner checks newly cancelled roots under the existing
root lock/transaction. Outstanding descendants/live claims, current or historical
decision work, unsettled allocations/reserves and open shared capture prevent finalization.
The proposed root cannot retain a live claim or running step. The check returns an
explicit rejection and leaves runtime, events, receipts, outbox and accounting unchanged.

A shared pure classifier compares proposed work with persisted fields rather than
caller-supplied original claim details. It detects new or extended claims, reactivation,
reservations, decision enrollment/refresh and capture start. Stopped roots cannot admit
that work, including via a descendant. Legitimate failed-root rework still works.
Historical accepted commands replay before these checks; exact closure and measured
settlement remain available. Final settlement and finalization use separate short commits.

The UoW retains receipt/CAS/transaction ownership and its original 43 members. Shared
scope admission uses the same classification and loses duplicated checks. No schema,
public contract, project reference, DI registration or Agent dependency is added.
This checks persisted Process work; owner-side uncertain effects remain pending through
their existing journals/accounting contracts. It does not infer external completion.

## Selected proof

All **367 distinct cases** are qualified; **51** are new. The initial run passed 287,
failed one fixture and stopped before 79 cases. Exactly one test method now uses the
existing final-child Completion=Cancel command instead of reopening a cancelled step.
The rebuilt final selection passes all 80 cases (that method plus the 79 unexecuted
cases). An exact source/runtime bridge verifies the other 287 behaviors and all
production sources/binaries are unchanged; only Integration DLL/PDB were refreshed.
The original failure remains retained. Selection verifies exact current source method
names, not only counts. The new cases include forged snapshot
claim renewal, historical unclosed children, missing current generations, zero-work
replan allocations, interrupted capture, terminal descendants with live work, original
receipt replay and competing root/independent-root commits. Legacy malformed states
are explicitly labelled fixtures, not produced through newly forbidden transitions.

| Lane | Passed |
| --- | ---: |
| Unit-ProcessRuntimeWorkAdmissionTests | 18 |
| Integration-ProcessRootQuiescenceTests | 19 |
| Integration-ProcessRootWorkAdmissionTests | 14 |
| Unit-ProcessPersistenceStoreTests | 77 |
| Unit-ProcessCancellationBarrierTests | 3 |
| Unit-ProcessDecisionTerminalAccountingTests | 57 |
| Unit-ProcessRuntimeOperatorApplicationServiceTests | 8 |
| Integration-ProcessCancellationBarrierPersistenceTests | 5 |
| Integration-ProcessDecisionBudgetPersistenceTests | 21 |
| Integration-ProcessDecisionFundingPersistenceTests | 35 |
| Integration-ProcessDecisionPersistenceTests | 31 |
| Integration-ProcessDecisionRunScopePersistenceTests | 9 |
| Integration-ProcessDecisionRunScopeRecoveryTests | 12 |
| Integration-ProcessDecisionTerminalAccountingIntegrationTests | 10 |
| Integration-ProcessRuntimeDispatchPreconditionTests | 22 |
| Integration-ProcessStepMaterialProducerTests | 13 |
| Integration-ProcessDecisionReviewRootTests | 3 |
| Integration-ProjectTaskPlanProcessChildStopperTests | 10 |

## Cost and architecture

The quiescence query projects four Boolean values with correlated EXISTS. Two warmups
and five measured reads at each terminal-descendant population all execute exactly one
SQL statement and return four scalar columns, without payload/ID materialization.

| Descendants | Median ms | Maximum ms |
| ---: | ---: | ---: |
| 0 | 15.9462 | 20.1164 |
| 128 | 13.6281 | 17.6856 |
| 1024 | 22.3914 | 54.9296 |

Pure admission classification has ten warmups and 100 measured iterations per shape:

| Steps | Mean microseconds | Allocated bytes per call |
| ---: | ---: | ---: |
| 1 | 0.547 | 888 |
| 128 | 11.159 | 5496 |
| 1024 | 246.005 | 40488 |

These are finite local diagnostic measurements using the existing PostgreSQL 18 fixture
with pooling disabled, not sustained capacity or a production latency guarantee. The
classification is linear in the current/proposed claims and steps. Quiescence is only
queried for finalization; new descendant work adds one indexed root-status read.

Scoped Persistence snapshots change from `snap-20261022033809-531e33b7` to
`snap-20261022044324-531e33b7`: 179 to 182 types, 1373 to 1389 members,
204 to 209 dependency edges, unchanged 32 entities and 62 findings, zero cycles.
The five added type edges stay internal. Ten pre-existing WS0002 package-advisory
diagnostics remain; successful direct builds are separate evidence, not a claim of
globally clean analysis.

## Validation identity and resources

Direct standard Release Persistence, Web, Unit and Integration builds pass, followed
by the one-test Integration rebuild. Full
portability enforcement passes with 15665 reviewed findings unchanged; rendering
review passes for 1212 current inputs. Documentation and whitespace checks pass.
Each candidate stayed identical from prebuild through execution; runtime identity also
passes. The exact one-method repair bridge connects them.

Source SHA256: `C9CC926FF7980C3EACAEDC0BE0BE2455E243CC5B86F98CDD09E040218B512202`.
Runtime SHA256: `6DA07CD7E13D4CDBCBA727F66671E4BAB03E81A05BEF939AABD4B5B067C8CC26`.

The initial observer attempt refused a stale Freeze.py companion checksum before any
resource baseline or test execution. The one added prebuild-equality assertion and
old/new checksum are retained. Product source and test outputs did not change.

[Compact proof](../proof/2026-10-19-planning-authority/c4-root-cancellation-fence.json),
SHA256 `07B6057FA2ED6BBDAFA28EA258788AB4D75906C4F257856D6FB8512B264AC81F`, retains selections, commands, outcomes, measurements, architecture,
input identities and resource reconciliation. It is written as canonical UTF-8/LF
and passes secret scanning including the private fixture-password sentinel.

All 19 test commands across both candidates were observed; neither run leaves a new
process, database name or temporary path. The shared isolated fixture is still needed
for subsequent integration. The
ordinary instance on 5032 and installed tooling are preserved. Historical automatic
cleanup rejections remain in [resources](../resources.md); none was retried or closed.

## Next gate

Complete owned Agent cancellation callback lifetime and durable fair root recovery,
including a legal empty-decision reconciliation path that preserves a terminal step,
then original-generation snapshot refresh and actual producer/manager integration.
Workflow/settings, remaining writers, sustained capacity and final integrated/Stable
qualification remain open. SPA stays on its separate `codex/concurrent-mutations`
worktree without merge or push.
