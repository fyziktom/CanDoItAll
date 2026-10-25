# Reuse unchanged Process selection publication

Status: implementation, focused correctness, upgrade recovery, repeated small-history
cost and full portability qualify. Explicit temporary-resource cleanup remains blocked.
Same-workspace contention, worker activation and final E2E remain open. SPA remains on
its separate `codex/concurrent-mutations` branch.

## Change and ownership

The existing Persistence owners carry an optional canonical Process selection revision
through the exact original target. It advances when membership changes and remains stable
for ordinary runtime updates. Compact schema 2 binds to that revision; schema 1 retains
its strict global-revision rule. Selected original records are still read afresh.

The compact owner sorts entries by execution identity, serializes the complete validated
target, and compares actual persisted bytes before replacing the derived file. Missing or
different content follows the original durable writer. Canonical/chat publication, flushes,
publication keys, pending-journal checks and exact owner recovery remain in place. No
cache, count-only shortcut, new project, interface or registration was added.

Legacy prepared source/target pairs with both optional fields absent retain their original
wire shape. A source with a present stamp cannot downgrade its target. Invalid negative or
future revisions are rejected. Existing new-run, session, deletion, bulk and recovery
publishers propagate the target or retain the stricter legacy revision binding.

## Behavioral proof

| Selection | Expected / discovered / passed |
| --- | ---: |
| ProcessSelectionRevisionTests | 8 / 8 / 8 |
| FileSandboxWorkspaceProcessIndexReuseTests | 12 / 12 / 12 |
| FileSandboxWorkspaceProcessIndexIntegrationTests | 23 / 23 / 23 |
| FileSandboxWorkspaceProcessPageIntegrationTests | 18 / 18 / 18 |
| FileSandboxWorkspaceProcessQueryIntegrationTests | 12 / 12 / 12 |
| FileSandboxWorkspacePublicationRecoveryTests | 9 / 9 / 9 |

The exact class filters use `FullyQualifiedName~CanDoItAll.Tests.Unit.AgentFramework.`
for the Unit class and `FullyQualifiedName~CanDoItAll.Tests.Integration.Runtime.` for
the Integration classes, followed by the class name and a dot. All 82 cases pass.
They prove unchanged physical publication, fresh original revisions, identity replacement
at equal counts, missing/corrupt projection repair, strict invalid/stale evidence, legacy
recovery and refusal to downgrade an original target.

The first eight-case Unit run passed six and failed two before entering the tested method:
xUnit could not bind `int` attribute values to nullable `long`. Explicit long literals fix
the test data; all 82 cases were rediscovered and rerun. Preserve that failed execution.

Four additional native handoff executions pass. A temporary test-only probe built against
actual pre-index production `4a35d5311aa23c5294b6fabadc5f4930ebd863d1` interrupts the real
publisher at JournalPersisted and ExecutionIndexPersisted. The current binary verifies
every copied file hash before opening the workspace, recovers the exact original run
revision/title, clears its journal, publishes schema 2, and initializes the optional stamp
on the following write. No copied journal is rewritten to simulate an older schema.
The probe source, exact binary/source identities and both source receipts are retained.

## Repeated cost and attribution

Three alternating pairs use identical real-owner workload and fixture sources against the
pre-index baseline: baseline/candidate, candidate/baseline, baseline/candidate. Each has
20 warmup writes, 100 measured single-attempt writes and 100 concurrent pages. All 600
measured writes and reads succeed, with zero conflicts and every original final revision
retained. Cache and machine background load are uncontrolled.

| Median | Pre-index baseline | Reuse candidate | Change |
| --- | ---: | ---: | ---: |
| Accepted writes/second | 9.9338 | 9.6010 | -3.35% |
| Write p95, ms | 138.4601 | 136.1682 | -1.66% |
| Allocated bytes | 197,736,192 | 201,564,616 | +1.94% |

The unchanged 10% throughput-loss and 15% p95-increase investigation thresholds pass.
The preceding three-pair investigation measured a 12.714% throughput loss and remains
retained. This is a finite overhead qualification, not a production capacity SLA.

The separate diagnostic listener records 743 physical JSON writes, compared with 863 in
the previous index candidate and 740 in the historical baseline. Stable selection reuse
removes 120 repeated publications. Its 123 compact serializations total 7.65 ms; inclusive
JSON-write duration totals 13,762.51 ms. Diagnostics include fixture/warmup and listener
overhead; nested durations cannot be added or treated as a disk-flush breakdown.

