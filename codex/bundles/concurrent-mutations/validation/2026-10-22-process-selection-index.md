# Compact Process selection index

Status: Behavioral correctness passes. Read cost is reduced, but write-cost investigation
and worker fairness remain open. This checkpoint does not activate recovery or close the
concurrent mutations bundle. SPA remains on its separate `codex/concurrent-mutations` branch.

## Ownership and crash consistency

Core exposes separate page-read and explicit index-preparation ports. The existing scoped
workspace store supplies publication exclusion; one internal Persistence owner derives
and validates the compact projection. It contains execution/Process identities, unknown
legacy lineage and completeness/version evidence, never authority, effects or outcomes.
Selected original run records are still read afresh. Missing, stale, future or corrupt
projection evidence cannot become successful exhaustion. Reads do not repair or migrate.

Every chat projection publisher derives the compact file from its exact target: bulk and
initial publication, new/existing runs, chat creation, session updates, Agent deletion,
rebuild and retained-journal recovery. Both projections finish before the original journal
is cleared. The same prepared target can therefore recover a crash between the chat and
compact writes; no second canonical journal or cross-store commit was introduced. The
shared publication witness and Agent-deletion reconciliation paths include the new file.
Explicit legacy preparation requires a complete matching source projection and reads no
historical journals. It is not an unbounded startup backfill or an automatic reader repair.

## Focused proof

The selected Integration topics all use the prefix
`FullyQualifiedName~CanDoItAll.Tests.Integration.Runtime.` followed by the class and a dot:

| Class | Expected / discovered / final passed |
| --- | ---: |
| FileSandboxWorkspaceProcessIndexIntegrationTests | 23 / 23 / 23 |
| FileSandboxWorkspaceProcessPageIntegrationTests | 18 / 18 / 18 |
| FileSandboxWorkspaceProcessQueryIntegrationTests | 12 / 12 / 12 |
| FileSandboxWorkspacePublicationRecoveryTests | 9 / 9 / 9 |
| FileSandboxWorkspaceProcessIndexCostTests | 4 / 4 / 4 |

The 66 final cases cover real owner publication/recovery, same-stamp changes, incomplete
and future metadata, legacy preparation, orphan projections, missing required fields,
explicit repair, cancellation, pagination and physical journal-read bounds. The real
filesystem failure test obstructs the compact destination after canonical publication,
proves the chat target and retained journal, then verifies roll-forward and idempotent
re-read. Existing publication-key tests cover competing updates and changed recovery
evidence. A shallow test that applies Take after hydrating all journals would fail the
physical-read assertions; successful empty pages from unavailable metadata also fail.

The first owner run passed 22/23: its Agent deletion fixture left the run active and the
existing deletion guard correctly rejected it. Only that fixture was changed to a
consistent terminal run; all 66 cases were rediscovered and rerun. Preserve this failure.
There are 93 retained executions: four baseline cases, 23 first-attempt cases and 66 final
cases, with one historical fixture failure. Prior passes do not substitute for the rerun.

Core, Persistence, Agent module, Web and Integration built directly in dependency order
using standard Release outputs and `--no-restore --no-dependencies
-p:UseLocalCanDoItAllLibraries=true /m:1`. The test-only correction refreshed Integration.
Before/after identities verify all 14 owning/copied DLL/PDB outputs and all source pins.
The separate baseline test-only build used the previously qualified `4a35d5311` production
DLL/PDB identities intentionally; it is not validation of the new production source.
Exact discovery, commands, outcomes, identities and measurements are retained in
`proof/2026-10-22-process-selection-index.json` relative to the bundle root.

## Cost comparison and limits

| Synthetic history | Old / compact metadata bytes | Compact first / repeated page, ms | Compact allocated bytes, first / repeated |
| --- | ---: | ---: | ---: |
| 20,000 | 16,424,265 / 2,240,477 | 32.86 / 27.02 | 6,863,096 / 6,862,208 |
| 100,000 | 82,104,264 / 11,200,478 | 132.98 / 103.55 | 30,240,920 / 30,159,992 |

