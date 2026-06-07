# SB024 Semantic Invariants

- Invariant ID: SB024_INV_001
- Source raw note: REQ-008 Core descriptor consumer hardening
- Expected behavior: Only explicit process-module adapter files consume Core descriptors.
- Disallowed shallow implementation: Searching for Core references without enforcing an allow-list.
- Failing-first test: N/A - no production behavior change; policy-denial negative cases execute in the passing test transcript.
- Passing test: Process_driver_prerequisites_SB024_INV_001_keep_core_descriptor_consumers_allowlisted with transcript bundle://proof/shared/transcripts/focused-prerequisite-tests.txt
- Changed source files: repo://tests/CanDoItAll.Tests.Unit/ProcessDriverContractPrerequisitesVerificationTests.cs SHA-256 FEEBCDEAC91B755517FD5B3E725B0FCCF02BAE64A397E8FC3A9BA28243E4A036
- Production assertions: production source under repo://src has no production driver runtime tokens, no forbidden Core dependencies, and no stub markers in bundle://proof/shared/transcripts/source-scans.txt.
- Red-team negative case: Rejects unapproved dispatch files that import Core descriptors.
- Downstream dependency check: Unlocks Office and business lane denial phases.
