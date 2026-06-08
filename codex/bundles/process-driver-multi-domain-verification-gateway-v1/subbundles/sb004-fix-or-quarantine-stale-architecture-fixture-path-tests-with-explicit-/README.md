# SB004 — Fix or quarantine stale architecture fixture path tests with explicit current-bundle fixture ownership.

## Status
- Status: `Completed`
- Entry gate: `Passed`
- Closure gate: `Passed`
- Completed: `2026-06-08`

## Objective
Fix or quarantine stale architecture fixture path tests with explicit current-bundle fixture ownership.

## Covered Inputs
- Raw user request in bundle://inputs/raw-request.md.
- Current-state analysis in bundle://analysis/01-current-state-review.md.
- Normalized requirements in bundle://requirements/01-normalized-requirements.md.

## Prerequisites
- Complete all prior subbundles in plan order.
- If this is a phase gate, all phase-owned source scans and focused tests must pass before downstream work continues.

## Exact Source References
- repo://src/CanDoItAll.Processes.Core
- repo://src/CanDoItAll.Processes.Drivers.Abstractions
- repo://src/CanDoItAll.Processes.Drivers.TranscriptVerification
- repo://src/CanDoItAll.Processes.Drivers.RuntimeEvidence
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch
- repo://tests/CanDoItAll.Tests.Unit
- repo://tests/CanDoItAll.Tests.Integration

## Deliverables
- Source changes matching the objective, or documentation/test changes when explicitly scoped.
- Updated tests and architecture guards.
- Updated proof artifacts under `proof/SB004/` if this is a critical gate.

## Dependency Impact
- Downstream phases assume this subbundle preserved read-only driver semantics, Core dependency cleanliness, and no runtime host/registry/selector behavior.

## Validation Depth
- Build and relevant focused tests.
- Source scans for forbidden runtime/Core/driver/UI/stub drift.
- Architecture tests for package/dependency boundaries.
- Standard source-backed closure proof.

## Implementation Steps
1. Re-read current source references before editing.
2. Implement only the named coherent slice; do not opportunistically add runtime host behavior.
3. Update or add tests first when behavior is new.
4. Run focused proof before moving to the next subbundle.
5. Update reviews/01-execution-report.md row for this subbundle.

## Scope Exceptions
- Runtime host/registry/selector/DI/manager/scheduler/workflow integration remains out of scope.
- Execution-capable drivers remain out of scope.
- UI/browser/mobile proof remains out of scope unless UI/media drift occurs, which should fail the bundle.

## Do Not Do
- Do not add generic runtime driver discovery.
- Do not register drivers in DI.
- Do not add manager commands.
- Do not read arbitrary files or call external systems.
- Do not mutate process state, claims, transitions, finalizers, retries, workspace, or storage.
- Do not add UI/mobile screenshots.

## Acceptance Checklist
- [x] Objective implemented.
- [x] Tests added/updated.
- [x] No forbidden dependency/runtime tokens.
- [x] No UI/media drift.
- [x] Existing behavior preserved.


## Proof Required
- Focused architecture test transcript: `bundle://proof/SB004/transcripts/architecture-fixture-quarantine-focused-tests.txt`.
- Broad unit transcript excluding only remaining TuningRequest debt: `bundle://proof/SB004/transcripts/unit-tests-excluding-tuningrequest-after-fixture-quarantine.txt`.
- Source/skip/no-drift audit: `bundle://proof/SB004/transcripts/source-skip-and-no-drift-audit.txt`.
- Subbundle proof manifest: `bundle://proof/SB004/manifest.md`.
- Subbundle row in execution report: `bundle://reviews/01-execution-report.md`.

## Browser Validation Logging
- N/A — runtime/service/Core/driver work only. If UI/media files change unexpectedly, fail and re-scope.

## Progression Gate
- Noncritical: downstream work may continue only if focused proof is green.

## Suggested Agent Prompt
Implement SB004: Fix or quarantine stale architecture fixture path tests with explicit current-bundle fixture ownership.. Preserve all hard constraints from bundle://requirements/02-hard-constraints.md and close proof in reviews/01-execution-report.md.
