# Header Selector And Snapshot Parity Semantic Invariants

- Invariant ID: SB08-INV-001
- Source raw note: preserve original candidate header selection and hydration readback behavior.
- Expected behavior: header selection delegates to ProcessDispatchCandidateHeaderSelector.SelectAsync; hydration readback delegates to ProcessDispatchCandidateHydrationLoader.LoadAsync without moving side effects.
- Disallowed shallow implementation: inline header query logic remains in the dispatcher, or the hydration loader performs writes, workflow execution, or technical-agent binding.
- Failing-first test: `proof/SB08/transcripts/sb08-failing-first-selector-snapshot-trap.txt`
- Passing test: `proof/current/transcripts/candidate-hydration-architecture-tests.txt`
- Changed source files: ProcessRunAutomationDispatchService.Dispatch.cs, ProcessDispatchCandidateHeaderSelector.cs, ProcessDispatchCandidateHydrationLoader.cs, ProcessAgentExecutionBoundaryArchitectureTests.cs.
- Production assertions: module-local selector and loader helpers preserve candidate hydration behavior without adding Process Core, driver APIs, UI changes, or hidden side-effect movement.
- Red-team negative case: `proof/SB08/transcripts/sb08-failing-first-selector-snapshot-trap.txt`
- Downstream dependency check: Unlocks artifact, branch, and assignment assembly movement in SB09-SB12.
