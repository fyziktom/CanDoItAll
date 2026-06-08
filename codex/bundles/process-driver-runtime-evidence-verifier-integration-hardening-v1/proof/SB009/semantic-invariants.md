# SB009 Semantic Invariants

- Invariant ID: SB009_INV_001
- Source raw note: Harden supplied evidence URI/hash policy and deny file/network access
- Expected behavior: Shared evidence policy normalizes hashes and denies local or network evidence URI schemes before verifier parsing.
- Disallowed shallow implementation: Returning a success flag, status row, or template text without enforcing the read-only evidence boundary and without command/source proof is not enough.
- Failing-first test: N/A process non-production closure
- Passing test: bundle://proof/SB024/transcripts/focused-transcript-tests-after-shared-uri-policy-overload.txt
- Changed source files: bundle://proof/SB045/changed-file-hashes.md
- Production assertions: repo://src/CanDoItAll.Processes.Drivers.Abstractions/Evidence/ProcessDriverEvidencePolicy.cs and repo://src/CanDoItAll.Processes.Drivers.TranscriptVerification/TranscriptVerificationRequestPolicy.cs; source audit bundle://proof/SB045/transcripts/source-boundary-and-anti-stub-audit-after-uri-policy.txt
- Red-team negative case: N/A process non-production closure; source audit confirms no runtime, DI, file/network, UI/media, TODO, or NotImplemented drift.
- Downstream dependency check: P03 downstream audit and adapter tests checked by SB012 and SB015 proof.

## Notes
- Evidence URI and hash boundary closed with repo:// source references and bundle:// proof transcripts.
