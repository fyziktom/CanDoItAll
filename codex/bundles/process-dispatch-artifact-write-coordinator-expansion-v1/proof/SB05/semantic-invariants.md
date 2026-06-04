# SB05 Semantic Invariants

## SB05-INV-001 Process Mock Write Migration

- Invariant ID: SB05-INV-001
- Source raw note: RQ-005 requires process mock artifact writes to use the coordinator while preserving hard-failure behavior.
- Expected behavior: ProjectProcessMockArtifactsAsync plans process mock sources, calls WriteAsync, and updates candidate external references and expectation state only from the structured coordinator result.
- Disallowed shallow implementation: A shallow pass would keep direct storage placement or direct RecordArtifactAsync in the process mock section, or soften missing-source behavior.
- Failing-first test: bundle://proof/SB05/transcripts/failing-first-process-mock-source-guard.txt
- Passing test: bundle://proof/SB05/transcripts/process-mock-tests.txt
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs; repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs
- Production assertions: bundle://proof/SB05/source-assertions/process-mock-source-scan.txt
- Red-team negative case: The failing-first source guard proved the process mock section was not yet coordinator-owned before the migration.
- Downstream dependency check: SB06-SB08 rely on the same candidate-state update pattern for later storage-backed paths.

