# SB020 Semantic Invariants

## Invariants

- Invariant ID: `SB020-INV-001`
- Source raw note: `Create slim execution proof snapshot for route/finalizer/driver-readiness documentation.`
- Expected behavior: Route-facing execution outcomes carry only `ProcessRouteExecutionRunSnapshot`; route consumers use `ExecutionRun.Id`; the finalizer adapter remains the explicit application edge that recovers full dispatcher execution detail.
- Disallowed shallow implementation: Keeping full `ProcessAutomationExecutionRunDetail` on route DTOs or letting route consumers call `ProcessDispatchRouteModelAdapters.ToDispatcherExecutionOutcome`.
- Failing-first test: `N/A - no production behavior change was intended; this subbundle validates an existing behavior-preserving execution snapshot boundary.`
- Passing test: `bundle://proof/SB020/transcripts/execution-snapshot-architecture-test.txt`
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteModels.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteModelAdapters.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCompetingExecutionGuardService.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteHandlers.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchFinalizerAdapter.cs`, `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`, `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- Production assertions: `bundle://proof/SB020/transcripts/execution-snapshot-source-assertions.txt`
- Red-team negative case: Reintroducing `ProcessAutomationExecutionRunDetail Detail` to `ProcessRouteExecutionOutcome` or calling `ProcessDispatchRouteModelAdapters.ToDispatcherExecutionOutcome(executionOutcome)` from the competing guard fails SB020 guards.
- Downstream dependency check: `SB021` may run execution parity because the route execution outcome is now slim enough for boundary proof while finalizer detail remains recoverable at the adapter edge.

## Raw Note Closure

- Execution proof/readiness snapshot: `Solved for SB020 with route-facing run snapshots and adapter-owned full-detail recovery.`
- Preserve direct-agent and finalizer behavior: `Partially proved here; SB021 owns critical execution parity.`
- Do not rush Process Core: `Partially solved without creating Core; final decision remains owned by SB036.`
