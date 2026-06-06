# process-dispatch-artifact-projection-split-dependency-boundary-v1

Status: Prepared for Codex implementation.

## Mission

Split the transitional nested artifact projection coordinator boundary into real module-local internal coordinator classes and narrow dependencies before any Process Core extraction.

## Why this exists

The previous bundle correctly introduced a projection coordinator boundary, but it remains nested inside `ProcessRunAutomationDispatchService`. That keeps the actual dependency surface hidden and makes a later Process Core or driver-pack boundary harder.

## Hard constraints

- Do not create Process Core.
- Do not create production process-driver APIs.
- Do not change projection behavior or source-family order.
- Do not remove existing functionality.
- Do not touch UI files.
- Do not produce small/medium/mobile proof artifacts.
- Keep driver readiness documentation-only.

## Current source references

- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjectionCoordinators.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionPlanner.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionWriteCoordinator.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionLineageBuilder.cs`
- `tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- `tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Bundle structure

This is an initiative-profile bundle with 64 subbundles and critical gates after repeated movement phases.

Start with `plan/01-phase-plan.md`.
