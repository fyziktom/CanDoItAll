# Candidate Assembly Parity Semantic Invariants

- Invariant ID: SB12-INV-001
- Source raw note: preserve artifact-input shaping, branch outcomes, and assignment/workflow route behavior.
- Expected behavior: artifact input assembly, branch dependency context, and assignment route recognition are behind local helpers with dispatcher wrappers preserved.
- Disallowed shallow implementation: helpers exist by name but shaping remains inline, artifact filtering changes, or branch/assignment semantics drift.
- Failing-first test: `proof/SB12/transcripts/sb12-failing-first-assembly-helper-trap.txt`
- Passing test: `proof/current/transcripts/candidate-hydration-integration-wrapper-tests.txt`
- Changed source files: ProcessDispatchArtifactInputAssembler.cs, ProcessDispatchBranchDependencyContext.cs, ProcessDispatchAssignmentRouteHelper.cs, ProcessRunAutomationDispatchService.Dispatch.cs, ProcessRunAutomationDispatchService.ArtifactValidation.cs, ProcessRunAutomationDispatchService.Cooperation.cs, ProcessAgentExecutionBoundaryArchitectureTests.cs.
- Production assertions: existing dispatcher wrappers remain available and delegate to local helpers, preserving artifact input filtering, branch dependency shaping, and assignment/workflow route recognition.
- Red-team negative case: `proof/SB12/transcripts/sb12-failing-first-assembly-helper-trap.txt`
- Downstream dependency check: Unlocks side-effectful technical-agent binding and recovery query isolation in SB13-SB16.
