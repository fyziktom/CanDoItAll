# SB12 Semantic Invariants

## SB12-INV-001 Gate C Write Boundary Closure

- Invariant ID: SB12-INV-001
- Source raw note: RQ-011 through RQ-013 require the final migrated write boundary to be checked before runtime smoke.
- Expected behavior: All storage-backed projection writes are coordinator-owned, completed-decision writes are record-only, and source semantics remain outside the coordinator.
- Disallowed shallow implementation: A shallow pass would let direct placement/record calls remain, force completed decisions through storage placement, or move source planning into the coordinator.
- Failing-first test: N/A - process gate; no production behavior changed in SB12 beyond proof and guardrail closure.
- Passing test: bundle://proof/SB12/transcripts/gate-c-tests.txt
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs; repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionWriteCoordinator.cs; repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs; repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs
- Production assertions: bundle://proof/SB12/source-assertions/final-write-boundary-scan.txt
- Red-team negative case: The red-team source scan verifies no prohibited direct write, source-planning movement, Process Core, driver-pack, or viewport proof artifact path remains.
- Downstream dependency check: SB13 runtime smoke and SB14 final closure consume Gate C proof.

