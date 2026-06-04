# Migrate process-mock artifact write path through coordinator

## Status

- Status: Completed

Completed.

- Entry gate: Passed on 2026-06-04. Gate A closure proof exists and SB03 coordinator proof was rechecked by SB04.
- Closure gate: Passed on 2026-06-04. Critical proof manifest and semantic invariants exist under `proof/SB05`; process-mock focused tests passed; source scan proves no direct storage placement or artifact record call remains in `ProjectProcessMockArtifactsAsync`.

## Objective

Migrate process mock artifact storage/write/record path through the write coordinator while preserving hard failure behavior.

## Covered Inputs

- User request to keep dispatcher isolation incremental and avoid Process Core.
- `inputs/01-source-artifacts.md`.
- Current source review in `analysis/01-current-state.md`.

## Prerequisites

- Gate A complete.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionWriteCoordinator.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionSourceAdapters.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionPlanner.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`

## Deliverables

- Process mock path uses coordinator.
- Hard failure behavior tests.
- Key/lineage parity tests.

## Dependency Impact

- Validates coordinator can support required deterministic artifacts.

## Validation Depth

- Focused process mock projection tests.
- Source scan proving no direct PlaceAsync remains in process mock section.

## Implementation Steps

- Replace direct placement/record block with coordinator request.
- Keep file read/path resolution in dispatcher.
- Preserve throw-on-failure behavior.
- Add tests.

## Scope Exceptions

- No Process Core.
- No driver packs.
- No UI proof unless an unexpected UI file change occurs, and then large desktop/PC only.

## Do Not Do

- Do not rename public process tools.
- Do not change external-reference key formats without explicit parity proof.
- Do not move source matching semantics into the write coordinator.
- Do not run small/medium/mobile proof.

## Acceptance Checklist

- Process mock external key unchanged.
- Candidate state updates preserved.
- Hard failure remains hard.

## Proof Required

- proof/SB05/manifest.md
- proof/SB05/semantic-invariants.md
- proof/SB05/transcripts/process-mock-tests.txt
- proof/SB05/transcripts/failing-first-process-mock-source-guard.txt
- proof/SB05/source-assertions/process-mock-source-scan.txt
- proof/SB05/source-assertions/changed-file-hashes.txt

## Browser Validation Logging

- N/A expected. Runtime/service refactor only. If unexpectedly needed, record only large desktop/PC proof and explain why service tests were insufficient.

## Progression Gate

- SB06 may start only after process mock tests pass.

## Suggested Agent Prompt

Implement this subbundle only. Record source assertions, tests, and anti-stub audit before proceeding. Preserve all prior behavior and update `reviews/01-execution-report.md`.

