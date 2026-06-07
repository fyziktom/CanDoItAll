# SB009 Semantic Invariants

- Invariant ID: SB009_INV_001
- Source raw note: REQ-003 permission mode executable tests
- Expected behavior: Missing mode denies everything, verification-only and manager-readonly remain read-only, and execution-capable future mode remains disabled.
- Disallowed shallow implementation: Testing only a happy-path read request.
- Failing-first test: N/A - no production behavior change; policy-denial negative cases execute in the passing test transcript.
- Passing test: Process_driver_prerequisites_SB009_INV_001_enforce_permission_modes_and_capability_denials with transcript bundle://proof/shared/transcripts/focused-prerequisite-tests.txt
- Changed source files: repo://tests/CanDoItAll.Tests.Unit/ProcessDriverContractPrerequisitesVerificationTests.cs SHA-256 FEEBCDEAC91B755517FD5B3E725B0FCCF02BAE64A397E8FC3A9BA28243E4A036
- Production assertions: production source under repo://src has no production driver runtime tokens, no forbidden Core dependencies, and no stub markers in bundle://proof/shared/transcripts/source-scans.txt.
- Red-team negative case: Rejects mutation, command, write, external-call, transition, claim, finalizer, and retry operations.
- Downstream dependency check: Unlocks audit and redaction phases.
