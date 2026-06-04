# Migrate workspace-written artifact write path through coordinator

## Status

- Status: Completed

Completed.

## Objective

Migrate workspace-written artifact storage/write/record path through the write coordinator while preserving path matching and soft failure behavior.

## Covered Inputs

- User request to keep dispatcher isolation incremental and avoid Process Core.
- `inputs/01-source-artifacts.md`.
- Current source review in `analysis/01-current-state.md`.

## Prerequisites

- SB05 complete.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionWriteCoordinator.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionSourceAdapters.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionPlanner.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`

## Deliverables

- Workspace-written path uses coordinator.
- Path matching tests.
- Soft failure logging preserved.

## Dependency Impact

- Prepares migration of other storage-backed paths.

## Validation Depth

- Workspace-written projection tests.
- Source scan.

## Implementation Steps

- Use existing source adapter plan.
- Replace placement/record block with coordinator call.
- Keep source path resolution in dispatcher.
- Preserve warnings/continue semantics.

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

- SourceRelativePath/ProjectedRelativePath behavior unchanged; dispatcher still resolves source full paths before write coordination.
- Duplicate detection unchanged; external-reference probe still uses the workspace-written source adapter.
- Candidate state updates preserved; successful coordinator outcome updates `ExternalReferenceKeys` and `RecordedArtifactExpectationIds`.

## Proof Required

- proof/SB06/transcripts/workspace-written-tests.txt
- proof/SB06/transcripts/failing-first-workspace-written-source-guard.txt
- proof/SB06/source-assertions/workspace-written-source-scan.txt
- proof/SB06/source-assertions/changed-file-hashes.txt

## Completion Notes

- Migrated `ProjectWorkspaceWrittenArtifactsAsync` to accept the existing write coordinator and call `WriteAsync`.
- Removed the direct `storagePlacementService.PlaceAsync` and `RecordArtifactAsync` block from the workspace-written method.
- Preserved soft failure behavior with warning logs and continue semantics for coordinator/recording failures.

## Browser Validation Logging

- N/A expected. Runtime/service refactor only. If unexpectedly needed, record only large desktop/PC proof and explain why service tests were insufficient.

## Progression Gate

- SB07 may start only after workspace-written parity passes.

## Suggested Agent Prompt

Implement this subbundle only. Record source assertions, tests, and anti-stub audit before proceeding. Preserve all prior behavior and update `reviews/01-execution-report.md`.

