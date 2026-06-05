# SB04 Gate A Source Assertions

- Invariant ID: `SB04-INV-001` pins live finalizer inventory, local helper cutline, no Process Core source, no production driver API source, no MAF module reference broadening, and no prohibited viewport proof paths.
- Invariant ID: `SB04-INV-002` preserves the nested finalizer type surface across future movement by scanning all `ProcessRunAutomationDispatchService.StepCompletionFinalizer*.cs` files.
- Failing-first transcript: `bundle://proof/SB04/transcripts/gate-a-architecture-tests-rebuilt.txt` failed until the SB02 inventory explicitly named `ProcessStepTransitionRequest`.
- Passing transcript: `bundle://proof/SB04/transcripts/gate-a-architecture-tests-passing.txt` passed 34 architecture tests.
- Changed test file: `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`.
