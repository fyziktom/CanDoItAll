# SB012 Semantic Invariants

- Invariant ID: SB012_INV_001
- Source raw note: REQ-004 audit facts and redaction expectations
- Expected behavior: Audit facts carry caller, mode, lane, operation, evidence ids, denial, hash, and redaction status while masking secrets.
- Disallowed shallow implementation: Recording diagnostics without masking sensitive fields.
- Failing-first test: N/A - no production behavior change; policy-denial negative cases execute in the passing test transcript.
- Passing test: Process_driver_prerequisites_SB012_INV_001_capture_audit_facts_and_redact_sensitive_values with transcript bundle://proof/shared/transcripts/focused-prerequisite-tests.txt
- Changed source files: repo://tests/CanDoItAll.Tests.Unit/ProcessDriverContractPrerequisitesVerificationTests.cs SHA-256 FEEBCDEAC91B755517FD5B3E725B0FCCF02BAE64A397E8FC3A9BA28243E4A036
- Production assertions: production source under repo://src has no production driver runtime tokens, no forbidden Core dependencies, and no stub markers in bundle://proof/shared/transcripts/source-scans.txt.
- Red-team negative case: Rejects leaked token and email content.
- Downstream dependency check: Unlocks sandbox and command denial phases.
