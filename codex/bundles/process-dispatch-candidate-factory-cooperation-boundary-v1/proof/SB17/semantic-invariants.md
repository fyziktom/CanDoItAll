# SB17 Semantic Invariants

- Invariant ID: SB17-INV-001
- Source raw note: Close the bundle without Process Core, production driver API, UI proof, or hidden stubs.
- Expected behavior: Final red-team proof cites passing tests, full build, no forbidden production dispatch identifiers, no hidden side effects in candidate helpers, no stubs in new helpers, and no prohibited proof paths.
- Disallowed shallow implementation: Marking raw notes solved from prose while proof artifacts or scans are missing.
- Failing-first test: N/A process/non-production exemption: SB17 is the final verifier gate and relies on earlier failing-first proof plus final negative scans.
- Passing test: bundle://proof/SB17/transcripts/sb17-final-red-team-scans.txt; bundle://proof/SB16/transcripts/sb16-full-solution-build.txt
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCandidateAssemblyContext.cs; repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCandidateFactory.cs; repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCooperationMetadataResolver.cs; repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs; repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs
- Production assertions: Candidate construction and cooperation metadata are isolated module-locally with explicit side-effect boundaries.
- Red-team negative case: Final red-team transcript rejects fake closure without guardrail scan evidence.
- Downstream dependency check: SB18 next cutline records documentation-only follow-up direction after final proof passes.
