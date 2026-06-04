# Migrate provider-native browser artifact write paths through coordinator

## Status

- Status: Completed

Completed.

## Objective

Migrate provider-native browser expected/discovered artifact write paths through the coordinator without collapsing their semantics.

## Covered Inputs

- User request to keep dispatcher isolation incremental and avoid Process Core.
- `inputs/01-source-artifacts.md`.
- Current source review in `analysis/01-current-state.md`.

## Prerequisites

- SB09 complete.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionWriteCoordinator.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionSourceAdapters.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionPlanner.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`

## Deliverables

- Expected provider-native path uses coordinator.
- Discovered provider-native path uses coordinator.
- Mode-specific tests.

## Dependency Impact

- Completes storage-backed projection migration.

## Validation Depth

- Provider-native browser artifact tests.
- Source scan.

## Implementation Steps

- Identify expected-output and discovered-output blocks.
- Use appropriate adapter Plan methods.
- Keep source output discovery in dispatcher.
- Preserve produced-by tool names.

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

- Expected/discovered modes remain distinct; expected uses `PlanExpectedOutput`, discovered uses `PlanDiscoveredOutput`.
- Key and lineage unchanged through existing provider-native source adapter planning.
- Trust behavior unchanged through mode-specific projection plans.

## Proof Required

- proof/SB10/transcripts/provider-native-browser-tests.txt
- proof/SB10/transcripts/failing-first-provider-native-browser-source-guard.txt
- proof/SB10/source-assertions/provider-native-browser-source-scan.txt
- proof/SB10/source-assertions/anti-stub-audit.txt
- proof/SB10/source-assertions/changed-file-hashes.txt
- proof/SB10/semantic-invariants.md
- proof/SB10/manifest.md

## Completion Notes

- Migrated expected provider-native browser artifacts through `ProcessArtifactProjectionWriteCoordinator`.
- Migrated discovered provider-native browser outputs through `ProcessArtifactProjectionWriteCoordinator`.
- Kept output discovery, path safety, file copy, expected/discovered planning, and optional expectation matching outside the coordinator.
- Full `dotnet build CanDoItAll.slnx --no-restore -v:minimal` passed.

## Browser Validation Logging

- N/A expected. Runtime/service refactor only. If unexpectedly needed, record only large desktop/PC proof and explain why service tests were insufficient.

## Progression Gate

- SB11 may start only after provider-native tests pass.

## Suggested Agent Prompt

Implement this subbundle only. Record source assertions, tests, and anti-stub audit before proceeding. Preserve all prior behavior and update `reviews/01-execution-report.md`.

