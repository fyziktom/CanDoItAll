# SB015 Semantic Invariants

- Invariant ID: SB015_INV_001
- Source raw note: Harden process read-only adapter and observation envelope
- Expected behavior: Process adapter reuses shared evidence and redaction policy, denies side effects preflight, and returns read-only observations.
- Disallowed shallow implementation: Returning a success flag, status row, or template text without enforcing the read-only evidence boundary and without command/source proof is not enough.
- Failing-first test: N/A process non-production closure
- Passing test: bundle://proof/SB024/transcripts/process-transcript-readonly-adapter-integration-tests.txt
- Changed source files: bundle://proof/SB045/changed-file-hashes.md
- Production assertions: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessTranscriptVerificationReadOnlyAdapter.cs; source audit bundle://proof/SB045/transcripts/source-boundary-and-anti-stub-audit-after-uri-policy.txt
- Red-team negative case: N/A process non-production closure; source audit confirms no runtime, DI, file/network, UI/media, TODO, or NotImplemented drift.
- Downstream dependency check: P05 downstream runtime evidence verifier tests checked by SB018 proof.

## Notes
- Process adapter evidence flow closed with repo:// source references and bundle:// proof transcripts.
