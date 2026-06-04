# Process Dispatch Artifact Boundary Foundation v1

## Profile

- `initiative`

## Mission

Continue the safe dispatcher decomposition after the process automation execution snapshot boundary. Do **not** start a full `Processes.Core` split. Instead, isolate the artifact projection, artifact validation, lineage, and required-evidence surfaces that currently live inside the large `ProcessRunAutomationDispatchService.*` partials.

## Current Branch Baseline

Source branch: `maf-processes-refactor`.

The previous execution-boundary work is treated as the entry invariant:

- MAF and Tooling remain product-module neutral.
- Dispatcher execution/detail/list operations use process-owned execution snapshots, not raw AgentFramework execution detail/result/query types.
- `CanDoItAll.Processes.Contracts` remains neutral and dependency-free.
- No `CanDoItAll.Processes.Core` project, process driver pack, or domain driver project may be introduced by this bundle.

## Why This Bundle Comes Before Process Core

The dispatcher still contains very large artifact/projection/validation partials. The right next step is not to move domain state into a core project, but to build stable internal seams inside the Processes module and migrate one concrete dispatcher responsibility at a time. This bundle focuses on artifact evidence boundary preparation and staged migration.

## Scope Summary

Included:

- Artifact/projection/validation inventory.
- Internal DTOs/contexts for artifact evidence planning inside `CanDoItAll.Modules.Processes` or neutral snapshots where dependency-safe.
- Projection planning helpers and services that can be tested without mutating the DB/storage where practical.
- A staged migration of selected projection and validation consumers.
- Regression tests for required artifact satisfaction, lineage, receipt metadata, and current-run evidence.
- Refactor gates every few subbundles.

Excluded:

- Full `Processes.Core` extraction.
- EF entity moves.
- UI rewrite or UI redesign.
- Driver packs.
- MAF/Product-module dependency rollback.
- Small/medium/mobile screen validation.

## Large-Screen Proof Policy

This is runtime/service refactoring. Browser proof is expected to be `N/A`. If a rendered UI route is unexpectedly affected, validate **large desktop/PC viewport only**. Do not spend time on mobile, tablet, small-screen, or medium-screen screenshots.

## Recommended Execution Order

1. SB01 Entry audit, branch hygiene, prior boundary smoke.
2. SB02 Artifact/projection/validation inventory and method ownership map.
3. SB03 Artifact evidence seam design and no-production-movement cutline.
4. SB04 Refactor Gate A architecture guardrails.
5. SB05 Artifact expectation matcher and lineage helper foundation.
6. SB06 Projection planner foundation for execution artifacts only.
7. SB07 Migrate execution-artifact projection path through planner.
8. SB08 Refactor Gate B projection parity and line-count review.
9. SB09 Response/mock/workspace projection planning adapters.
10. SB10 Artifact validation rule service foundation.
11. SB11 Refactor Gate C runtime smoke and artifact lineage regression.
12. SB12 Final red-team and next dispatcher cutline.

## Required Final Evidence

- Full solution build.
- Focused unit tests for new artifact helper/planner/validation services.
- Process-filtered integration tests covering artifact projection and lineage.
- Source scans proving no Process Core/driver-pack project was introduced.
- Source scans proving no MAF product dependency returned.
- Source scans proving no prohibited viewport proof artifacts were created.
- Explicit next-phase cutline.

## Validation Summary

- Bundle preparation status: `Ready`; prepared-stage validation passed after contract repair.
- Bundle readiness gate: `Passed`.
- Execution status: `Completed`.
- Subbundle gate review: `Passed`; all subbundle rows are completed in `reviews/01-execution-report.md`.
- Final closure gate: `Passed`; artifact-backed proof manifests, semantic invariants, source scans, tests, and raw-note closure are recorded.
- Browser validation analytics: `Passed - N/A`; service/runtime work only, no UI files changed, and no prohibited viewport proof artifacts were created.
