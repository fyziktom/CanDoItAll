# SB015 Semantic Invariants

- Invariant ID: SB015_INV_001
- Source raw note: REQ-005 sandbox and command denial policy
- Expected behavior: Current sandbox policy is denial-only, while future execution prerequisites are enumerated.
- Disallowed shallow implementation: Listing sandbox fields without proving commands and writes are denied.
- Failing-first test: N/A - no production behavior change; policy-denial negative cases execute in the passing test transcript.
- Passing test: Process_driver_prerequisites_SB015_INV_001_keep_command_and_sandbox_policy_denial_only with transcript bundle://proof/shared/transcripts/focused-prerequisite-tests.txt
- Changed source files: repo://tests/CanDoItAll.Tests.Unit/ProcessDriverContractPrerequisitesVerificationTests.cs SHA-256 FEEBCDEAC91B755517FD5B3E725B0FCCF02BAE64A397E8FC3A9BA28243E4A036
- Production assertions: production source under repo://src has no production driver runtime tokens, no forbidden Core dependencies, and no stub markers in bundle://proof/shared/transcripts/source-scans.txt.
- Red-team negative case: Rejects command execution, package restore, Office calls, writes, transitions, and finalizer application.
- Downstream dependency check: Unlocks verification-only contract rehearsal.
