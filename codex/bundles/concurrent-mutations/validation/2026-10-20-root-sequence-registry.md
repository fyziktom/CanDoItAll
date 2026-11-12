# Process root sequence registry lifetime

All 26 selected tests pass: 11 new pure registry cases, four existing event-store/unit-of-work
Unit cases and 11 PostgreSQL Project/root ordering, budget competition, rollback and original
receipt cases. Exact discovery matched 15 Unit and 11 Integration cases. Unit execution took
4.513 s and Integration 51.256 s; no skips or failed cases are counted as passes.

The existing persistence owner now counts holders and waiters before awaiting its per-root
semaphore. Cancellation drops its pending reference and releases earlier roots in reverse;
last release removes/disposes the entry. Sixty-four fixed short registry mutexes protect
same-key reference transitions without holding a mutex across awaits or owner work. Sorted
multi-root acquisition remains unchanged. Interlocked lease disposal prevents duplicate
release. No public contract, project edge, DI registration or transaction boundary changed.

New tests prove no overlap with eight contending callers, reversed root ordering, unrelated
root progress, cancellation of a later root or one of several waiters, concurrent disposal,
300 cancellation/reuse rounds and reclamation after 10,000 distinct roots. Existing actual
owners preserve event sequence/idempotency and SQL ordering/recovery behavior.

The identical standalone probe loads actual standard Release assemblies in fresh processes.
All three baseline runs retain 45,009 entries after all leases end; both candidate designs
retain zero. The first single-mutex candidate was not accepted because its independent-root
median rose from 10.243 ms to 38.042 ms. Fixed registry partitions restore that observation
to 10.774 ms. All raw timings, allocation counts and first-run outliers remain in proof.

| Probe scenario | Operations | Baseline median ms | Single mutex ms | Final partitions ms |
| --- | ---: | ---: | ---: | ---: |
| Repeated idle same root | 5,000 | 9.869 | 11.423 | 9.103 |
| Distinct roots | 5,000 | 13.343 | 12.576 | 9.555 |
| Eight independent roots | 40,000 | 10.243 | 38.042 | 10.774 |
| Eight callers, one root | 4,000 | 10.685 | 10.731 | 7.931 |
| Eight callers, distinct-root churn | 40,000 | 26.119 | 34.546 | 10.267 |

Final completely idle acquire/release cycles allocate 184 additional bytes per operation;
overlapping holders reuse the current entry. This is an explicit cost of reclaiming retained
entries, not an allocation improvement claim. Dictionary storage can retain peak concurrent
capacity. Baseline history tests and initial-candidate builds supplied differing background
load; these short experiments establish the retention defect and inform design, not controlled
production throughput or sustained capacity. Full workload qualification remains open.

Final direct standard Release builds pass: Persistence 3.670 s, Unit 49.929 s, Integration
46.725 s. Initial builds and both complete static runs also pass. Final full portability
enforcement preserves all 15,647 reviewed findings without rewriting the baseline. Baseline,
secret-tool, rendering, documentation and diff checks pass. Architecture snapshots
`snap-20261020101355-8624fb63` → `snap-20261020103054-8624fb63` have 153→154 types,
1212→1221 members, unchanged 58 findings and 189 diagnostics, and no scoped cycle.

Frozen source: 9,634 inputs,
`8810EE4079FC10724E4EB7BE774557D0E8DB4464D72BCAABCEBA2F8A7D84F917`.
Runtime: 2,589 files,
`23609C322D77A952C578F539AF79163C0E92D11431BDF821856B9E6D21BAB90D`.
Final verification is unchanged. The observer covers both complete commands for 64.122 s.
No observed process or database-name delta remains; seven TEMP paths without established
ownership remain untouched. The reusable PostgreSQL fixture remains needed.

The separately owned probe has no active process. Automatic approval review rejected the
validated removal of its exact `bin`/`obj` paths, stating only `blocked by policy`. No deletion
occurred and no workaround was attempted. Both directories remain under
`.artifacts/concurrent-mutations/validation/root-sequence-probe`; its ownership sidecar and
retained proof record `CleanupBlocked`. This is not global resource-cleanup closure.

Retained [proof](../proof/2026-10-19-planning-authority/c4-root-sequence-registry.json)
SHA-256: `0314889C8D1DD27497EC0D39DC95018006F141A52ED25325F5C5F4A30E4BAB1C`.
Explicit secret-pattern/private-sentinel review passes. Producer/manager/Workflow integration,
sustained capacity and broad completion remain open. SPA remains on its dedicated branch.
