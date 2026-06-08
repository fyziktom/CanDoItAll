# SB003 Semantic Invariants

- Invariant ID: SB003_INV_001
- Source raw note: Verify after Codex crash from real code
- Expected behavior: Crash recovery guardrails retarget stale bundle references, rerun focused tests, and prove no hidden runtime or UI drift.
- Disallowed shallow implementation: Returning a success flag, status row, or template text without enforcing the read-only evidence boundary and without command/source proof is not enough.
- Failing-first test: bundle://proof/SB001/transcripts/focused-transcript-alpha-tests.txt
- Passing test: bundle://proof/SB002/transcripts/process-driver-contract-prerequisites-after-runtime-evidence-refactor.txt
- Changed source files: bundle://proof/SB045/changed-file-hashes.md
- Production assertions: repo://tests/CanDoItAll.Tests.Unit/ProcessDriverContractPrerequisitesVerificationTests.cs and repo://tests/CanDoItAll.Tests.Unit/ProcessDriverTranscriptVerificationAlphaTests.cs; source audit bundle://proof/SB045/transcripts/source-boundary-and-anti-stub-audit-after-uri-policy.txt
- Red-team negative case: bundle://proof/SB001/transcripts/focused-transcript-alpha-tests.txt; source audit confirms no runtime, DI, file/network, UI/media, TODO, or NotImplemented drift.
- Downstream dependency check: P01 downstream parser and evidence work checked by SB006 and SB009 focused tests.

## Notes
- Crash recovery and stale proof repair closed with repo:// source references and bundle:// proof transcripts.
