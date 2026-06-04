# SB05 Semantic Invariants

- Invariant ID: `SB05-INV-001`
- Source raw note: "Add abstractions/seams first, then use them for concrete paths."
- Expected behavior: Matcher internals use `ProcessArtifactValidationExpectation` snapshots while existing dispatcher callers and tests continue using stable `DispatchArtifactExpectation` entry points.
- Disallowed shallow implementation: Adding snapshot records but leaving the matcher core and projection conversion fully tied to dispatcher-local conversion helpers.
- Failing-first test: `bundle://proof/SB05/transcripts/focused-unit-architecture-tests.txt`
- Passing test: `bundle://proof/SB05/transcripts/focused-unit-architecture-tests-rerun.txt` and `bundle://proof/SB05/transcripts/focused-matcher-integration-tests.txt`
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactValidationSnapshotBuilder.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs`, `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- Production assertions: `bundle://proof/SB05/source-assertions/snapshot-decoupling.md`
- Red-team negative case: The failed compile transcript proves stale projection callers were not hidden; the source scan proves no private dispatcher projection converter remains.
- Downstream dependency check: SB06 may start because matcher parity passed after snapshot conversion.

- Raw note owned: Reduce nested dispatcher expectation use without behavior drift.
- Shipped behavior: Existing matcher behavior remains equivalent; `MatchExpectedArtifactId` integration slice passed.
- Source proof: `bundle://proof/SB05/source-assertions/snapshot-decoupling.md`
- Test proof: `bundle://proof/SB05/transcripts/focused-matcher-integration-tests.txt`
- Shallow-pass trap: Snapshot files compile but pure matching still uses dispatcher-only types and duplicate conversion.
- Adversarial negative proof: `bundle://proof/SB05/transcripts/focused-unit-architecture-tests.txt`
- Semantic positive proof: `bundle://proof/SB05/transcripts/focused-matcher-integration-tests.txt`
- Anti-stub audit: `bundle://proof/SB05/transcripts/snapshot-decoupling-source-scans.txt`
