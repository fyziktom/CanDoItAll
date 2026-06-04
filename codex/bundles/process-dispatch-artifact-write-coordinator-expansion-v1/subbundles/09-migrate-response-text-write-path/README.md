# Migrate response-text artifact write path through coordinator

## Status

Prepared.

## Objective

Migrate response-text artifact storage/write/record path through the coordinator while preserving file content creation and path safety behavior.

## Covered Inputs

- User request to keep dispatcher isolation incremental and avoid Process Core.
- `inputs/01-source-artifacts.md`.
- Current source review in `analysis/01-current-state.md`.

## Prerequisites

Gate B complete.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionWriteCoordinator.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionSourceAdapters.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionPlanner.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`

## Deliverables

- Response text storage/record call uses coordinator.
- File creation remains dispatcher-owned.
- Content/newline/path-safety tests.

## Dependency Impact

High-risk special path; blocks provider-native migration.

## Validation Depth

- Response text projection tests.
- Path traversal/source scan.

## Implementation Steps

- Keep target path validation and File.WriteAllTextAsync outside coordinator.
- Use coordinator for storage placement and record.
- Preserve existing-managed short-circuit path.
- Add tests for newline/content.

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

- Response file contents unchanged.
- Outside-workspace guard preserved.
- Existing-file short-circuit preserved.

## Proof Required

- proof/SB09/transcripts/response-text-tests.txt

## Browser Validation Logging

N/A expected. Runtime/service refactor only. If unexpectedly needed, record only large desktop/PC proof and explain why service tests were insufficient.

## Progression Gate

SB10 may start only after response-text tests pass.

## Suggested Agent Prompt

Implement this subbundle only. Record source assertions, tests, and anti-stub audit before proceeding. Preserve all prior behavior and update `reviews/01-execution-report.md`.
