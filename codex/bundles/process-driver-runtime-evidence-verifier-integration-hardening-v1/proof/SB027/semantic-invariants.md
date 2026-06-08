# SB027 Semantic Invariants

- Invariant ID: SB027_INV_001
- Source raw note: Prepare Office/business lanes with denial-first read-only contracts
- Expected behavior: Non-.NET/Rust read-only lanes are denied by the adapter while future domain lanes remain documentation-only.
- Disallowed shallow implementation: Returning a success flag, status row, or template text without enforcing the read-only evidence boundary and without command/source proof is not enough.
- Failing-first test: N/A process non-production domain-lane closure
- Passing test: bundle://proof/SB024/transcripts/process-transcript-readonly-adapter-integration-tests.txt
- Changed source files: bundle://proof/SB045/changed-file-hashes.md
- Production assertions: repo://tests/CanDoItAll.Tests.Integration/ProcessTranscriptVerificationReadOnlyAdapterTests.cs and repo://codex/bundles/process-driver-runtime-evidence-verifier-integration-hardening-v1/architecture/05-driver-domain-roadmap.md; source audit bundle://proof/SB045/transcripts/source-boundary-and-anti-stub-audit-after-uri-policy.txt
- Red-team negative case: N/A process non-production domain-lane closure; source audit confirms no runtime, DI, file/network, UI/media, TODO, or NotImplemented drift.
- Downstream dependency check: P09 downstream harness and roadmap proof checked by SB030 and SB033.

## Notes
- Domain read-only lane denial closed with repo:// source references and bundle:// proof transcripts.
