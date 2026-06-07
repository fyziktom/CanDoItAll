# SB013 Semantic Invariants

## Invariants

- Invariant ID: `SB013-INV-001`
- Source raw note: `Separate pure database blocking decision from transition execution.`
- Expected behavior: Database requirement handling resolves a typed pure decision from route facts, keeps unsupported/no-transition cases explicit, and executes claim-bound transitions only in the route service.
- Disallowed shallow implementation: A helper extraction that still performs `TransitionStepWithClaimAsync` inside pure decision code, accepts dispatcher candidates instead of route facts, or hides unsupported transition cases.
- Failing-first test: `N/A - no production behavior change was intended; this subbundle validates an existing behavior-preserving pre-execution split.`
- Passing test: `bundle://proof/SB013/transcripts/pre-execution-database-architecture-test.txt` and `bundle://proof/SB013/transcripts/pre-execution-database-focused-integration-tests.txt`
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchPreExecutionRouteFacts.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchPreExecutionGuardHandler.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteServices.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessAutomationDatabaseRequirementResolver.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchDatabaseRequirementBlocker.cs`, `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`, `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- Production assertions: `bundle://proof/SB013/transcripts/pre-execution-database-source-assertions.txt`
- Red-team negative case: Reintroducing `TransitionStepWithClaimAsync` into `ProcessDispatchPreExecutionGuardHandler` or accepting `ProcessRouteCandidate` in pure pre-execution handlers fails the SB013 guard.
- Downstream dependency check: `SB014` may proceed to upstream materialization facts and side effects because database requirement decision/transition ownership is explicit.

## Raw Note Closure

- Database requirement split: `Solved for SB013 with typed pure decision and route-service transition side effect.`
- Preserve pre-execution behavior: `Proved through focused route planner/database blocker tests.`
- Do not rush Process Core: `Partially solved without creating Core; final decision remains owned by SB036.`
