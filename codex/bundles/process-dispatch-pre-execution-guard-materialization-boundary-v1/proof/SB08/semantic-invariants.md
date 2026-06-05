# SB08 Semantic Invariants

- Invariant ID: `SB08-INV-001`
- Source raw note: Preserve original pre-execution guard behavior while isolating database and upstream gap facts.
- Expected behavior: Database requirement decisions preserve blocked/failed target status rules and upstream gap facts select only missing runnable agent-sourced dependencies.
- Disallowed shallow implementation: A helper that treats no-op targets as runnable or changes block transition shape is rejected.
- Failing-first test: N/A process refactor; focused parity assertions cover the negative cases.
- Passing test: Focused database blocker and missing upstream materialization facts tests pass.
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchDatabaseRequirementBlocker.cs`; `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessMissingUpstreamArtifactMaterialization.cs`; `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- Production assertions: Status targets, block reason, and upstream dependency selection stay equivalent to the dispatcher-local behavior.
- Red-team negative case: Source assertions reject runnable selection for unsupported no-op targets and non-missing dependencies.
- Downstream dependency check: SB09-SB14 build on these facts without changing their meaning.

