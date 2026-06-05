# Final Red-Team Semantic Invariants

- Invariant ID: SB18-INV-001
- Source raw note: final closure, future-driver readiness as documentation only, and no prohibited UI/proof scope.
- Expected behavior: final red-team scan confirms helper tokens, no Process Core or driver API source, no UI diff, no prohibited proof path names, and documentation-only driver-readiness mapping.
- Disallowed shallow implementation: final report prose is marked complete without manifest-backed source, command, failing-first, semantic positive, and anti-stub proof.
- Failing-first test: `proof/SB18/transcripts/sb18-failing-first-final-red-team-trap.txt`
- Passing test: `proof/SB18/transcripts/sb18-final-red-team-scan.txt`
- Changed source files: ProcessDispatchCandidateHeaderSelector.cs, ProcessDispatchCandidateHydrationLoader.cs, ProcessDispatchArtifactInputAssembler.cs, ProcessDispatchBranchDependencyContext.cs, ProcessDispatchAssignmentRouteHelper.cs, ProcessDispatchTechnicalAgentBindingCoordinator.cs, ProcessDispatchRecoveryQueryHelper.cs, ProcessRunAutomationDispatchService.Dispatch.cs, ProcessRunAutomationDispatchService.ArtifactValidation.cs, ProcessRunAutomationDispatchService.Cooperation.cs, ProcessAgentExecutionBoundaryArchitectureTests.cs.
- Production assertions: closure keeps the candidate hydration boundary module-local, excludes Process Core and production driver APIs, avoids UI changes, and records future driver readiness only in documentation.
- Red-team negative case: `proof/SB18/transcripts/sb18-failing-first-final-red-team-trap.txt`
- Downstream dependency check: no downstream production driver API or UI proof dependency remains for this bundle.
