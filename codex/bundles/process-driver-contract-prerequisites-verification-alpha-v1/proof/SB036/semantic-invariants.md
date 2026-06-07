# SB036 Semantic Invariants

- Invariant ID: SB036_INV_001
- Source raw note: REQ-012 long-range domain driver roadmap
- Expected behavior: Roadmap keeps domain drivers future-scoped and starts with read-only transcript inspection.
- Disallowed shallow implementation: Listing milestones while sneaking runtime implementation into this bundle.
- Failing-first test: N/A - no production behavior change; policy-denial negative cases execute in the passing test transcript.
- Passing test: Process_driver_prerequisites_SB036_INV_001_keep_domain_driver_roadmap_consistent_with_deferred_runtime with transcript bundle://proof/shared/transcripts/focused-prerequisite-tests.txt
- Changed source files: repo://tests/CanDoItAll.Tests.Unit/ProcessDriverContractPrerequisitesVerificationTests.cs SHA-256 FEEBCDEAC91B755517FD5B3E725B0FCCF02BAE64A397E8FC3A9BA28243E4A036
- Production assertions: production source under repo://src has no production driver runtime tokens, no forbidden Core dependencies, and no stub markers in bundle://proof/shared/transcripts/source-scans.txt.
- Red-team negative case: Rejects production runtime, shell-command, and workspace-write instructions in this bundle.
- Downstream dependency check: Unlocks broad smoke, red-team, and final closure.
