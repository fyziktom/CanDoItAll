# SB006 Semantic Invariants

- Invariant ID: SB006_INV_001
- Source raw note: REQ-002 deterministic dependency-clean Core
- Expected behavior: Core public API stays owner-governed and dependency-clean.
- Disallowed shallow implementation: Counting public types without checking dependency and governance rules.
- Failing-first test: N/A - no production behavior change; policy-denial negative cases execute in the passing test transcript.
- Passing test: Process_driver_prerequisites_SB006_INV_001_keep_core_public_api_governed_and_dependency_clean with transcript bundle://proof/shared/transcripts/focused-prerequisite-tests.txt
- Changed source files: repo://tests/CanDoItAll.Tests.Unit/ProcessDriverContractPrerequisitesVerificationTests.cs SHA-256 FEEBCDEAC91B755517FD5B3E725B0FCCF02BAE64A397E8FC3A9BA28243E4A036
- Production assertions: production source under repo://src has no production driver runtime tokens, no forbidden Core dependencies, and no stub markers in bundle://proof/shared/transcripts/source-scans.txt.
- Red-team negative case: Rejects forbidden Core dependencies and missing governance language.
- Downstream dependency check: Unlocks permission and capability phases.
