# SB030 Semantic Invariants

- Invariant ID: `SB030-FINAL-CLOSURE`
- Source raw note: Finish only after implementation, validation, proof, and final report closure are complete.
- Expected behavior: Completed-stage bundle validation passes and all report statuses are non-pending.
- Disallowed shallow implementation: Leaving status as prepared, collapsing rows, missing proof citations, or skipping final scans.
- Failing-first test: N/A process/no production behavior; completed validator is the closure gate.
- Passing test: bundle://proof/common/transcripts/completed-validator.txt
- Changed source files: bundle://README.md, bundle://reviews/01-execution-report.md, and bundle://proof/SB030/manifest.md
- Production assertions: Final source remains limited to the narrow route-rule Core seed and explicit enum sharing through Contracts.
- Red-team negative case: bundle://proof/common/transcripts/core-forbidden-scan.txt, bundle://proof/common/transcripts/production-driver-token-scan.txt, and bundle://proof/common/transcripts/ui-media-drift-scan.txt reject forbidden drift.
- Downstream dependency check: bundle://proof/common/transcripts/build-solution.txt and bundle://proof/common/transcripts/full-unit.txt prove compile and full-unit closure.
