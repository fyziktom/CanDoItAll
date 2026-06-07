# SB027 Semantic Invariants

- Invariant ID: SB027_INV_001
- Source raw note: REQ-009 Office and business-analysis read-only lanes
- Expected behavior: Office and business-analysis lanes can inspect existing evidence and return diagnostics only.
- Disallowed shallow implementation: Checking lane names without denying side effects.
- Failing-first test: N/A - no production behavior change; policy-denial negative cases execute in the passing test transcript.
- Passing test: Process_driver_prerequisites_SB027_INV_001_keep_office_and_business_lanes_readonly with transcript bundle://proof/shared/transcripts/focused-prerequisite-tests.txt
- Changed source files: repo://tests/CanDoItAll.Tests.Unit/ProcessDriverContractPrerequisitesVerificationTests.cs SHA-256 FEEBCDEAC91B755517FD5B3E725B0FCCF02BAE64A397E8FC3A9BA28243E4A036
- Production assertions: production source under repo://src has no production driver runtime tokens, no forbidden Core dependencies, and no stub markers in bundle://proof/shared/transcripts/source-scans.txt.
- Red-team negative case: Rejects Graph calls, email mutation, task creation, document writes, business mutation, transitions, and workspace writes.
- Downstream dependency check: Unlocks production driver contract decision.
