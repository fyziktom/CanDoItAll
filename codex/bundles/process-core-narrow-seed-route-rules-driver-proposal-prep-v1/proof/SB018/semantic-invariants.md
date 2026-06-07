# SB018 Semantic Invariants

- Invariant ID: `SB018-ARTIFACT-STAYS-MODULE-LOCAL`
- Source raw note: Artifact expectation candidates require later isolation before any Core move.
- Expected behavior: Artifact expectation, validation, projection, workspace, and storage behavior remains module-local.
- Disallowed shallow implementation: Moving storage/workspace/projection helpers into Core or masking side effects behind generic route rules.
- Failing-first test: N/A process/no production behavior; non-movement is proven by Core dependency scans and architecture tests.
- Passing test: bundle://proof/common/transcripts/unit-architecture.txt and bundle://proof/common/transcripts/integration-dispatch.txt
- Changed source files: repo://src/CanDoItAll.Processes.Core/Routing/ProcessDispatchRouteSnapshot.cs and repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs
- Production assertions: Core has no storage, workspace, file IO, projection, or artifact write surface.
- Red-team negative case: bundle://proof/common/transcripts/core-forbidden-scan.txt rejects workspace/storage and infrastructure tokens.
- Downstream dependency check: bundle://proof/common/transcripts/build-solution.txt proves artifact-related consumers still compile.
