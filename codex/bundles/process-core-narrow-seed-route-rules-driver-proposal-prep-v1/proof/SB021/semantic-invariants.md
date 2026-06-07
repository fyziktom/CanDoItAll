# SB021 Semantic Invariants

- Invariant ID: `SB021-CORE-HYGIENE`
- Source raw note: Maintain long-term evolvability by keeping the Core seed narrow and dependency-clean.
- Expected behavior: Solution build and focused route/dispatch tests pass with Core registered in the solution.
- Disallowed shallow implementation: Shipping Core without solution registration, build proof, adapter proof, dependency scans, or anti-stub scans.
- Failing-first test: N/A process/no production behavior; the architecture suite rejects incomplete or overly broad Core structure.
- Passing test: bundle://proof/common/transcripts/build-solution.txt, bundle://proof/common/transcripts/full-unit.txt, bundle://proof/common/transcripts/unit-architecture.txt, and bundle://proof/common/transcripts/integration-dispatch.txt
- Changed source files: repo://CanDoItAll.slnx, repo://src/CanDoItAll.Processes.Core, and repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs
- Production assertions: Existing dispatch behavior compiles and tests through module-owned orchestration while Core remains pure.
- Red-team negative case: bundle://proof/common/transcripts/core-forbidden-scan.txt and bundle://proof/common/transcripts/anti-stub-scan.txt reject dependency and placeholder drift.
- Downstream dependency check: bundle://proof/common/transcripts/build-solution.txt proves downstream projects compile after explicit Contracts references.
