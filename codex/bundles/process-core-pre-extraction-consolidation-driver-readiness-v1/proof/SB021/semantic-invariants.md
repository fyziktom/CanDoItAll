# SB021 Semantic Invariants

## Invariants

- Invariant ID: `SB021-INV-001`
- Source raw note: `Prove retry, provider repair, no-progress, competing execution, finalizer detail compatibility.`
- Expected behavior: Direct-agent runtime uses route-owned execution input, retry/provider/no-progress paths remain intact, competing execution uses the slim route run snapshot, and finalizer detail compatibility is preserved through the adapter edge.
- Disallowed shallow implementation: Proving only the DTO shape while dropping retry/provider/no-progress branches, changing provider fallback behavior, using dispatcher detail in route consumers, or bypassing direct-agent finalizer context construction.
- Failing-first test: `N/A - no production behavior change was intended; this critical gate validates execution parity after SB019/SB020 boundary tightening.`
- Passing test: `bundle://proof/SB021/transcripts/execution-parity-architecture-test.txt` and `bundle://proof/SB021/transcripts/execution-parity-focused-integration-tests.txt`
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchDirectAgentExecutionModels.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchDirectAgentExecutionAdapter.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchDirectAgentRuntimeService.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteModels.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCompetingExecutionGuardService.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteHandlers.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchFinalizerAdapter.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Execution.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ProviderRecovery.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessExecutionAttemptLoopFacade.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessProviderRepairCoordinator.cs`, `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`, `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- Production assertions: `bundle://proof/SB021/transcripts/source-assertions-and-scans.txt`
- Red-team negative case: Removing no-progress duplicate detection, broadening provider fallback to Ollama, reading `executionOutcome.Detail` in competing guard, or bypassing `ProcessDispatchFinalizerContextFactory.ForDirectAgent` fails SB021 proof.
- Downstream dependency check: `SB022` may start artifact-rule hardening because execution DTO/snapshot tightening is parity-proved.

## Raw Note Closure

- Preserve retry/provider/no-progress execution behavior: `Solved for SB021 with focused integration and source proof.`
- Preserve competing execution and finalizer detail compatibility: `Solved for SB021 with route snapshot and adapter-edge proof.`
- Do not rush Process Core: `Partially solved without creating Core; final decision remains owned by SB036.`
