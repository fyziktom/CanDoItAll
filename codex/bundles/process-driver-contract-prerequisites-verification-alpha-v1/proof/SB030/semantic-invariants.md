# SB030 Semantic Invariants

- Invariant ID: SB030_INV_001
- Source raw note: REQ-010 production driver contract decision
- Expected behavior: Production driver contract remains deferred until every prerequisite and owner approval is green.
- Disallowed shallow implementation: Treating green test rows as production runtime approval.
- Failing-first test: N/A - no production behavior change; policy-denial negative cases execute in the passing test transcript.
- Passing test: Process_driver_prerequisites_SB030_INV_001_defer_production_driver_contract_until_all_prerequisites_are_green with transcript bundle://proof/shared/transcripts/focused-prerequisite-tests.txt
- Changed source files: repo://tests/CanDoItAll.Tests.Unit/ProcessDriverContractPrerequisitesVerificationTests.cs SHA-256 FEEBCDEAC91B755517FD5B3E725B0FCCF02BAE64A397E8FC3A9BA28243E4A036
- Production assertions: production source under repo://src has no production driver runtime tokens, no forbidden Core dependencies, and no stub markers in bundle://proof/shared/transcripts/source-scans.txt.
- Red-team negative case: Rejects approval when the follow-up bundle owner approval is missing.
- Downstream dependency check: Unlocks Core documentation and compatibility work.
