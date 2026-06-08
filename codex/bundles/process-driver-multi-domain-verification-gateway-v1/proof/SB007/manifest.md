# SB007 Proof Manifest

## Status
- Subbundle: `SB007`
- Status: `Completed`
- Owned requirement: `REQ-003`
- Scope result: Core public API ownership and descriptor compatibility are captured in a bundle-owned snapshot and guarded by focused reflection tests.

## Changed File Hashes
| File | SHA-256 |
| --- | --- |
| `repo://codex/bundles/process-driver-multi-domain-verification-gateway-v1/architecture/06-core-public-api-owner-classification.md` | `42b5c11cd28486d259113f0c0f406a63386332cd12f42cf547972457ee9fb113` |
| `repo://tests/CanDoItAll.Tests.Unit/ProcessDriverContractApiVerificationBoundaryTests.cs` | `08cea57790bbb65214e0b1ad7c626b8062ebc7793a988b5e0818c63cbcc29e54` |
| `repo://codex/bundles/process-driver-multi-domain-verification-gateway-v1/subbundles/sb007-refresh-core-public-api-owner-classification-and-descriptor-compatibil/README.md` | `0585b3df1758398df29365d89ff5091cc236c201b6647c8c40bfd8e1f796f3b7` |
| `repo://codex/bundles/process-driver-multi-domain-verification-gateway-v1/reviews/01-execution-report.md` | `a3a6e0398090b05b14706663071b84e7f8bf8deb55e1c7d289f5930c0c8ac69e` |

## Command Transcripts
- Focused contract boundary tests: `bundle://proof/SB007/transcripts/focused-contract-api-boundary-after-core-snapshot.txt`
- Source/no-drift/anti-stub audit: `bundle://proof/SB007/transcripts/core-snapshot-source-scan-and-anti-stub-audit.txt`

## Source Assertions
- Core public API snapshot documents 64 reflected public types and ordinal surface hash `99e2a6a6033d749f388a440360e4ef6db5b92c1d1fb2949a9f22d321ccd606d1`.
- Namespace ownership is explicit for artifacts, diagnostics, execution, finalization, routing, and subprocess rule families.
- Core dependency boundary remains unchanged: only `CanDoItAll.Processes.Contracts` is referenced.
- No production Process Core source files changed.

## Validation Results
- Focused contract API boundary tests passed: 10 passed, 0 failed, 0 skipped.
- Source/no-drift/anti-stub audit passed.
- No UI/media drift occurred.

## Closure Gate
- Entry gate: passed after SB006.
- Closure gate: passed.
- Progression decision: SB008 may proceed.
