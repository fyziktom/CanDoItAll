# SB051 Semantic Invariants

## SB051_INV_001 Malicious Corpus Does Not Leak Through Readback
- Source raw note: SB049 requires a malicious payload and secret corpus.
- Expected behavior: access tokens, bearer tokens, passwords, generic secrets, email addresses, and connection strings are redacted before they can appear in diagnostics, audit facts, manager readback JSON, stored audit requester fields, or audit hashes.
- Disallowed shallow implementation: direct redactor-only proof with no verification/readback path, or tests that only assert one secret shape.
- Positive proof: `bundle://proof/SB049/transcripts/malicious-secret-corpus-focused-tests.txt`.
- Source proof: `bundle://proof/SB049/transcripts/malicious-secret-corpus-source-assertions.txt`.
- Red-team negative case: `bundle://proof/SB051/transcripts/red-team-security-redaction-shallow-proof-rejection.txt`.

## SB051_INV_002 Audit And Redaction Matrix Remains Typed
- Source raw note: SB050 requires audit/redaction/non-leak matrix proof.
- Expected behavior: redaction emits typed `ProcessDriverRedactionKind` values, SHA-256 redacted text hashes, observation hashes, audit counts, and mutation-denial flags.
- Disallowed shallow implementation: string-only redaction, missing hash proof, in-memory-only audit proof, or requester secret persistence.
- Positive proof: `bundle://proof/SB050/transcripts/audit-redaction-non-leak-matrix-focused-tests.txt`.
- Production source scan: `bundle://proof/SB050/transcripts/production-secret-fragment-source-scan.txt`.

## SB051_INV_003 Security Hardening Does Not Expand Runtime Authority
- Expected behavior: redaction/security changes do not add runtime host/registry/selector/manager hooks, external calls, workspace writes, storage writes, or process mutation permissions.
- Disallowed shallow implementation: security wrapper that secretly enables runtime execution or broadens driver capabilities.
- Boundary proof: `bundle://proof/SB051/transcripts/gate-q-security-boundary-source-scan.txt`.
- Anti-stub audit: `bundle://proof/SB051/transcripts/gate-q-security-anti-stub-audit.txt`.
- Downstream dependency check: release-candidate and operator-smoke gates must preserve non-leak and no-mutation assertions.

## Production Behavior Artifact Matrix
| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Malicious secret corpus | SB049 focused test | Manager readback JSON and diagnostics consume redacted output | Gate Q focused rollup | Red-team rejects direct-redactor-only proof |
| Audit redaction matrix | `ProcessDriverRedactionPolicy` and `EfCoreProcessVerificationAuditStore` | Durable audit and readback tests | SB050 transcript | Production source scan rejects raw corpus fragments |
| Security no-authority boundary | Gate Q boundary source scan | Existing host/readback APIs stay mutation-free | Gate Q proof index | Anti-stub audit rejects report-only closure |

## Gate Result
Gate Q is semantically adequate for security hardening. The malicious corpus is redacted across diagnostics, audit, and readback, production source does not contain raw corpus fragments, and no runtime authority was expanded.