Each page reads exactly two selected original journals. The prior 100,000-entry page took
1263.90/1002.50 ms. Explicit preparation took 139.64/627.05 ms for the two populations,
outside page timing. These are synthetic unrelated metadata with real selected records,
two samples and uncontrolled filesystem cache, not cold-cache or sustained-load evidence.
The implementation still performs O(history) metadata work and does not establish a
maximum supported population or a history-independent latency bound.

The identical source test creates real history through the owner, warms up 20 writes,
then runs 100 single-attempt writes and 100 concurrent page reads per case. Every baseline
and candidate case accepted all 100 writes, reported zero conflicts and retained each
accepted final revision. No retry concealed a conflict.

| History / writers | Baseline / candidate writes per second | Baseline / candidate write p95, ms | Baseline / candidate read p95, ms |
| --- | ---: | ---: | ---: |
| 0 / 1 | 10.37 / 8.94 | 125.93 / 163.42 | 99.19 / 318.35 |
| 2000 / 1 | 6.78 / 6.38 | 228.19 / 184.65 | 542.06 / 451.35 |
| 2000 / 8 | 6.37 / 6.20 | 4769.29 / 5395.05 | 790.29 / 8.14 |
| 2000 / 32 | 5.42 / 5.22 | 15410.47 / 15838.69 | 40.85 / 17.84 |

The tiny-history sample exceeds the investigation thresholds: throughput falls 13.8%
and write p95 rises 29.8% (37.49 ms). Do not classify this as a performance pass. The
additional compact serialization and atomic publication are real work; this sample
does not attribute their individual cost or explain all observed variation. Throughput
falls 2.6–5.9% at 2000 records. Existing workspace serialization still causes substantial
tail waits, including a candidate eight-writer read p99 of 6237.56 ms. Smaller metadata
alone does not prove fairness or satisfy the coordinator's cooperative two-second budget.

Progression accepts this correctness checkpoint for further integration, not production
capacity or the regressed cost. Before activation, qualify bounded read/stop waits,
cancelled waiting operations and sibling-root progress under contention, then investigate
the small-history cost with repeated controlled samples or a measured redesign. Preserve
the original thresholds. Queue wait, disk flush, WAL, server CPU and sustained throughput
were not separately measured. One actual sample per case cannot establish their causes.

## Architecture and closure

The scoped CodeAnalytics comparison is `snap-20261022142721-198deb42` to
`snap-20261022151727-8e97ad3a`: 145 to 148 types, 1504 to 1529 members, with the same
existing HistoricalReadPass containment/back-reference cycle and ten NuGet-advisory
diagnostics. The two new informational nine-member findings describe the small index
owner and its completeness record. Direct inspection finds one cohesive responsibility,
no new partial, project reference, service locator or inward infrastructure dependency.
This limited snapshot is not a clean whole-repository architecture certificate.

C# architecture gate: Pass with follow-up. Behavioral subbundle closure passes; capacity
and activation do not. The DI alias points to the existing scoped store and its rendering
register entry was reviewed; no rendering, assets or editor lifetime changed. The full
source portability scan includes new untracked source. The one added finding is the
portable Path.Combine for the compact file under the existing storage root; its reviewed
baseline delta is 15669 to 15670. Final enforcement without --write-baseline passes.
Rendering verifies 1212 inputs; portability/redaction/rendering tool tests pass 6/4/7.
Documentation and whitespace checks complete the frozen source/runtime/resource record.

Seven test commands across the baseline and two candidates are fully observed. All three
resource reconciliations report zero new process, database-name or temporary-path residue.
No new host/container was created. Historical cleanup denials remain in the resource ledger
and were not retried. The broad Stable gate remains at the final integrated frozen
checkpoint: shared projection publication/serialization is its named invalidation trigger.
Actual Agent stop/stage integration, HTTP/status, policy refresh, remaining writers and
E2E closure remain open; this checkpoint cannot close R1/R4/R5/R6.
