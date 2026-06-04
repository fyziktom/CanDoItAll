# Runtime smoke and artifact write regression proof

## Status

- Status: Completed

Completed.

## Objective

Run runtime smoke and artifact write regression proof after all migrations.

## Covered Inputs

- User request to keep dispatcher isolation incremental and avoid Process Core.
- `inputs/01-source-artifacts.md`.
- Current source review in `analysis/01-current-state.md`.

## Prerequisites

- Gate C complete.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionWriteCoordinator.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionSourceAdapters.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionPlanner.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`

## Deliverables

- Focused unit tests.
- Focused integration artifact/projection tests.
- Full solution build.

## Dependency Impact

- Final proof before red-team closure.

## Validation Depth

- Tests and build.
- No prohibited viewport proof scan.

## Implementation Steps

- Run unit architecture tests.
- Run integration artifact/projection slices.
- Run full solution build.
- Record all transcripts.

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

- All selected tests pass.
- Build passes.
- No UI proof required.

## Proof Required

- proof/SB13/transcripts/unit-tests.txt
- proof/SB13/transcripts/integration-tests.txt
- proof/SB13/transcripts/full-build.txt

## Proof Captured

- `bundle://proof/SB13/transcripts/unit-tests.txt`
- `bundle://proof/SB13/transcripts/integration-tests.txt`
- `bundle://proof/SB13/transcripts/full-build.txt`
- `bundle://proof/SB13/source-assertions/runtime-smoke-source-scan.txt`
- `bundle://proof/SB13/source-assertions/anti-stub-audit.txt`
- `bundle://proof/SB13/source-assertions/changed-file-hashes.txt`
- `bundle://proof/SB13/manifest.md`

## Browser Validation Logging

- N/A expected. Runtime/service refactor only. If unexpectedly needed, record only large desktop/PC proof and explain why service tests were insufficient.

## Progression Gate

- SB14 may start after runtime smoke passes or exact blocker is recorded.

## Suggested Agent Prompt

Implement this subbundle only. Record source assertions, tests, and anti-stub audit before proceeding. Preserve all prior behavior and update `reviews/01-execution-report.md`.

