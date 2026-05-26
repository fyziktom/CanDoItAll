# SB11: Refactor recovery, block state, and health diagnostics.

## Objective

Refactor recovery, block state, and health diagnostics.

## Why This Matters

This subbundle closes a concrete runtime correctness gap observed after phase5. The process runtime must avoid both false completion and unnecessary blocking while staying generic.

## Implementation Tasks

- Extract `ProcessBlockStateClassifier`.
- Extract `ProcessRecoveryRouter` if not already isolated.
- Extract `ProcessHealthInvariantAuditor`.
- Extract `WorkflowSubprocessArtifactMapper`.
- Ensure no single process dispatch partial class grows with new recovery logic.

## Required Tests

- Add failing-first or red-team tests before the production fix where practical.
- Add positive tests proving the fixed behavior.
- Include at least one generic/non-software case if this subbundle changes generic process semantics.

## Closure Criteria

- Production code implements the behavior; no prompt-only fix.
- Proof manifest is updated.
- Focused tests pass.
- No SQLite runtime/migration dependency is introduced.

## Status

- Completed

## Covered Inputs

- RN06 infer wrong block/recovery classification from broad reason text.
- RN07 route workflow/subprocess artifacts heuristically instead of explicitly.
- RN09 add refactoring checkpoints every few subbundles.
- RQ07, RQ10, RQ11.

## Prerequisites

- SB09 and SB10 closure gates pass.
- Checkpoint B proof remains trusted.

## Exact Source References

- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessStepRunBlockState.cs
- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.RuntimeReadQuery.Support.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessWorkflowRunCoordinator.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs
- repo://codex/bundles/processes-hardening-followup-runtime-correctness-v6/architecture/02-refactoring-checkpoints.md

## Deliverables

- Extracted block state classifier, recovery router, health invariant auditor, and workflow/subprocess artifact mapper where needed.
- Runtime services testable without full dispatch orchestration.
- Architecture note update for checkpoint C.

## Dependency Impact

- SB12 strictness and SB13 diagnostics rely on stable recovery/health service boundaries.
- SB14 final closure depends on these services being directly testable.

## Validation Depth

- Focused tests for extracted classifiers/router/mapper/auditor.
- Source assertions proving production call paths use the extracted services.
- Anti-stub audit for unused extraction or lingering duplicated heuristics.

## Implementation Steps

- Extract cohesive recovery, block-state, health, and mapping logic.
- Redirect production runtime paths through extracted services.
- Add or adjust direct service tests.
- Update architecture/refactoring checkpoint notes.
- Record proof under `bundle://proof/SB11/`.

## Do Not Do

- Do not move code without preserving behavior tests.
- Do not leave new recovery logic inside large dispatch partials.
- Do not add service layers that hide string heuristics instead of removing them.

## Acceptance Checklist

- [x] Recovery and health behavior is modular and directly testable.
- [x] Focused tests pass after extraction.
- [x] Architecture notes describe the new boundaries.
- [x] SB12/SB13 can proceed without depending on monolithic partial internals.

## Closure Notes

- Added `ProcessBlockStateClassifier`, `ProcessHealthInvariantAuditor`, and `WorkflowSubprocessArtifactMapper`.
- Redirected production block-state, health read-query, workflow projection, and subprocess projection paths through the extracted services.
- Updated checkpoint C architecture notes.
- Focused SB11 direct service tests plus SB09/SB10 regression slice passed.

## Proof Required

- `bundle://proof/SB11/manifest.md`
- `bundle://proof/SB11/semantic-invariants.md`
- Passing focused test transcript.
- Source assertion transcript.
- Changed-file SHA-256 transcript.
- Anti-stub audit transcript.

## Browser Validation Logging

- N/A: SB11 is a non-UI refactoring checkpoint.

## Progression Gate

- SB12 may start only after checkpoint C proves recovery/health/mapping extraction did not weaken SB09/SB10 behavior.

## Suggested Agent Prompt

- Execute checkpoint C with minimal extraction, update architecture proof, rerun focused tests, and record SB11 gate closure.
