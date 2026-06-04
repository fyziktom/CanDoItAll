# Classify all artifact projection write paths and side effects

## Status

Prepared.

## Objective

Build a precise inventory of every remaining direct storage placement and artifact record construction path in ArtifactProjection.cs.

## Covered Inputs

- User request to keep dispatcher isolation incremental and avoid Process Core.
- `inputs/01-source-artifacts.md`.
- Current source review in `analysis/01-current-state.md`.

## Prerequisites

SB01 complete.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionWriteCoordinator.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionSourceAdapters.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionPlanner.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`

## Deliverables

- Projection write path matrix updated.
- Side-effect classification for each path.
- Hard/soft failure behavior recorded.

## Dependency Impact

SB03 coordinator changes depend on this inventory.

## Validation Depth

- Source audit from real file.
- No behavior changes.

## Implementation Steps

- Search for storagePlacementService.PlaceAsync and RecordArtifactAsync inside ArtifactProjection.cs.
- Classify caller path and failure behavior.
- Record duplicate state update behavior.

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

- Every direct PlaceAsync call classified.
- Every RecordArtifactAsync call classified.
- Hard vs soft failure recorded.

## Proof Required

- proof/SB02/transcripts/write-path-scan.txt
- inventories/02-projection-write-path-inventory.md

## Browser Validation Logging

N/A expected. Runtime/service refactor only. If unexpectedly needed, record only large desktop/PC proof and explain why service tests were insufficient.

## Progression Gate

SB03 may start only after all write paths are classified.

## Suggested Agent Prompt

Implement this subbundle only. Record source assertions, tests, and anti-stub audit before proceeding. Preserve all prior behavior and update `reviews/01-execution-report.md`.
