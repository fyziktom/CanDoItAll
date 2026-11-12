# Root Process budgets and atomic child funding

Gate: **Pass with follow-up**, 2026-10-19. The repaired candidate passed all **361/361**
discovered cases on unchanged frozen source and runtime inputs. Root accounting and
funding are implemented; active shared material ownership, actual child journals and
Process/Workflow executor activation remain open.

## Behavior and boundaries

A genuine root budget retains its original policy provenance and admission command.
It belongs to the real root run, with a null step rather than a synthetic step. Nested
launches reuse it after the existing parent-step precondition verifies runtime lineage.
Each step retains its own admitted ceiling and a typed reference to the original root.

Local reservations and settlements update the exact root allocation in the same fenced
runtime transaction. Deterministic allocation identity, a restrictive parent FK and a
unique reverse index prevent double funding or adoption of another allocation. Original
receipts retain both results and survive later spending, settlement, restart and lost
acknowledgement. Unknown outcomes remain reserved. Overrun is retained in both balances;
root cancellation or terminal state blocks new descendant work, while closed usage can
still settle. Publication checks root overrun/status as well as the local generation.

Root policy and funding identity belong to Runtime. The internal Persistence funding
collaborator uses the existing DbContext/transaction and shared row mapping. No provider,
journal, permission callback or sibling enumeration is added to ordinary allocation.
Unrelated-root progress is proven with a held funded transaction. Shared RunSnapshot
replanning is charged only at the root; local children cannot reserve their own replans.

Root admission is bounded to 32 KiB, receipts to 8 KiB, enclosing provenance to 32 budgets
and total root allocations to 256 across captures, orchestration and descendants. Grant
sizing/fair scheduling and workload capacity remain required before producer activation.
These correctness tests do not establish a sustained rate or latency service level.

## Compatibility and repaired candidate

Funded step admission uses schema 4, root admission schema 1, and root/funded receipts
version 2. Exact old step encodings remain unchanged. Prepared root admission has a
distinct hash domain because old preparation readers ignore unknown properties.
Migration `20261019215227_AddProcessRunDecisionFunding` preserves old rows and refuses
downgrade when new budgets, allocations, receipts or prepared admissions exist.

The first candidate executed 361 cases: 327 passed and 34 failed. Thirty-three legacy
publication tests exposed non-nullable metadata fields in an optional parent SQL join.
The projection now explicitly represents SQL nulls. The other failure expected a
serialization exception for an empty root ID; the existing typed identifier correctly
throws its argument exception. The test now checks that exact rejection. All 361 cases
were rebuilt/discovered/repeated after the production correction; no unchanged-production
bridge is used to dismiss the original failures.

## Validation

Direct standard Release builds passed for Runtime, Application, Persistence, Processes
module, Migrations, Web, Integration and Unit. EF reports no pending model changes.

| Exact selected topic | Discovered / passed | Seconds |
| --- | --- | --- |
| `ProcessDecisionFundingPersistenceTests` | 35 / 35 | 140.2919774 |
| `ProcessDecisionMaterialPersistenceTests` | 49 / 49 | 184.1407189 |
| `ProcessDecisionPersistenceTests`, `ProcessDecisionBudgetPersistenceTests`, `ProcessDecisionPublicationFenceTests` | 85 / 85 | 317.3397881 |
| Material/generation/child identity/execution contract/material protocol/run budget Unit topics | 166 / 166 | 3.3215028 |
| Exact prepared material/policy/root-funding methods | 13 / 13 | 57.9035119 |
| `ProcessDecisionDataPolicyAdmissionTests` | 13 / 13 | 3.284805 |

Exact filters, discovery and command records are in the candidate resource manifest and
retained proof. Cases cover competing descendants, actual restart, post-flush rollback,
lost commit acknowledgement, original prepared admission, malformed/cross-scope evidence,
root cancellation/overrun publication refusal, parent takeover, unknown reservations,
the 256-allocation bound, legacy migration round trips and downgrade refusal.

Full portability rescan and final enforcement pass with **15,635** reviewed findings.
The initial four additions are generated metadata for RootPathState, RootPathSyntax,
RaisesEscalation and EscalationCount; no executable portability defect is accepted.
The repaired rescan has zero additions/stale entries and does not rewrite the baseline.
Rendering, documentation, diff checks and validation-tool tests pass.

CodeAnalytics snapshot `snap-20261019222407-d1ce2c09` covers four projects, 1,034 types,
9,215 members and 29 entities, with zero cycles. The 190 collector diagnostics and one
existing registration ambiguity are unchanged. Relative to shared content, six new
informational size findings and one removed finding reflect cohesive contract/receipt
shapes and the extracted shared policy wire. Existing large-file findings changed for
thin admission/delegation additions. The nullable correction adds no further findings.
No new project reference or runtime partial was introduced.

## Frozen evidence and resources

Initial candidate `c4-process-run-budget-funding` retained 361 results and 34 failures:
source 9,496 files, SHA256 `511813D5B0AF553518AE6CEE8D16A5775B5379456D1461120B4DBC2648390D23`;
runtime 6,065 files, SHA256 `B7346A8EA58BF7963AFBA71C0AF71F7498899DE4C224A9174A513C83F68B6993`.
Proof SHA256: `1EB8377822C5E8A64BE10216833D3E52E35F99E57223CCEE3752FDE9B4427100`.

Passing candidate `c4-process-run-funding-publication-repair` retained all 361 passes:
source 9,497 files, SHA256 `7E06572AF3E3A5186F5AEAEC7D803622B8F7710934D2D6BB3A4994E84AD7E923`;
runtime 6,065 files, SHA256 `2179D49AF2B002F7D522B15FFAFF0A5AC407095D14C73DC32C9DD6818BA3285B`.
Proof SHA256: `59918FD2E94C745E639382789FB9C54211123E99DFAD75E4060175CA950EA481`.
Both proof JSON files remain under the ignored local
`proof/2026-10-19-planning-authority` directory.

Whole-command observation covered both runs (705.5132419 and 730.847723 seconds).
The repair observer initially rejected the recorded directory move before any tests
started. Its successor verifies the complete original/target ownership chain; both
ownership records and the failed startup remain retained. Observer and freeze scripts
are pinned in the companion inventory. No process or database-name delta remained.
Thirty-two and twenty unattributed TEMP entries were preserved without claiming ownership.
Earlier cleanup denials remain unchanged. The shared isolated PostgreSQL fixture is still
needed for continuing work. The passing freeze was released at
`2026-10-19T22:42:58.6771142+00:00` before documentation updates.

## Continuation

Continue [active run material ownership](../plan/P07-active-run-material-scope.md), including
the capture/quiescence race and grant-sizing policy, followed by versioned inherited Agent
journals, exact child recovery and real Process/Workflow publication paths. R5/R6 and broad
E2E/capacity closure remain open. SPA remains on `codex/concurrent-mutations`, without merge
to its main branch.
