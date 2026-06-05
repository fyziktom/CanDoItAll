# SB19 Semantic Invariants

- Invariant ID: `SB19-INV-001`
- Source raw note: Final source scans must prove no Process Core, no production driver APIs, no UI drift, and no prohibited viewport proof.
- Expected behavior: Dispatch helpers stay under the module-local dispatch folder and Dispatch.cs line count drops after extraction.
- Disallowed shallow implementation: Closing the bundle with only build output and no negative source scans is rejected.
- Failing-first test: N/A process source-scan gate; interpreted negative scans are the required closure proof.
- Passing test: Final source assertions and focused facade regression tests pass.
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`; `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- Production assertions: No prohibited source boundary, driver API, UI file, or viewport proof artifact was introduced.
- Red-team negative case: Source assertions reject Process Core directories, driver API tokens, UI drift, and mobile/tablet proof paths.
- Downstream dependency check: SB20 final red-team uses these final scans as closure input.

