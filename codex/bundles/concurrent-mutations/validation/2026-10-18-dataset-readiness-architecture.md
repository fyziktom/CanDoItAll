# C# Architecture Gate Result

Status: Pass with follow-up — dataset selection, inventory and rotation foundation.

## Findings

| Severity | Finding | Evidence | Required action |
| --- | --- | --- | --- |
| Resolved | Initial Bootstrap/Attach retains a usable selection for offline repair. | Four fresh/interrupted cases pass; ordinary readiness still refuses legacy unbound work. | Preserve selection publication before the readiness check. |
| Resolved | Actual parameterized queries use indexed bounded reads. | Seven captured production commands pass EXPLAIN ANALYZE checks with 10,000 terminal records before 131 pending records. | Preserve cursor range conditions and the ordered one-row existence probes. |
| Existing | Scoped module/type cycles predate the new inventories and guard. | `snap-20261019003445-cfc506a8` contains no cycle involving the new types and no project-reference cycle. | Retain the existing findings; do not describe the repository as cycle-free. |
| Existing | Workspace diagnostics are degraded by NuGet advisories. | All 189 workspace error diagnostics are package advisories; affected direct Release builds pass. | Keep that analysis limitation visible. |

## Dependency direction

Projects owns completion interpretation and the pending-inventory contract. Agent Framework
and Workbench implement bounded native reads. Composition creates explicit-profile options
and adapters; it contains no module-table query or payload decoder. Infrastructure owns
advisory-lock-before-lifecycle-row exclusion. The guard uses no source filesystem, provider,
callback, service locator or nested host. No project reference changed. The scoped analysis
reports 5,191 dependency facts; the previously reviewed global project graph has 248
projects, 996 distinct internal edges and no project-reference cycle.

## Partial-class policy

No production partial class was added for behavior. The EF migration and designer are the
normal generated pair. Readiness integration tests use a separate class and the existing
activation fixture. The nested Workbench row is a SQL materialization DTO, not an ownership
boundary. Native participants remain responsible for callbacks and claim state.

## Testability proof

All 102 selected integration cases pass, including 19 new readiness cases and existing
native participant, lifecycle, activation and restoration regressions. Tests cover two
header pages, byte-limited short pages, invalid first records, missing/foreign/partial
binding, retirement and obligation checks, Awaiting and Released unfinished work, deletion
after preflight, exact previous-selection restoration and resume after SQL commit. Native
state snapshots prove the checks do not change payloads, claims or completion state.

Final portability enforcement passes at 15,596 reviewed findings. Source/runtime identity
verification passes, with zero remaining observed processes and zero database-name delta.
The four unowned TEMP entries are preserved. The additive three-index migration passes
the pending-model check and idempotent SQL review. Full evidence is retained in
`proof/2026-10-18-offline-recovery/p03-dataset-readiness-validation.json` and summarized in
[lifecycle validation](2026-10-18-lifecycle-integration.md).

## Closure decision

The existing-dataset readiness/rotation foundation may be used by the next repair slice.
The follow-up candidate passes 45/45 integration cases, including seven actual production
plans (0.032–0.363 ms). Direct affected Release builds, final portability enforcement at
15,596 findings and source/runtime verification pass. Cleanup finds zero observed
processes and zero database-name delta; three unowned TEMP entries are preserved.
`snap-20261019010024-cfc506a8` retains the same dependency/cycle set and advisory-only
workspace diagnostic limitation. Compact proof is
`proof/2026-10-18-offline-recovery/p03-dataset-indexed-probe-validation.json`.
Legacy installation and full runtime snapshot behavior remain required. This gate does
not close P03 or the concurrency bundle.
SPA remains on its existing review branch.
