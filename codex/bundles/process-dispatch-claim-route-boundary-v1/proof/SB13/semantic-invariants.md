# SB13 Semantic Invariants

- Invariant ID: `SB13_INV_001`
- Source raw note: RN-001 and RN-003.
- Expected behavior: Finalizer context construction for manager recovery, direct agent, workflow, and subprocess routes is centralized without changing any finalizer field values.
- Disallowed shallow implementation: A factory that omits route-specific ids/flags, keeps inline dispatcher constructors, or moves finalization/transition side effects.
- Failing-first test: N/A - non-critical factory extraction; focused architecture test and source scan prove field parity and no side effects.
- Passing test: `bundle://proof/SB13/transcripts/sb13-finalizer-context-factory-tests.txt`.
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.FinalizerContextFactory.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`, `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`.
- Production assertions: `bundle://proof/SB13/source-assertions/finalizer-context-factory.md`.
- Red-team negative case: `bundle://proof/SB13/transcripts/sb13-anti-stub-and-scope-scan.txt`.
- Downstream dependency check: SB14/SB16 can refer to finalizer context construction as a named local boundary without treating it as a production driver API.
