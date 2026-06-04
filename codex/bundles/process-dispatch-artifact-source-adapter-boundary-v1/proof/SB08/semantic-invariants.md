# SB08 Semantic Invariants

Status: Completed.
Objective: Response text and provider-native browser projection adapters.

## Invariant Contract

- Invariant ID: INV-SB08-ARTIFACT-PROJECTION-BOUNDARY
- Source raw note: Continue small dispatcher isolation while preserving artifact projection, validation, lineage, and required-artifact behavior.
- Expected behavior: Source-specific projection planning is isolated behind typed projection snapshots and adapters while existing ProcessArtifactRecord metadata, trust, lineage, and duplicate-key semantics remain unchanged.
- Disallowed shallow implementation: Empty adapter classes, retained dispatcher-nested expectation dependencies, changed external reference keys, or moving source semantics into the write coordinator are rejected.
- Failing-first test: N/A process/non-production proof; architecture source scans in bundle://proof/SB03/source-assertions/failing-first-helper-dependency.md describe the rejected pre-boundary helper dependency shape.
- Passing test: bundle://proof/SB03/transcripts/focused-unit-architecture.txt, bundle://proof/SB05/transcripts/focused-integration-projection-slice.txt, and bundle://proof/SB11/transcripts/full-solution-build.txt.
- Changed source files: bundle://proof/SB12/source-assertions/changed-file-hashes.txt lists repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch and repo://tests changed files with SHA-256 hashes.
- Production assertions: bundle://proof/SB12/source-assertions/final-source-scans.txt proves no Process Core, driver-pack, or MAF product-module dependency was introduced.
- Red-team negative case: bundle://proof/SB12/source-assertions/red-team-audit.md records fake-proof risks and the tests or scans that reject them.
- Downstream dependency check: SB gate table in bundle://reviews/01-execution-report.md shows downstream closure from SB01 through SB12.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| ProcessArtifactRecord projection request | repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionWriteCoordinator.cs and repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionSourceAdapters.cs | repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs | bundle://proof/SB05/transcripts/focused-integration-projection-slice.txt and bundle://proof/SB11/transcripts/full-solution-build.txt | ProcessAgentExecutionBoundaryArchitectureTests in bundle://proof/SB03/transcripts/focused-unit-architecture.txt rejects shallow coordinator and helper-boundary drift. |

## Browser Validation

N/A. No rendered UI route changed.
