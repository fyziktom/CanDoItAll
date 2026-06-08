# SB018 Semantic Invariants

- Invariant ID: SB018_INV_001
- Source raw note: Add runtime evidence consistency verifier alpha as read-only diagnostics only
- Expected behavior: Runtime verifier detects finalizer, retry, provider repair, no-progress fingerprint, and projection-order contradictions without mutation.
- Disallowed shallow implementation: Returning a success flag, status row, or template text without enforcing the read-only evidence boundary and without command/source proof is not enough.
- Failing-first test: bundle://proof/SB018/transcripts/focused-runtime-evidence-consistency-tests-after-uri-policy.txt
- Passing test: bundle://proof/SB018/transcripts/focused-runtime-evidence-consistency-tests-after-uri-policy-fix.txt
- Changed source files: bundle://proof/SB045/changed-file-hashes.md
- Production assertions: repo://src/CanDoItAll.Processes.Drivers.RuntimeEvidence/RuntimeEvidenceConsistencyAlphaVerifier.cs; source audit bundle://proof/SB045/transcripts/source-boundary-and-anti-stub-audit-after-uri-policy.txt
- Red-team negative case: bundle://proof/SB018/transcripts/focused-runtime-evidence-consistency-tests-after-uri-policy.txt; source audit confirms no runtime, DI, file/network, UI/media, TODO, or NotImplemented drift.
- Downstream dependency check: P06 downstream Core compatibility checked by SB021 and SB024 tests.

## Notes
- Runtime evidence consistency verifier closed with repo:// source references and bundle:// proof transcripts.
