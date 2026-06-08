# SB036 Semantic Invariants

- Invariant ID: SB036_INV_001
- Source raw note: Prepare safe process integration handoff points without wiring runtime host
- Expected behavior: Process module keeps only the read-only transcript adapter; no DI/runtime selector/registry wiring appears in project or source.
- Disallowed shallow implementation: Returning a success flag, status row, or template text without enforcing the read-only evidence boundary and without command/source proof is not enough.
- Failing-first test: N/A process non-production integration-readiness closure
- Passing test: bundle://proof/SB024/transcripts/process-transcript-readonly-adapter-integration-tests.txt
- Changed source files: bundle://proof/SB045/changed-file-hashes.md
- Production assertions: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessTranscriptVerificationReadOnlyAdapter.cs and repo://src/CanDoItAll.Modules.Processes/CanDoItAll.Modules.Processes.csproj; source audit bundle://proof/SB045/transcripts/source-boundary-and-anti-stub-audit-after-uri-policy.txt
- Red-team negative case: N/A process non-production integration-readiness closure; source audit confirms no runtime, DI, file/network, UI/media, TODO, or NotImplemented drift.
- Downstream dependency check: P12 downstream security hardening checked by redaction and URI negative tests.

## Notes
- Integration readiness without wiring closed with repo:// source references and bundle:// proof transcripts.
