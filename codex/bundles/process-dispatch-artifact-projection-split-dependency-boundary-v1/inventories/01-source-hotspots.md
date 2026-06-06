# Source Hotspots

| File | Current role | Next action |
| --- | --- | --- |
| `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs` | Projection facade plus compatibility wrappers; currently creates all nested coordinators in order | Keep as thin facade and remove broad coordinator construction details |
| `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjectionCoordinators.cs` | Transitional nested coordinator boundary; private nested classes and coordinator context | Split into top-level module-local classes |
| `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionPlanner.cs` | Pure projection plan helper | Reuse; do not broaden into driver API |
| `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionWriteCoordinator.cs` | Storage-backed write coordinator | Reuse as explicit side-effect boundary |
| `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionLineageBuilder.cs` | Recovery/rework projection lineage | Reuse and keep behavior identical |
| `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactValidationSnapshot.cs` | Validation/projection expectation conversion | Reuse; do not move to public contracts |
| `tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs` | Architecture guardrails | Extend for top-level coordinator and no broad dependency checks |
| `tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs` | Existing process dispatch integration tests | Use focused projection filters |
| `tests/CanDoItAll.Tests.Integration/ProcessAutomationObservationTests.cs` | Observation/outcome tests | Include in broad smoke if impacted |
