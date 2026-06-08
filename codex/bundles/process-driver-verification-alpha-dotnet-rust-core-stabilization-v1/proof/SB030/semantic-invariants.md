# SB030 Semantic Invariants

- Invariant ID: `SB030-INV-001`
- Source raw note: review latest Codex work, move toward stable Process Core with domain drivers, add coherent phases, and prepare a zip bundle.
- Expected behavior: the verifier alpha stays read-only over supplied .NET/Rust transcripts and returns diagnostics, evidence references, redaction metadata, audit facts, and no-mutation proof without runtime wiring.
- Disallowed shallow implementation: status-only rows, fixture-only parsing, missing audit facts, unredacted sensitive text, missing hash checks, or any runtime/IO/DI hook.
- Failing-first test: bundle://proof/SB012/transcripts/failing-first-alpha-tests.txt
- Passing test: bundle://proof/SB012/transcripts/passing-alpha-tests.txt plus bundle://proof/SB040/transcripts/passing-full-unit-tests.txt
- Changed source files: repo://src/CanDoItAll.Processes.Drivers.TranscriptVerification/TranscriptVerificationAlphaVerifier.cs; repo://src/CanDoItAll.Processes.Drivers.Abstractions/Verification/ProcessDriverVerificationResponse.cs; bundle://proof/changed-file-hashes.txt
- Production assertions: source scan bundle://proof/SB041/transcripts/passing-source-scans.txt proves no alpha runtime tokens, no Core reverse dependency, no process-module hook, no stubs, and no UI/media drift.
- Red-team negative case: bundle://proof/SB043/red-team-fake-proof-review.md rejects prose-only and table-only fake closure.
- Downstream dependency check: bundle://proof/SB041/transcripts/passing-source-scans.txt includes every critical invariant ID and validates dependent phase assumptions.