All four actual-owner workload cases pass, preserving 400 writes and 400 reads with zero
conflicts. Their results retain the remaining global-workspace contention:

| History / writers | Writes/second | Write p50 / p95 / p99, ms | Allocated bytes |
| --- | ---: | --- | ---: |
| 0 / 1 | 9.7382 | 95.03 / 153.53 / 191.57 | 194,912,784 |
| 2,000 / 1 | 6.9163 | 136.77 / 172.36 / 287.70 | 4,222,270,016 |
| 2,000 / 8 | 6.5329 | 167.36 / 4,752.03 / 8,845.02 | 4,360,829,224 |
| 2,000 / 32 | 5.2600 | 4,097.53 / 15,059.87 / 17,760.79 | 4,834,289,320 |

The metadata diagnostics already included in the 82-case selection retain two original
journal reads per page. At 20,000 entries, compact metadata is 2,240,477 bytes and fresh/
repeated page times are 41.90/33.29 ms; preparation takes 140.85 ms. At 100,000 entries,
metadata is 11,200,478 bytes versus 82,104,294 source bytes; pages take 141.32/132.25 ms
and preparation 771.51 ms. Sorting and exact comparison remain history-dependent work.
Neither these synthetic metadata samples nor the finite writer samples prove sustained
capacity, cold-cache behavior, cross-process fairness, WAL cost or server CPU.

## Architecture and progression

The scoped before/after snapshots are `snap-20261022215400-198deb42` and
`snap-20261022222045-198deb42`: 148 types unchanged, 1530 to 1536 members, 55 findings
unchanged, and the exact same 183 dependency edges. The existing HistoricalReadStore/
HistoricalReadPass containment cycle and ten NuGet-advisory load diagnostics remain.
Separate direct production builds pass; this is not a clean whole-repository certificate.

C# architecture gate: Pass with follow-up. Selection policy stays in its existing owner;
the facade receives only target-field propagation. The pure revision tests require no
runtime, filesystem or database; native tests exercise the real publishers. No partial
class, project reference, service locator or new abstraction was added. No UI/editor
lifetime or rendering seam changed.

Seven affected production/consumer/test projects were refreshed in standard Release;
the typed test correction refreshed Unit. Before/after source and 28 owning/copied output
identities match throughout the 82-case run. Paired tests additionally pin every DLL/PDB
in both actual test output directories. Five observed runs report zero new process,
database-name or fixture-temp residue; local handoff staging and the baseline worktree
still require their explicit last-use cleanup.

Full portability enforcement passes without a baseline write: 15,675 reviewed findings
are unchanged. The 6/4/7 portability, secret-scanner and rendering tool tests pass, as do
the rendering register, documentation and whitespace checks. The complete source scan
includes untracked test files; no protected source was omitted. The compact proof is
`proof/2026-10-22-process-selection-reuse.json`, with 105 retained executions: 103 passes
and the two original test-binding failures. Of those, 97 are the final correctness,
six repeated-cost, five contention/listener and four historical-handoff passes.

Automatic approval review rejected the initial combined cleanup command before it ran,
with `blocked by policy`. The user then authorized the four exact targets separately.
Both temporary `ProcessIndexHistoricalBinaryHandoffProbe.cs` sources were removed, and
the ordinary Integration Release build passed. All source pins and production/copied
outputs match the original freeze. The rebuilt Integration DLL/PDB have new identities:
their informational version now contains signed commit `e0918e98a`; the original
82-case source/output freeze and all handoff identities remain intact in historical proof.

Separate retries of `JournalPersisted` and `ExecutionIndexPersisted` were rejected again
by automatic approval review with the same generic reason. Removal of the exact owned
`C:/cm-cost` junction was also rejected. These paths remain; baseline worktree archival is
deferred until its alias is removed. The retained `.cs.txt`, receipts, inventories and
`cleanup-approved-retry-outcome.json` preserve the original evidence and retry outcomes.

Final resource closure remains pending cleanup. The broad Stable gate belongs at
the final integrated freeze; shared publication/serialization remains its named trigger.
Bounded cancellation under competing writes, sibling-root progress, operational HTTP
pending/status, remaining writers, policy refresh and final E2E remain open. This local
cost repair does not close R1/R4/R5/R6 or enable automatic recovery.
