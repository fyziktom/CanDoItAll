# Introduce record-only projection coordinator for completed-decision artifacts

## Status

- Status: Completed

Completed.

## Objective

Introduce a record-only helper/coordinator for completed-decision artifacts without forcing storage placement.

## Covered Inputs

- User request to keep dispatcher isolation incremental and avoid Process Core.
- `inputs/01-source-artifacts.md`.
- Current source review in `analysis/01-current-state.md`.

## Prerequisites

- SB10 complete.

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

- Prepares Gate C and avoids storage-backed coordinator misuse.

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

- No managed storage required for record-only decision artifacts; record-only coordinator has no storage placement dependency.
- External reference key unchanged; focused integration test covers the `process-step-decision:{stepRunId}:{artifactExpectationId}` format.
- Candidate state updates preserved from record-only coordinator outcome.

## Proof Required

- proof/SB11/transcripts/decision-artifact-tests.txt
- proof/SB11/transcripts/failing-first-decision-record-only-source-guard.txt
- proof/SB11/source-assertions/decision-record-only-source-scan.txt
- proof/SB11/source-assertions/anti-stub-audit.txt
- proof/SB11/source-assertions/changed-file-hashes.txt

## Completion Notes

- Added `ProcessArtifactProjectionRecordOnlyCoordinator` and typed record-only request/result records.
- Migrated completed-decision artifact recording through the record-only coordinator without adding managed storage.
- Added focused tests for decision external-reference key and trust mapping.
- Full `dotnet build CanDoItAll.slnx --no-restore -v:minimal` passed.

## Browser Validation Logging

- N/A expected. Runtime/service refactor only. If unexpectedly needed, record only large desktop/PC proof and explain why service tests were insufficient.

## Progression Gate

- SB12 Gate C may start after this helper lands.

## Suggested Agent Prompt

Implement this subbundle only. Record source assertions, tests, and anti-stub audit before proceeding. Preserve all prior behavior and update `reviews/01-execution-report.md`.

