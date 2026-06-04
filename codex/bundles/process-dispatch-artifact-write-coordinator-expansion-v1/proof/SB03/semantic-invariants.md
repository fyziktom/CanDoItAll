# SB03 Semantic Invariants

## SB03-INV-001 Coordinator Outcome Contract

- Invariant ID: SB03-INV-001
- Source raw note: RQ-004 requires the write coordinator to return structured artifact write outcomes and preserve failure semantics.
- Expected behavior: ProcessArtifactProjectionWriteCoordinator returns artifact record id, managed storage path, external reference key, and optional expectation id after placement and recording succeed.
- Disallowed shallow implementation: A shallow pass would leave callers to infer record ids or silently hide placement/recording failures.
- Failing-first test: bundle://proof/SB03/transcripts/failing-first-coordinator-outcome-tests.txt
- Passing test: bundle://proof/SB03/transcripts/passing-coordinator-outcome-tests.txt
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionWriteCoordinator.cs; repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs; repo://tests/CanDoItAll.Tests.Integration/ProcessArtifactProjectionWriteCoordinatorTests.cs
- Production assertions: bundle://proof/SB03/source-assertions/outcome-contract-source-scan.txt
- Red-team negative case: The failing-first transcript proves missing structured outcome support broke the focused coordinator tests before implementation.
- Downstream dependency check: SB04 Gate A and later migration source guards consume the structured outcome contract.

