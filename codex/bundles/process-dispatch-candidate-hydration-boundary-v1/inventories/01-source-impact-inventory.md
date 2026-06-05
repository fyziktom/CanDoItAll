# Source Impact Inventory

| File | Current role | Planned movement |
| --- | --- | --- |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs` | Main lifecycle route and candidate hydration | Reduce hydration section by extracting local selector/loader/assembler/coordinator helpers. |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Concurrency.cs` | Selection wrappers around extracted run-selection helper | Preserve; only smoke if candidate hydration uses these facts. |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteSnapshot.cs` | Existing route facts | Reuse in candidate header/hydration tests. |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRoutePlanner.cs` | Existing route decisions | Preserve; do not expand into full state machine. |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchStartTransitionPlanner.cs` | Existing start transition builder | Preserve. |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.FinalizerContextFactory.cs` | Existing finalizer context factory | Preserve. |
| `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs` | Guardrail tests | Extend for candidate helper boundaries. |
| `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs` | Dispatch behavior proof | Add focused candidate selection/hydration slices. |
