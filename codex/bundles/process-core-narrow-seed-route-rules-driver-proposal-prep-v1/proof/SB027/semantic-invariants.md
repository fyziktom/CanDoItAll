# SB027 Semantic Invariants

- Invariant ID: `SB027-BROAD-SMOKE`
- Source raw note: Broader meaningful phases require proof, not collapsed reporting.
- Expected behavior: Each gate is individually reported and backed by build/test/scan transcripts.
- Disallowed shallow implementation: Marking the bundle complete with collapsed rows, weak proof, or pending browser/raw-note closure.
- Failing-first test: N/A process/no production behavior; final report validation rejects pending gate and raw-note entries.
- Passing test: bundle://proof/common/transcripts/build-solution.txt, bundle://proof/common/transcripts/full-unit.txt, bundle://proof/common/transcripts/unit-architecture.txt, and bundle://proof/common/transcripts/integration-dispatch.txt
- Changed source files: bundle://reviews/01-execution-report.md and repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs
- Production assertions: The shipped source change remains the narrow Core route-rule seed plus explicit project references.
- Red-team negative case: bundle://proof/common/transcripts/ui-media-drift-scan.txt and bundle://proof/common/transcripts/production-driver-token-scan.txt reject forbidden drift.
- Downstream dependency check: bundle://proof/common/transcripts/build-solution.txt proves downstream compilation.
