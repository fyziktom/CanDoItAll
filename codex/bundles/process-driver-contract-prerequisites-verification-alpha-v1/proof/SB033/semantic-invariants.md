# SB033 Semantic Invariants

- Invariant ID: SB033_INV_001
- Source raw note: REQ-011 Core docs and compatibility roadmap
- Expected behavior: Core docs describe deterministic descriptors and compatibility governance without claiming runtime ownership.
- Disallowed shallow implementation: Writing broad docs that imply Core owns runtime dispatch or process mutation.
- Failing-first test: N/A - no production behavior change; policy-denial negative cases execute in the passing test transcript.
- Passing test: Process_driver_prerequisites_SB033_INV_001_document_core_package_rules_without_broad_runtime_ownership with transcript bundle://proof/shared/transcripts/focused-prerequisite-tests.txt
- Changed source files: repo://tests/CanDoItAll.Tests.Unit/ProcessDriverContractPrerequisitesVerificationTests.cs SHA-256 FEEBCDEAC91B755517FD5B3E725B0FCCF02BAE64A397E8FC3A9BA28243E4A036
- Production assertions: production source under repo://src has no production driver runtime tokens, no forbidden Core dependencies, and no stub markers in bundle://proof/shared/transcripts/source-scans.txt.
- Red-team negative case: Rejects docs that say Core owns mutation, finalizer application, or runtime dispatch.
- Downstream dependency check: Unlocks long-range roadmap work.
