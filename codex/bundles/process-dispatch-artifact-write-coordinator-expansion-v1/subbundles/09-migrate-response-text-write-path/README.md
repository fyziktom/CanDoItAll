# Migrate response-text artifact write path through coordinator

## Status

- Status: Completed

Completed.

## Objective

Migrate response-text artifact storage/write/record path through the coordinator while preserving file content creation and path safety behavior.

## Covered Inputs

- User request to keep dispatcher isolation incremental and avoid Process Core.
- `inputs/01-source-artifacts.md`.
- Current source review in `analysis/01-current-state.md`.

## Prerequisites

- Gate B complete.

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

- High-risk special path; blocks provider-native migration.

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

- Response file contents unchanged; newline normalization, UTF-8 file write, and persisted bytes remain dispatcher-owned.
- Outside-workspace guard preserved with `IsWithinWorkspace` before file creation.
- Existing-file short-circuit preserved; existing-managed response-target helper logs coordinator failures and returns `false`.

## Proof Required

- proof/SB09/transcripts/response-text-tests.txt
- proof/SB09/transcripts/failing-first-response-text-source-guard.txt
- proof/SB09/source-assertions/response-text-source-scan.txt
- proof/SB09/source-assertions/anti-stub-audit.txt
- proof/SB09/source-assertions/changed-file-hashes.txt
- proof/SB09/semantic-invariants.md
- proof/SB09/manifest.md

## Completion Notes

- Migrated response-text storage placement and artifact recording through `ProcessArtifactProjectionWriteCoordinator`.
- Migrated the response existing-managed short-circuit helper through the coordinator without changing its soft failure/continue behavior.
- Added a shared expected-artifact projection write outcome applicator for soft projection paths.
- Full `dotnet build CanDoItAll.slnx --no-restore -v:minimal` passed.

## Browser Validation Logging

- N/A expected. Runtime/service refactor only. If unexpectedly needed, record only large desktop/PC proof and explain why service tests were insufficient.

## Progression Gate

- SB10 may start only after response-text tests pass.

## Suggested Agent Prompt

Implement this subbundle only. Record source assertions, tests, and anti-stub audit before proceeding. Preserve all prior behavior and update `reviews/01-execution-report.md`.

