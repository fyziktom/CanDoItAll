# Migrate provider-native browser artifact write paths through coordinator

## Status

Prepared.

## Objective

Migrate provider-native browser expected/discovered artifact write paths through the coordinator without collapsing their semantics.

## Covered Inputs

- User request to keep dispatcher isolation incremental and avoid Process Core.
- `inputs/01-source-artifacts.md`.
- Current source review in `analysis/01-current-state.md`.

## Prerequisites

SB09 complete.

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

Completes storage-backed projection migration.

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

- Expected/discovered modes remain distinct.
- Key and lineage unchanged.
- Trust behavior unchanged.

## Proof Required

- proof/SB10/transcripts/provider-native-browser-tests.txt

## Browser Validation Logging

N/A expected. Runtime/service refactor only. If unexpectedly needed, record only large desktop/PC proof and explain why service tests were insufficient.

## Progression Gate

SB11 may start only after provider-native tests pass.

## Suggested Agent Prompt

Implement this subbundle only. Record source assertions, tests, and anti-stub audit before proceeding. Preserve all prior behavior and update `reviews/01-execution-report.md`.
