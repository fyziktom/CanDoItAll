# SB012 Semantic Invariants

- Invariant ID: SB012_INV_001
- Source raw note: Harden audit/redaction/no-mutation response semantics
- Expected behavior: Redaction policy masks secrets and emails, and audit facts retain deterministic output hashes with NoMutationPerformed true.
- Disallowed shallow implementation: Returning a success flag, status row, or template text without enforcing the read-only evidence boundary and without command/source proof is not enough.
- Failing-first test: N/A process non-production closure
- Passing test: bundle://proof/SB024/transcripts/focused-transcript-tests-after-shared-uri-policy-overload.txt
- Changed source files: bundle://proof/SB045/changed-file-hashes.md
- Production assertions: repo://src/CanDoItAll.Processes.Drivers.Abstractions/Audit/ProcessDriverRedactionPolicy.cs and repo://src/CanDoItAll.Processes.Drivers.TranscriptVerification/TranscriptVerificationAuditFactBuilder.cs; source audit bundle://proof/SB045/transcripts/source-boundary-and-anti-stub-audit-after-uri-policy.txt
- Red-team negative case: N/A process non-production closure; source audit confirms no runtime, DI, file/network, UI/media, TODO, or NotImplemented drift.
- Downstream dependency check: P04 downstream process adapter integration checked by SB015 proof.

## Notes
- Audit, redaction, and no-mutation semantics closed with repo:// source references and bundle:// proof transcripts.
