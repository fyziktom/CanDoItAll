# SB021 Semantic Invariants

- Invariant ID: SB021_INV_001
- Source raw note: REQ-007 .NET/Rust verifier alpha lane preparation
- Expected behavior: .NET/Rust lane inspects existing transcripts and classifies diagnostics without executing commands or writing files.
- Disallowed shallow implementation: Checking transcript text while allowing dotnet or shell execution.
- Failing-first test: N/A - no production behavior change; policy-denial negative cases execute in the passing test transcript.
- Passing test: Process_driver_prerequisites_SB021_INV_001_make_dotnet_rust_transcript_lane_readonly with transcript bundle://proof/shared/transcripts/focused-prerequisite-tests.txt
- Changed source files: repo://tests/CanDoItAll.Tests.Unit/ProcessDriverContractPrerequisitesVerificationTests.cs SHA-256 FEEBCDEAC91B755517FD5B3E725B0FCCF02BAE64A397E8FC3A9BA28243E4A036
- Production assertions: production source under repo://src has no production driver runtime tokens, no forbidden Core dependencies, and no stub markers in bundle://proof/shared/transcripts/source-scans.txt.
- Red-team negative case: Rejects command execution and workspace/storage writes.
- Downstream dependency check: Unlocks descriptor consumer boundary work.
