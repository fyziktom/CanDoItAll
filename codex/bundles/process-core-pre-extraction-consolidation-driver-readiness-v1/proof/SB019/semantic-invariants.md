# SB019 Semantic Invariants

## Invariants

- Invariant ID: `SB019-INV-001`
- Source raw note: `Remove full dispatcher payloads from direct-agent runtime boundary except one adapter edge.`
- Expected behavior: Direct-agent route handler creates route-owned execution input, runtime executes through that input/output contract, and only `ProcessDispatchDirectAgentExecutionAdapter` converts to dispatcher candidate/outcome for the legacy `ExecuteUntilSettledAsync` call.
- Disallowed shallow implementation: Keeping dispatcher aliases or `ProcessDispatchRouteModelAdapters` in the runtime service or route-facing service while wrapping the call in a DTO.
- Failing-first test: `N/A - no production behavior change was intended; this subbundle validates an existing behavior-preserving direct-agent execution DTO boundary.`
- Passing test: `bundle://proof/SB019/transcripts/direct-agent-execution-dto-architecture-test.txt` and `bundle://proof/SB019/transcripts/direct-agent-execution-focused-integration-tests.txt`
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchDirectAgentExecutionModels.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchDirectAgentRuntimeService.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchDirectAgentExecutionAdapter.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteFacets.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteHandlers.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteServices.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RouteHandlers.cs`, `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`, `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- Production assertions: `bundle://proof/SB019/transcripts/direct-agent-execution-dto-source-assertions.txt`
- Red-team negative case: Adding `ProcessRunAutomationDispatchService.DispatchCandidate` or `ProcessDispatchRouteModelAdapters` to `ProcessDispatchDirectAgentRuntimeService` fails SB019 guards.
- Downstream dependency check: `SB020` may create the execution proof/readiness snapshot because the direct-agent runtime boundary is route-owned.

## Raw Note Closure

- Direct-agent execution DTO boundary: `Solved for SB019 with one adapter edge and route-owned runtime input/output.`
- Preserve direct-agent behavior: `Partially proved here; SB021 owns critical execution parity.`
- Do not rush Process Core: `Partially solved without creating Core; final decision remains owned by SB036.`
