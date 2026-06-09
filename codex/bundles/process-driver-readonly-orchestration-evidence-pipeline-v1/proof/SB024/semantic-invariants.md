# SB024 Semantic Invariants

## Invariant SB024-CROSS-LANE-NO-SECRET-NO-MUTATION-NO-MISMATCH
- Invariant ID: `SB024-CROSS-LANE-NO-SECRET-NO-MUTATION-NO-MISMATCH`
- Source raw note: `Stable Process Core with domain drivers`
- Expected behavior: All five read-only gateway lanes preserve a sealed response envelope: current contract version, valid evidence hashes, valid redaction hash, normalized audit facts, no mutation, no external calls, no workspace/storage writes, secret-free diagnostics/audit summaries, and explicit `EvidenceHashMismatch` denial for tampered supplied evidence hash bindings.
- Disallowed shallow implementation: Proving only one lane, asserting only `Accepted`, omitting audit facts, omitting redaction hash checks, omitting secret-fragment suppression, accepting hash mismatches as ordinary insufficient proof, adding runtime host/DI/file/storage/network behavior, or adding object/dynamic dispatch.
- Failing-first test: No genuine P08 production compile/test failure was produced; bundle://proof/SB024/transcripts/p08-source-scans.txt records the only failed pass as a README false-positive scan.
- Passing test: bundle://proof/SB024/transcripts/build-cross-lane-audit-redaction-hash.txt, bundle://proof/SB024/transcripts/focused-p08-gateway-harness-tests.txt, bundle://proof/SB024/transcripts/focused-p08-readonly-adapter-integration-tests.txt, bundle://proof/SB024/transcripts/full-unit-p08.txt, and bundle://proof/SB024/transcripts/p08-source-scans-fixed.txt
- Changed source files: repo://tests/CanDoItAll.Tests.Unit/ProcessDriverVerificationTestHarness.cs, repo://tests/CanDoItAll.Tests.Unit/ProcessDriverVerificationGatewayTests.cs
- Production assertions: Existing request policies still require typed supplied-content hash binding; the gateway test proves transcript, runtime, artifact, Office, and business lanes all deny tampered bindings with `EvidenceHashMismatch`.
- Red-team negative case: The P08 gateway test supplies malicious secret, access token, and email fragments across every lane and verifies diagnostics/audit summaries do not leak those fragments; the fixed source scan rejects runtime/DI, file/network/storage, dynamic dispatch, Core reverse dependency, UI/media drift, and stubs.
- Downstream dependency check: P09 may start because cross-lane audit/redaction/hash assertions now cover all five read-only lanes without introducing runtime or mutation-capable infrastructure.
