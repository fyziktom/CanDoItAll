# SB08 semantic invariants

## SB08-I1 final closure is evidence backed

- Source raw note: final report must distinguish changed, skipped, passed, failed, and residual-risk work.
- Expected behavior: final closure cites transcripts and source assertions, not prose-only claims.
- Disallowed shallow implementation: marking all checks green while broad suites timed out or environment prerequisites failed.
- Passing proof: `bundle://proof/SB08/transcripts/full-solution-build-final-clean.txt`, `bundle://proof/SB08/transcripts/full-unit-tests.txt`, and `bundle://proof/SB08/transcripts/focused-integration-tests.txt`.
- Changed source files and hashes: `bundle://proof/SB08/transcripts/changed-file-hashes.txt`.
- Production assertions: `bundle://proof/SB08/final-execution-report.md`.
- Red-team negative case: `bundle://proof/SB08/transcripts/anti-stub-audit.txt`.
- Downstream dependency check: final validator transcript.
