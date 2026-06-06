# Implementation Agent Prompt

You are implementing `process-dispatch-artifact-projection-split-dependency-boundary-v1` on branch `maf-processes-refactor`.

Do not implement Process Core. Do not add production process-driver APIs. Do not touch UI files.

Your job is to split the transitional nested artifact projection coordinator boundary into top-level module-local internal coordinator classes and narrow their dependencies.

Execute SB01-SB64 in order. Do not skip critical gates.

Key files:
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjectionCoordinators.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionPlanner.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionWriteCoordinator.cs`
- `tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- `tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

Every production movement must preserve behavior and be backed by focused tests and source scans.