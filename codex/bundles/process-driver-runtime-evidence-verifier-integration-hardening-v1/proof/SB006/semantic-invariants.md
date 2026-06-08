# SB006 Semantic Invariants

- Invariant ID: SB006_INV_001
- Source raw note: Decompose transcript verifier internals without behavior drift
- Expected behavior: Transcript verifier delegates parser, request policy, diagnostics, and audit fact construction while preserving .NET and Rust diagnostics.
- Disallowed shallow implementation: Returning a success flag, status row, or template text without enforcing the read-only evidence boundary and without command/source proof is not enough.
- Failing-first test: bundle://proof/SB006/transcripts/focused-transcript-tests-after-parser-policy-refactor.txt
- Passing test: bundle://proof/SB024/transcripts/focused-transcript-tests-after-shared-uri-policy-overload.txt
- Changed source files: bundle://proof/SB045/changed-file-hashes.md
- Production assertions: repo://src/CanDoItAll.Processes.Drivers.TranscriptVerification/TranscriptVerificationAlphaVerifier.cs and repo://src/CanDoItAll.Processes.Drivers.TranscriptVerification/TranscriptDiagnosticParsers.cs; source audit bundle://proof/SB045/transcripts/source-boundary-and-anti-stub-audit-after-uri-policy.txt
- Red-team negative case: bundle://proof/SB006/transcripts/focused-transcript-tests-after-parser-policy-refactor.txt; source audit confirms no runtime, DI, file/network, UI/media, TODO, or NotImplemented drift.
- Downstream dependency check: P02 downstream evidence hardening checked by SB024 URI-policy tests.

## Notes
- Parser and policy decomposition closed with repo:// source references and bundle:// proof transcripts.
