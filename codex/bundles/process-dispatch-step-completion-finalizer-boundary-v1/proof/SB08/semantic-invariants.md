# SB08 Refactor Gate B Type Reader Parity Semantic Invariants

- Invariant ID: SB08-INV-001
- Source raw note: Preserve original functions through focused parity tests.
- Expected behavior: The gate sees extracted types/readers, no duplicated nested implementation in the main finalizer, and no Process Core/driver production API.
- Disallowed shallow implementation: A shallow split that leaves duplicate nested code in the main finalizer or moves behavior into a future core project.
- Failing-first test: N/A refactor parity gate; no production behavior change was introduced, so no behavior-level failing-first transcript applies.
- Passing test: bundle://proof/SB12/transcripts/gate-b-c-architecture-tests.txt
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs; repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.Types.cs; repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.ArtifactContentReaders.cs; repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs
- Production assertions: Processes-module behavior is preserved; no Process Core project, driver pack API, or UI file change is introduced.
- Red-team negative case: bundle://proof/SB08/transcripts/anti-stub-audit.txt rejects placeholder exception/TODO implementation markers and boundary drift for this scope.
- Downstream dependency check: Execution report gate row and final red-team scan confirm downstream SBs can proceed or close without expanding the process-driver boundary.
