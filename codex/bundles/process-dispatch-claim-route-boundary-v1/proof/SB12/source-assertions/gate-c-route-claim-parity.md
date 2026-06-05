# SB12 Source Assertions

- Gate C verifies the route, claim, heartbeat, start-transition, and execution-selection helper boundaries after SB09-SB11 movement.
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRoutePlanner.cs` returns explicit route decisions for database requirement, upstream materialization, stranded recovery, subprocess, workflow, and direct agent execution.
- `ProcessDispatchRoutePlanner` is decision-only. The Gate C scan rejects EF, workflow, subprocess, execution-client, transition, finalizer, logging, and service-scope side-effect tokens in the planner.
- `DispatchAsync` remains the side-effect owner for durable claim acquisition/release, heartbeat startup, database blocking, missing upstream materialization, stranded recovery, subprocess handling, start transition execution, workflow coordinator calls, direct agent execution, finalizer calls, and failure transitions.
- Claim/heartbeat behavior remains explicit through `ProcessDispatchGuardLease`, `ProcessDispatchLeaseHeartbeat`, durable claim methods, and `ProcessDispatchClaimLostException`.
- Start-transition request construction remains pure through `ProcessDispatchStartTransitionPlanner.BuildStartTransitionRequest`; transition execution and failed-start reload behavior remain in `DispatchAsync`.
- Line-count gate results: `Dispatch.cs` 2038 lines, `Concurrency.cs` 1414 lines, `StepCompletionFinalizer.cs` 1433 lines, `ProcessDispatchRoutePlanner.cs` 69 lines.
- Focused architecture and integration proof passed; failing-first proof against `HEAD` showed the gate rejects the pre-route-planner source shape.
- No Process Core, production process driver API, UI, or small/medium/mobile proof artifacts were introduced.
