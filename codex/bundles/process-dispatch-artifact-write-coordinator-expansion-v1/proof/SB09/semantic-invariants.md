# SB09 Semantic Invariants

## SB09-INV-001 Response Text Write Migration

- Invariant ID: SB09-INV-001
- Source raw note: RQ-008 requires response-text writes to use the coordinator without moving text generation, UTF-8 file creation, or path safety into the coordinator.
- Expected behavior: Response text projection writes the text file in the dispatcher, plans response or existing-managed sources, and records through WriteAsync.
- Disallowed shallow implementation: A shallow pass would move File.WriteAllTextAsync or IsWithinWorkspace into the coordinator, or keep direct RecordArtifactAsync in the response path.
- Failing-first test: bundle://proof/SB09/transcripts/failing-first-response-text-source-guard.txt
- Passing test: bundle://proof/SB09/transcripts/response-text-tests.txt
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs; repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs
- Production assertions: bundle://proof/SB09/source-assertions/response-text-source-scan.txt
- Red-team negative case: The failing-first source guard captured the old response section before WriteAsync was required.
- Downstream dependency check: SB10-SB12 reuse the same coordinator outcome application helper and Gate C verifies the boundary.

