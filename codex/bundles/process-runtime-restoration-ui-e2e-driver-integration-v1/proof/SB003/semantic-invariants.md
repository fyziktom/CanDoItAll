# SB003 Semantic Invariants

- Invariant ID: `SB003_INV_001`
- Source raw note: Tests contain bundle names and bundle folders are being deleted.
- Expected behavior: the current branch, HEAD commit, and source/test bundle-path contamination are re-read from real repository state before implementation continues.
- Disallowed shallow implementation: marking Gate A complete from prepared report prose or collapsed execution rows without source-backed inspection.
- Failing-first test: N/A - non-production current-state inventory gate; SB006 carries the behavior-changing failing-first transcript.
- Passing test: `bundle://proof/SB003/transcripts/source-backed-current-state-scan.txt`
- Changed source files: bundle contract and proof files only for SB003; behavior-changing source/test files are owned by SB006.
- Production assertions: process runtime production code is not changed by SB003; the gate only records source-backed current state.
- Red-team negative case: report-only closure is rejected by requiring `bundle://proof/SB003/transcripts/source-backed-current-state-scan.txt` and `bundle://proof/SB006/transcripts/anti-stub-audit-changed-files.txt`.
- Downstream dependency check: SB004-SB006 cannot proceed unless this inventory names concrete test/source contamination and the prepared validator passes after bundle repair.

