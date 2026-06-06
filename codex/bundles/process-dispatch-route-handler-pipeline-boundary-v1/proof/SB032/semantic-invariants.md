# SB032 Semantic Invariants

- Invariant ID: `SB032_INV_001`
- Source raw note: `bundle://inputs/00-original-request.md` asks to continue smaller dispatcher isolation without rushing Process Core, while preserving original functionality.
- Expected behavior: `ExecuteClaimedDispatchRouteAsync` hydrates the claimed candidate and delegates route-stage decisions to explicit module-local handlers in the exact `ProcessDispatchRoutePipeline.StageOrder` order.
- Disallowed shallow implementation: Empty or wrapper-only handler classes while fresh recovery skip, database requirement, upstream materialization, stranded recovery, subprocess, start transition, workflow, direct-agent execution, competing guard, run-closed guard, or finalizer decisions remain inline in the route execution body.
- Failing-first test: `bundle://proof/transcripts/failing-first-route-handler-boundary.txt` returns `ExitCode: 1` against `HEAD` because the pre-refactor route body lacks `CreateClaimedDispatchRouteHandlerPipeline`.
- Passing test: `bundle://proof/transcripts/unit-route-boundary-tests.txt` and `bundle://proof/transcripts/integration-route-boundary-tests.txt` return `ExitCode: 0`.
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RouteExecution.cs, repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RouteHandlers.cs, repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRoutePipeline.cs, repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs, repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs.
- Production assertions: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RouteHandlers.cs` contains the stage handlers and `ProcessDispatchRouteOrderAssertion`; `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RouteExecution.cs` delegates to the route handler pipeline.
- Red-team negative case: `bundle://proof/transcripts/failing-first-route-handler-boundary.txt` demonstrates the old inline route body cannot satisfy the new handler-pipeline invariant.
- Downstream dependency check: `bundle://proof/transcripts/source-boundary-scan.txt` confirms no Process Core, no production driver API, no UI/browser/mobile drift, and route handler order matching the canonical pipeline.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| Claimed dispatch route handler order | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RouteHandlers.cs` | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RouteHandlers.cs` | Handler list is validated against `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRoutePipeline.cs` before route execution. | `bundle://proof/transcripts/failing-first-route-handler-boundary.txt` |
| Direct-agent execution outcome context | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RouteHandlers.cs` | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RouteHandlers.cs` | Direct-agent handler sets the outcome; competing, run-closed, and finalizer handlers require it. | `bundle://proof/transcripts/unit-route-boundary-tests.txt` |
