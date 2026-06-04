# Migrate existing-managed artifact write path through coordinator

## Status

Prepared.

## Objective

Migrate existing-managed artifact storage/write/record path through the write coordinator while preserving duplicate detection and managed path behavior.

## Covered Inputs

- User request to keep dispatcher isolation incremental and avoid Process Core.
- `inputs/01-source-artifacts.md`.
- Current source review in `analysis/01-current-state.md`.

## Prerequisites

SB06 complete.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionWriteCoordinator.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionSourceAdapters.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionPlanner.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`

## Deliverables

- Existing-managed path uses coordinator.
- Duplicate detection tests.
- Managed path tests.

## Dependency Impact

Completes the first batch of storage-backed migrations.

## Validation Depth

- Existing-managed artifact tests.
- Source scan.

## Implementation Steps

- Replace direct storage/record block.
- Preserve ExistingManagedArtifactFileMatches logic outside coordinator.
- Add parity tests.

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

- Duplicate behavior unchanged.
- Key and lineage unchanged.
- Candidate state updates preserved.

## Proof Required

- proof/SB07/transcripts/existing-managed-tests.txt

## Browser Validation Logging

N/A expected. Runtime/service refactor only. If unexpectedly needed, record only large desktop/PC proof and explain why service tests were insufficient.

## Progression Gate

SB08 Gate B may start after this migration.

## Suggested Agent Prompt

Implement this subbundle only. Record source assertions, tests, and anti-stub audit before proceeding. Preserve all prior behavior and update `reviews/01-execution-report.md`.
