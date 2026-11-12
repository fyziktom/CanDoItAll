# Indexed Process cancellation selection

Status: Behavioral index gate passes, all 24 selected Integration cases. Runtime and
decision continuation now use `(RootRunId, RunId)` and `(RootRunId, DecisionId)` indexes.
Worker/HTTP activation and full E2E remain open; SPA main is unchanged.

The real PostgreSQL migration builds each replacement concurrently before removing its
redundant root-only index. Downgrade restores the prefix index first. Exact catalog guards
check table, key order, ascending UUID operator classes, no expressions/predicate/include/
uniqueness, and valid/ready/live state. Conflicting or invalid named objects require
explicit operator repair. They are never silently removed. Reentry after completed
commands preserves rows and always leaves a usable root index.

| Class | Expected / discovered / passed |
| --- | --- |
| ProcessRecoverySelectionIndexMigrationTests | 13 / 13 / 13 |
| ProcessCancellationRecoveryMigrationTests | 4 / 4 / 4 |
| ProcessCancellationRuntimeRecoveryTests | 2 / 2 / 2 |
| ProcessCancellationDecisionRecoveryTests | 3 / 3 / 3 |
| ProcessCancellationCompletionIntegrationTests | 2 / 2 / 2 |

The new cases exercise all eight interrupted up/down boundaries, two conflicting/invalid
index variants, fresh upgrade/restart/downgrade, independent writes during a held index
construction, and actual reader query plans. All original runtime/head rows survive.
Existing readers and both real Agent full-stage recovery flows pass on the new model.

The cost fixture contains 50,000 synthetic metadata rows per owner, including a 20,000-row
root and a 1,000-row root. Both actual readers retain their five-second command timeout.
Before migration, the dense-root queries use primary-key scans (6 buffers each); the
sparse-root queries sort (15 and 12 buffers). After migration all four queries use the
composite indexes, touch 4 buffers and have no sort. Single observed execution samples
range 0.021–0.076 ms after versus 0.032–0.123 ms before. Construction takes 139.8 ms.
These warm local samples are not latency percentiles or a production migration SLA.

Each composite index is 2,596,864 bytes versus 450,560 bytes for the deduplicated prefix
index. Seven 256-row insertion batches per schema have medians 178.49 ms before and
177.22 ms after (0.7% lower in this sample), with all four related tables populated.
This records the storage/read tradeoff without claiming sustained concurrent capacity.

The first candidate passed 12 safety cases but timed out during one 50,000-row fixture
insert, before measurement; later lanes were not executed. Only cost setup changed to
1,000-row batches. The complete final 24-case selection passed. Original production and
reader timeouts remain unchanged. Final cost execution started after static scanning
finished, avoiding that competing load. The failure and both reconciliations are retained.

Persistence, migration scaffold/final, Web and Integration builds pass in standard
Release, sequentially with `--no-restore --no-dependencies
-p:UseLocalCanDoItAllLibraries=true /m:1`; the test-only repair then refreshed Integration.
All 11 owning/copied binary identities and 37 source pins verify after execution.
CodeAnalytics remains 200 types, 1499 members, 67 findings, 10 existing design-time
diagnostics and zero cycles. There are no new findings or project references; generated
EF partial/designer files follow the existing schema convention.

Full portability includes untracked source. Four added findings are the generated
`RootPathState`, `RootPathSyntax`, `RaisesEscalation` and `EscalationCount` property
declarations already present in previous designers. Their reviewed baseline delta adds
only those entries; final enforcement passes without `--write-baseline` (15674 findings).
Rendering, tool self-tests, documentation and whitespace checks pass. Broad Stable was
not repeated for this focused change.

The first failed command and all five final commands are observed. Both reconciliations
find zero new process, database-name or temporary-path residue. Existing PostgreSQL and
standard outputs were reused. Earlier cleanup denials remain open and were not retried.

Compact proof: `proof/2026-10-22-recovery-selection-indexes.json`, 144973 bytes, SHA256
`4759FEA031CBF56D397CEBE3822B35B2163C377EDF23DFD2AE3A7CF063F7BC4A`.
Secret-pattern/private-password scanning passes. All four redundant scan JSON/CSV files
were removed after ownership and proof/hash verification; exact absence is recorded in
the candidate's `redundant-scan-cleanup.json`. Required evidence remains.

Next: held late publication/registration races, process restart, bounded I/O, live/legacy
fairness, the separate whole-journal cost regression and worker/HTTP cutover. Refresh,
Workflow/settings, remaining writers and final Stable/E2E remain open.
