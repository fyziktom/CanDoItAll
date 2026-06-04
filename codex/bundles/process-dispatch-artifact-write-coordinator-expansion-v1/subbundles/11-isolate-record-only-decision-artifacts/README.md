# Introduce record-only projection coordinator for completed-decision artifacts

## Status

Prepared.

## Objective

Introduce a record-only helper/coordinator for completed-decision artifacts without forcing storage placement.

## Covered Inputs

- User request to keep dispatcher isolation incremental and avoid Process Core.
- `inputs/01-source-artifacts.md`.
- Current source review in `analysis/01-current-state.md`.

## Prerequisites

SB10 complete.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionWriteCoordinator.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionSourceAdapters.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionPlanner.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`

## Deliverables

- Record-only helper.
- Completed-decision artifact tests.
- Source scan proving no storage placement added.

## Dependency Impact

Prepares Gate C and avoids storage-backed coordinator misuse.

## Validation Depth

- Decision artifact tests.
- Source scan.

## Implementation Steps

- Add narrow record-only request/outcome helper.
- Migrate completed decision record construction.
- Preserve trust/provenance/review summaries.
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

- No managed storage required for record-only decision artifacts.
- External reference key unchanged.
- Candidate state updates preserved.

## Proof Required

- proof/SB11/transcripts/decision-artifact-tests.txt

## Browser Validation Logging

N/A expected. Runtime/service refactor only. If unexpectedly needed, record only large desktop/PC proof and explain why service tests were insufficient.

## Progression Gate

SB12 Gate C may start after this helper lands.

## Suggested Agent Prompt

Implement this subbundle only. Record source assertions, tests, and anti-stub audit before proceeding. Preserve all prior behavior and update `reviews/01-execution-report.md`.
