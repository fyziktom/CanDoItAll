# SB01 Semantic Invariants

- Invariant ID: `SB01-INV-001`
- Source raw note: RN-001, RN-002, RN-004.
- Expected behavior: Entry proof establishes the current dispatch source shape before refactoring and confirms runtime/service-only scope.
- Disallowed shallow implementation: Starting helper extraction without proving the target source files exist, current line counts are known, and scope scans are clean.
- Failing-first test: `bundle://proof/SB01/transcripts/sb01-architecture-test.txt` records that the broad historical architecture class is not usable as a clean baseline because old bundle artifact paths are missing.
- Passing test: `bundle://proof/SB01/transcripts/sb01-focused-architecture-tests.txt` passes current no-core/no-driver and no-prohibited-viewport guardrails.
- Changed source files: None in production or test source for SB01.
- Production assertions: `bundle://proof/SB01/source-assertions/source-shape.md`.
- Red-team negative case: `bundle://proof/SB01/transcripts/sb01-no-core-no-driver-scan.txt` fails if Process Core or production driver API tokens appear in the Processes module.
- Downstream dependency check: SB02 can use the recorded line counts and clean scope scans as the source baseline; broad historical architecture-class failures must not be used as proof for this bundle.
