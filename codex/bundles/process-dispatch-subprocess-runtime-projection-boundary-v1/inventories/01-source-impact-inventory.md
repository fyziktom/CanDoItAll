# Source Impact Inventory

Primary files:

| File | Final role |
| --- | --- |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs` | Orchestrates subprocess dispatch and projection through helper/coordinator boundaries. Current line count: 1261. HEAD baseline line count: 1476. Delta: -215. |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Models.cs` | Existing dispatch model partial; nested subprocess capability-gap facts moved out. |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessLifecycleRules.cs` | Builds subprocess start, block, and terminal transition requests plus parent status/reason mapping. |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessRunObservationCoordinator.cs` | Explicit service-scope coordinator for `EnsureSubprocessRunForStepAsync`. |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessCapabilityGapInspector.cs` | Queries child step role/capability gaps and formats the parent block reason. |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessArtifactSourceResolver.cs` | Delegates subprocess source/mapping/eligibility decisions to the existing artifact mapper. |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessProjectionPlanBuilder.cs` | Pure projection plan builder for parent-scoped subprocess artifact markdown metadata and expectation matching. |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessProjectionGapJournalCoordinator.cs` | Explicit EF coordinator for projection-gap journaling without saving. |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessProjectionWriterCoordinator.cs` | Explicit workspace file-write and EF coordinator for parent-scoped projected artifacts without saving. |
| `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs` | Focused parity and boundary source-scan tests for lifecycle rules, capability-gap formatting, mapper behavior, and subprocess dispatch delegation. |
| `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs` | Existing architecture guardrail that still rejects premature Process Core/driver-pack projects. |

Final method/class movement:

- `HandleSubprocessDispatchAsync` now delegates lifecycle, runtime observation, capability-gap, terminal mirror, and projection responsibilities.
- `ProjectCompletedSubprocessArtifactsAsync` now delegates source resolution, projection planning, gap journaling, and write coordination while preserving claim checks and final save timing.
- `ResolveSubprocessSourceArtifact` and `ResolveSubprocessOutputArtifactMappings` wrappers remain in dispatch and delegate to `ProcessSubprocessArtifactSourceResolver` for compatibility.