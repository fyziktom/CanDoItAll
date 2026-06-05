# SB16 Semantic Invariants

- Invariant ID: `SB16-INV-001`
- Source raw note: Runtime smoke must pass after the pre-execution facade is wired into Dispatch.cs.
- Expected behavior: The solution builds and focused dispatch guard/materialization tests pass after facade wiring.
- Disallowed shallow implementation: Source scans without build and focused tests are insufficient for runtime closure.
- Failing-first test: N/A process/runtime smoke gate; the gate is build and focused regression proof.
- Passing test: Build and focused facade regression tests pass.
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchPreExecutionGuardHandler.cs`; `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`; `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- Production assertions: Dispatch.cs delegates database guard and upstream materialization planning to the facade while preserving wrapper behavior.
- Red-team negative case: Source assertions reject closure without build, focused tests, and anti-stub scans.
- Downstream dependency check: SB17-SB20 use this passing runtime gate as their baseline.

