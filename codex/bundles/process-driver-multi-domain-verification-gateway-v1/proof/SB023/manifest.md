# SB023 Proof Manifest

## Status
- Subbundle: `SB023`
- Status: `Completed`
- Owned requirement: `REQ-008`
- Scope result: transcript and runtime evidence read-only drivers now enforce supplied-content URI, hash, size, and content-type policies before semantic parsing.

## Changed File Hashes
| File | SHA-256 |
| --- | --- |
| `repo://src/CanDoItAll.Processes.Drivers.Abstractions/Evidence/ProcessDriverSuppliedEvidenceContentRules.cs` | `98b5875e8af5d4a3df9cc64c275f4d8019616d4271b0bb2f6a6d1880f845611d` |
| `repo://src/CanDoItAll.Processes.Drivers.TranscriptVerification/TranscriptVerificationRequestPolicy.cs` | `0ceeed65b19a759241731ead06e8b7cc03c5f43e6f1c8e208559960e1e3e851f` |
| `repo://src/CanDoItAll.Processes.Drivers.RuntimeEvidence/RuntimeEvidenceVerificationRequestPolicy.cs` | `50418373271042f7777162b572b1411617935af5e86eacd636829eb3e17cce49` |
| `repo://tests/CanDoItAll.Tests.Unit/ProcessDriverContractApiVerificationBoundaryTests.cs` | `e16f5f71c57a3ef511d68aad2a15c52b88962191643a8618692bb378d53a76d4` |
| `repo://tests/CanDoItAll.Tests.Unit/ProcessDriverTranscriptVerificationAlphaTests.cs` | `cd4c6b3b158e7d87c6e1da05b48882dc950094176f5a9fc23acc1dbe9595f3be` |
| `repo://tests/CanDoItAll.Tests.Unit/ProcessDriverRuntimeEvidenceConsistencyAlphaTests.cs` | `5ddf76886e23b997a304533bd7f2c70f0d4745c16e5946f22ee992133b8a7f9c` |
| `repo://codex/bundles/process-driver-multi-domain-verification-gateway-v1/subbundles/sb023-enforce-uri-hash-size-content-type-policies-and-denial-diagnostics-acr/README.md` | `3a86deb1c5324cd9b405b77856fc6bbdba44c6d52a7d750929b1d4a42a2e70ce` |
| `repo://codex/bundles/process-driver-multi-domain-verification-gateway-v1/reviews/01-execution-report.md` | `94c23a267b52982d7b15dc31eac1f9698b76fd4e47b2a0d870c8d7fb1ba8e86d` |

## Command Transcripts
- Focused evidence-policy tests: `bundle://proof/SB023/transcripts/focused-evidence-policy-tests.txt`
- Source/no-drift/anti-stub audit: `bundle://proof/SB023/transcripts/evidence-policy-source-scan-and-anti-stub-audit.txt`

## Source Assertions
- `ProcessDriverSuppliedEvidenceContentRules` now defines bounded size and hash-binding checks for supplied evidence content envelopes.
- Transcript verification rejects unapproved supplied-content evidence URIs, invalid transcript content type, invalid size, invalid hash, and content/reference hash mismatch with explicit denial diagnostics.
- Runtime evidence verification rejects unapproved supplied-content evidence URIs, invalid Core descriptor content type, invalid size, invalid hash, and content/reference hash mismatch with explicit denial diagnostics.
- Denial categories are explicit and stable: `TranscriptUntrusted`, `InsufficientProof`, and `EvidenceHashMismatch`.
- Focused tests exercise the verifier entry points and assert mutation-free denials for untrusted, mismatched, oversized, and invalid-content-type envelopes.
- No runtime host, registry, selector, provider, DI, process, HTTP, file, directory, DbContext, manager-command, or UI/media behavior was added.

## Validation Results
- Focused evidence-policy tests passed: 32 passed, 0 failed, 0 skipped.
- Source/no-drift/anti-stub audit passed.
- No UI/media drift occurred.

## Closure Gate
- Entry gate: passed after SB022.
- Closure gate: passed.
- Progression decision: SB024 Gate H may proceed.
