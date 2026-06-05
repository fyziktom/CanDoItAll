# Runtime Smoke And Side Effect Boundary Semantic Invariants

- Invariant ID: SB16-INV-001
- Source raw note: preserve side-effectful binding/access mutation and recovery query behavior.
- Expected behavior: ProcessDispatchTechnicalAgentBindingCoordinator owns binding/access side effects with explicit outcomes; ProcessDispatchRecoveryQueryHelper owns manual directive and recoverable execution query helper calls.
- Disallowed shallow implementation: binding mutation is hidden inside a pure-looking planner or loader, or recovery query logic remains inline without a local helper boundary.
- Failing-first test: `proof/SB16/transcripts/sb16-failing-first-binding-recovery-trap.txt`
- Passing test: `proof/current/transcripts/candidate-hydration-processes-build.txt`
- Changed source files: ProcessDispatchTechnicalAgentBindingCoordinator.cs, ProcessDispatchRecoveryQueryHelper.cs, ProcessRunAutomationDispatchService.Dispatch.cs, ProcessAgentExecutionBoundaryArchitectureTests.cs.
- Production assertions: technical-agent binding and project-structure read-access mutation are explicit side effects, and recovery query helpers remain local to the dispatcher module.
- Red-team negative case: `proof/SB16/transcripts/sb16-failing-first-binding-recovery-trap.txt`
- Downstream dependency check: Unlocks documentation-only driver-readiness mapping and final red-team closure.
