# SB016 Semantic Invariants

## Invariants

- Invariant ID: `SB016-INV-001`
- Source raw note: `Make subprocess runtime consume route-owned inputs without dispatcher aliases.`
- Expected behavior: Subprocess runtime and projection helpers operate on `ProcessDispatchSubprocessRuntimeInput`, which carries route candidate and route dispatch claim snapshots, while preserving start, capability-gap block, observing, terminal mirror, completed projection, and parent finalizer paths.
- Disallowed shallow implementation: A wrapper that still exposes `ProcessRunAutomationDispatchService.DispatchCandidate`, `ProcessStepDispatchClaim`, route adapters, or direct finalizer adapter calls inside subprocess runtime.
- Failing-first test: `N/A - no production behavior change was intended; this subbundle validates an existing behavior-preserving subprocess runtime boundary split.`
- Passing test: `bundle://proof/SB016/transcripts/subprocess-runtime-input-architecture-test.txt` and `bundle://proof/SB016/transcripts/subprocess-runtime-focused-integration-tests.txt`
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchSubprocessRuntimeModels.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchSubprocessRuntimeService.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteFacets.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteHandlers.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteServices.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RouteHandlers.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessProjectionPlanBuilder.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessProjectionWriterCoordinator.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessProjectionGapJournalCoordinator.cs`, `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`, `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- Production assertions: `bundle://proof/SB016/transcripts/subprocess-runtime-source-assertions.txt`
- Red-team negative case: Adding `using DispatchCandidate`, `ProcessRunAutomationDispatchService.DispatchCandidate`, `ProcessRunAutomationDispatchService.ProcessStepDispatchClaim`, or `ProcessDispatchRouteModelAdapters` to `ProcessDispatchSubprocessRuntimeService` fails SB016 guards.
- Downstream dependency check: `SB017` may proceed to projection persistence ownership because subprocess runtime input ownership is stable.

## Raw Note Closure

- Subprocess runtime input model: `Solved for SB016 with route-owned runtime input and projection helper propagation.`
- Preserve subprocess behavior: `Partially proved here; SB018 owns critical subprocess parity.`
- Do not rush Process Core: `Partially solved without creating Core; final decision remains owned by SB036.`
