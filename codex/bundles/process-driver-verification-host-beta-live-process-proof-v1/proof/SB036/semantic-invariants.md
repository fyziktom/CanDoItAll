# SB036 Semantic Invariants

## SB036_INV_001 Process Runtime Lifecycle/Outbox/Finalizer Regression
- Source raw note: P12 requires a process runtime regression matrix before downstream release claims.
- Expected behavior: process start persists runtime rows and dispatch outbox records; terminal runs reject late transitions; branch routing skips non-selected paths; repair/recheck/release routing completes through the process-owned runtime.
- Disallowed shallow implementation: start-only tests, report-only lifecycle claims, final status assertions without outbox proof, or completed-run proof that ignores skipped branches and terminal-transition denial.
- Positive proof: `bundle://proof/SB034/transcripts/runtime-lifecycle-outbox-finalizer-regression.txt`.
- Source proof: `bundle://proof/SB034/transcripts/runtime-lifecycle-outbox-finalizer-source-assertions.txt`.
- Red-team negative case: `bundle://proof/SB036/transcripts/red-team-process-runtime-matrix-shallow-proof-rejection.txt`.
- Downstream dependency check: P18 release-candidate validation must include this runtime matrix before final handoff.

## SB036_INV_002 Project-Structure/UI Regression
- Source raw note: SB035 requires project-structure/UI regression coverage for process runtime surfaces.
- Expected behavior: Workbench projects process definitions/runs and process-run output folders, process-bound Workbench nodes complete and roll up parent progress, and project-structure component mutations keep UI state stable without direct process-driver calls.
- Disallowed shallow implementation: static component render proof, a single service projection test, or UI proof that bypasses Workbench/project-structure process ownership.
- Positive proof: `bundle://proof/SB035/transcripts/project-structure-ui-regression.txt`.
- Source proof: `bundle://proof/SB035/transcripts/project-structure-ui-source-assertions.txt`.
- Red-team negative case: `bundle://proof/SB036/transcripts/red-team-process-runtime-matrix-shallow-proof-rejection.txt`.
- Downstream dependency check: operator-smoke phases must keep diagnostics/readback as process-owned surfaces rather than direct driver UI integrations.

## SB036_INV_003 Runtime Boundary Remains Verification-Host Free
- Source raw note: P12 must preserve process-owned runtime launch, outbox, finalizer, project-structure, and UI behavior without smuggling verification-host shortcuts into those layers.
- Expected behavior: Workbench, Projects, AppComponents, component tests, and Playwright tests contain no direct process-driver namespace, verification gateway, verification host, orchestrator, or payload-builder references.
- Disallowed shallow implementation: hidden direct calls from UI/project-structure to drivers, runtime-host references in Workbench, or a matrix that proves only Processes module internals.
- Positive proof: `bundle://proof/SB036/transcripts/gate-l-runtime-boundary-source-scan.txt`.
- Anti-stub audit: `bundle://proof/SB036/transcripts/gate-l-anti-stub-runtime-matrix-audit.txt`.
- Red-team negative case: `bundle://proof/SB036/transcripts/red-team-process-runtime-matrix-shallow-proof-rejection.txt`.
- Downstream dependency check: Core/contract governance in SB037-SB039 must preserve this separation.

## Production Behavior Artifact Matrix
| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Process runtime start/outbox rows | `StartRunAsync_SB018_INV_001_persists_project_context_runtime_rows_and_dispatch_outbox` | Runtime read/query surfaces consume persisted rows | SB034 focused integration transcript | Red-team rejects lifecycle-only proof |
| Branch/finalizer runtime progression | Branch routing and repair workflow tests | Run details and status resolver consume completed/skipped state | SB034 focused integration transcript | Red-team rejects completed-only finalizer claims |
| Workbench/project-structure process projections | `ProjectWorkbenchServiceIntegrationTests` projection tests | Project-structure component mutation tests | SB035 focused transcript | Red-team rejects static UI-only proof |
| Project/workbench/UI boundary scan | Gate L source scan | Downstream operator-smoke and docs phases | Source scan covers Workbench, Projects, AppComponents, component tests, and Playwright tests | Anti-stub audit classifies placeholder tokens |

## Gate Result
Gate L is semantically adequate for P12. Runtime lifecycle/outbox/finalizer behavior and project-structure/UI behavior are covered by focused tests, and project/workbench/UI surfaces do not call process drivers or verification hosts directly.
