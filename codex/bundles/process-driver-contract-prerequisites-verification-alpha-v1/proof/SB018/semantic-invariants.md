# SB018 Semantic Invariants

- Invariant ID: SB018_INV_001
- Source raw note: REQ-006 verification-only rehearsal without production runtime
- Expected behavior: Test-only request and response shape returns diagnostics from existing evidence with no mutation flag set.
- Disallowed shallow implementation: Adding a production interface or DI registration and calling it rehearsal.
- Failing-first test: N/A - no production behavior change; policy-denial negative cases execute in the passing test transcript.
- Passing test: Process_driver_prerequisites_SB018_INV_001_rehearse_verification_contract_without_production_runtime_api with transcript bundle://proof/shared/transcripts/focused-prerequisite-tests.txt
- Changed source files: repo://tests/CanDoItAll.Tests.Unit/ProcessDriverContractPrerequisitesVerificationTests.cs SHA-256 FEEBCDEAC91B755517FD5B3E725B0FCCF02BAE64A397E8FC3A9BA28243E4A036
- Production assertions: production source under repo://src has no production driver runtime tokens, no forbidden Core dependencies, and no stub markers in bundle://proof/shared/transcripts/source-scans.txt.
- Red-team negative case: Rejects production driver API, registry, DI, and selector tokens.
- Downstream dependency check: Unlocks transcript verifier preparation.
