# Refactor Gate B: storage/write parity and line-count review

## Status

- Status: Completed

Completed.

## Objective

Refactor Gate B: prove first batch of storage-backed migrations is safe and line-count/source shape improves or is justified.

## Covered Inputs

- User request to keep dispatcher isolation incremental and avoid Process Core.
- `inputs/01-source-artifacts.md`.
- Current source review in `analysis/01-current-state.md`.

## Prerequisites

- SB05-SB07 complete.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionWriteCoordinator.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionSourceAdapters.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionPlanner.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`

## Deliverables

- Gate B source scans.
- Line-count review.
- Focused integration slice.

## Dependency Impact

- Blocks response/provider/native migrations.

## Validation Depth

- Focused artifact/projection integration tests.
- Full build if needed.

## Implementation Steps

- Run artifact/projection test slices.
- Run source scans for direct PlaceAsync/RecordArtifactAsync in migrated sections.
- Record line counts and risks.

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

- Gate B passed.
- No migration regressions; architecture guards, coordinator tests, migrated artifact projection parity tests, and full solution build passed.
- No prohibited proof paths.

## Proof Required

- proof/SB08/transcripts/gate-b-tests.txt
- proof/SB08/source-assertions/line-counts.txt
- proof/SB08/source-assertions/gate-b-source-scan.txt
- proof/SB08/source-assertions/anti-stub-audit.txt
- proof/SB08/source-assertions/changed-file-hashes.txt
- proof/SB08/manifest.md

## Completion Notes

- Re-scanned process mock, workspace-written, and existing-managed sections and confirmed each uses `writeCoordinator.WriteAsync` without direct placement/record calls.
- Recorded line counts; `ArtifactProjection.cs` is 1506 lines versus the SB04 1526-line baseline.
- Full `dotnet build CanDoItAll.slnx --no-restore -v:minimal` passed.

## Browser Validation Logging

- N/A expected. Runtime/service refactor only. If unexpectedly needed, record only large desktop/PC proof and explain why service tests were insufficient.

## Progression Gate

- SB09 may start only after Gate B.

## Suggested Agent Prompt

Implement this subbundle only. Record source assertions, tests, and anti-stub audit before proceeding. Preserve all prior behavior and update `reviews/01-execution-report.md`.

