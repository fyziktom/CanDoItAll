# Source Hotspots

| Area | Current file | Why risky | Desired boundary |
| --- | --- | --- | --- |
| Main loop | `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs` | Owns all route sequencing and exception handling | `ProcessDispatchLoopCoordinator` / route pipeline |
| Durable claim EF writes | same file | Claim, renew, held, release direct EF operations | `ProcessDispatchClaimStore` |
| Heartbeat | same file + existing heartbeat type | Lifetime and cancellation semantics are tightly coupled to loop | `ProcessDispatchHeartbeatCoordinator` |
| Failure closure | same file | Catch blocks duplicate claim-lost and transition semantics | `ProcessDispatchExceptionClosureCoordinator` |
| Start transition | same file | Start transition reload behavior is inline | `ProcessDispatchStartRouteHandler` |
| Workflow route | same file | Workflow outcome finalization inline | `ProcessDispatchWorkflowRouteHandler` |
| Direct agent route | same file + Execution.cs | Direct execution/finalizer path inline | `ProcessDispatchDirectAgentRouteHandler` |
| Run closed guard | same file | DB guard inline | `ProcessDispatchRunClosureGuard` |
| Existing projection | projection files | Now mostly isolated; must not regress | Source scans prevent regression |
