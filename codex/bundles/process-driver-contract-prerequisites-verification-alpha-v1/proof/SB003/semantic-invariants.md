# SB003 Semantic Invariants

- Invariant ID: SB003_INV_001
- Source raw note: REQ-001 latest work completion check
- Expected behavior: Branch, prior bundle proof, no production driver runtime tokens, and no UI/media drift are checked together before downstream work starts.
- Disallowed shallow implementation: Only checking that the branch exists while skipping source and prior-proof scans.
- Failing-first test: N/A - no production behavior change; policy-denial negative cases execute in the passing test transcript.
- Passing test: Process_driver_prerequisites_SB003_INV_001_preserve_baseline_branch_and_no_runtime_guardrails with transcript bundle://proof/shared/transcripts/focused-prerequisite-tests.txt
- Changed source files: repo://tests/CanDoItAll.Tests.Unit/ProcessDriverContractPrerequisitesVerificationTests.cs SHA-256 FEEBCDEAC91B755517FD5B3E725B0FCCF02BAE64A397E8FC3A9BA28243E4A036
- Production assertions: production source under repo://src has no production driver runtime tokens, no forbidden Core dependencies, and no stub markers in bundle://proof/shared/transcripts/source-scans.txt.
- Red-team negative case: Rejects wrong branch, forbidden production driver tokens, or UI/media drift.
- Downstream dependency check: Unlocks SB004-SB006 Core governance work.
