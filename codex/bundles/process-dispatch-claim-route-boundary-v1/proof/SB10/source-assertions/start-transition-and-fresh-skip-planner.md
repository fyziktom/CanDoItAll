# SB10 Source Assertions

- Added `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchStartTransitionPlanner.cs` as a module-local planner for start-transition request construction and route-snapshot fresh recovery skip decisions.
- `BuildStartTransitionRequest` preserves the existing dispatcher request fields: step run id, step run concurrency token, `InProgress` target status, durable dispatcher reason, `AutomationActor`, and `SuppressAutomationDispatch = true`.
- `ShouldSkipFreshAutomationDispatch` delegates to `ProcessAutomationExecutionRunSelection.ShouldSkipFreshAutomationDispatch` using the existing route snapshot facts and configured grace-period wrapper.
- `DispatchAsync` still owns `TransitionStepWithClaimAsync`, reload-after-failed-start behavior, logging, workflow execution, subprocess handling, and agent execution side effects.
- `ProcessRunAutomationDispatchService.ShouldSkipFreshAutomationDispatch(ProcessDispatchRouteSnapshot, DateTimeOffset)` preserves service-wrapper compatibility for route-snapshot callers.
- Focused tests cover start-transition request field parity and fresh-skip wrapper parity.
- No Process Core, production process driver API, UI, or viewport proof artifacts were introduced.
