# SB034 Semantic Invariants

## Invariants

- Invariant ID: `SB034-INV-001`
- Source raw note: `Run build, full unit tests, focused integration suites, and source scans before final Core/driver decisions.`
- Expected behavior: Broad smoke validation passes across build, full unit tests, process-focused integration tests, forbidden-boundary source scans, UI/mobile drift scans, stub scans, and report row integrity checks.
- Disallowed shallow implementation: Treating a successful build as enough, skipping full unit tests, skipping focused integration coverage, accepting collapsed proof rows, or ignoring Core/driver/UI drift.
- Failing-first test: `N/A - validation-only smoke matrix; no production behavior change was intended.`
- Passing test: `bundle://proof/SB034/transcripts/full-unit-tests.txt` and `bundle://proof/SB034/transcripts/focused-integration-tests.txt`
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteModels.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteModelAdapters.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchFinalizerAdapter.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchFinalizerInputs.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RouteHandlers.cs`, `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`, `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- Production assertions: `bundle://proof/SB034/transcripts/source-assertions-and-scans.txt`
- Red-team negative case: Failing build/tests, adding a production Core project, adding process-driver runtime tokens, modifying UI/media files, adding stub markers, or collapsing SB001-SB034 rows fails SB034 proof.
- Downstream dependency check: `SB035` may perform the final red-team and line-count review because the broad smoke matrix passed.

## Raw Note Closure

- Preserve existing functionality: `Solved for SB034 with build, full unit tests, focused process integration tests, and source scans.`
- No UI/mobile proof: `Solved for SB034 by no UI/mobile/media changed-path scan; final closure remains owned by SB036.`
- No production driver API: `Solved for SB034 by production source scan; final closure remains owned by SB036.`
