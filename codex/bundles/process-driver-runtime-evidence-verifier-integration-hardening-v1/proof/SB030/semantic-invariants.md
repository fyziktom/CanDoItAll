# SB030 Semantic Invariants

- Invariant ID: SB030_INV_001
- Source raw note: Avoid duplicating unsafe logic across future domain driver packages
- Expected behavior: Shared evidence, redaction, and request policies provide a reusable verifier shape without runtime registration.
- Disallowed shallow implementation: Returning a success flag, status row, or template text without enforcing the read-only evidence boundary and without command/source proof is not enough.
- Failing-first test: N/A process non-production harness closure
- Passing test: bundle://proof/SB045/transcripts/source-boundary-and-anti-stub-audit-after-uri-policy.txt
- Changed source files: bundle://proof/SB045/changed-file-hashes.md
- Production assertions: repo://src/CanDoItAll.Processes.Drivers.Abstractions/Evidence/ProcessDriverEvidencePolicy.cs and repo://tests/CanDoItAll.Tests.Unit/ProcessDriverRuntimeEvidenceConsistencyAlphaTests.cs; source audit bundle://proof/SB045/transcripts/source-boundary-and-anti-stub-audit-after-uri-policy.txt
- Red-team negative case: N/A process non-production harness closure; source audit confirms no runtime, DI, file/network, UI/media, TODO, or NotImplemented drift.
- Downstream dependency check: P10 downstream runtime-host deferral checked by SB033 docs test.

## Notes
- Reusable verifier package shape closed with repo:// source references and bundle:// proof transcripts.
