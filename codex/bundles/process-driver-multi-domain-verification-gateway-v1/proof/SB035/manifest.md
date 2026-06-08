# SB035 Proof Manifest

## Status
- Subbundle: `SB035`
- Status: `Completed`
- Owned requirement: `REQ-011`
- Scope result: Artifact-evidence alpha verifier now detects projection order drift, duplicate projection sources, missing lineage, trust/sensitivity mismatch, and satisfaction inconsistency over supplied Core descriptors and snapshots only.

## Changed File Hashes
| File | SHA-256 |
| --- | --- |
| `repo://src/CanDoItAll.Processes.Drivers.Abstractions/Verification/ProcessDriverDiagnostic.cs` | `03e0afcd300068fe9fa826bc8d85151d9915e222adc59ab3b203e05f878f1a80` |
| `repo://src/CanDoItAll.Processes.Drivers.Abstractions/Verification/ProcessDriverContractVersion.cs` | `ad5226500d9c8bb85c732a97cbc13a56fb2eb3b6178c233da74aa95b0a693730` |
| `repo://src/CanDoItAll.Processes.Drivers.ArtifactEvidence/ArtifactEvidenceAuditFactMapper.cs` | `5d6fb8dc66bd7a5a5b62cdce161c8c9ef901b557127a2f3df8adf3ad22818759` |
| `repo://src/CanDoItAll.Processes.Drivers.ArtifactEvidence/ArtifactEvidenceDiagnosticRules.cs` | `e7f6e2eeb08c978351b625202bc879f134130ef0a290e56c685303ec5f44a5a9` |
| `repo://src/CanDoItAll.Processes.Drivers.ArtifactEvidence/ArtifactEvidenceVerificationRequest.cs` | `50a82199fc213c2e5c5e258e08b49d4ab2575753e715dc587c97d3cbb34d7a8a` |
| `repo://tests/CanDoItAll.Tests.Unit/ProcessDriverArtifactEvidenceAlphaTests.cs` | `8d7b78dbae888954815a61817a15b81b6c52e794ee7c32d8d0bfdaf2bde54267` |
| `repo://tests/CanDoItAll.Tests.Unit/ProcessDriverContractApiVerificationBoundaryTests.cs` | `fad5f39b5bb966d92a3dc8aae2f0a0e7b450b8fea38eace0260fbab6503c3c3b` |
| `repo://codex/bundles/process-driver-multi-domain-verification-gateway-v1/architecture/07-driver-abstraction-api-versioning-snapshot.md` | `dec5998e8c6e8bca8b7ae9740afe3c9563443c27b3f8bf16d1ec9f1f7b937462` |
| `repo://codex/bundles/process-driver-multi-domain-verification-gateway-v1/architecture/08-explicit-verification-gateway-design.md` | `67ff53570feb0046c43cc0b9022c67a73c8ed8053281913de34c7027a9475535` |
| `repo://codex/bundles/process-driver-multi-domain-verification-gateway-v1/subbundles/sb035-detect-descriptor-contradictions-projection-order-drift-missing-lineag/README.md` | `a5c009afcdf89bbf6bf0910784cf7dbe8f34f689d34f3927acd5b2a036105f82` |
| `repo://codex/bundles/process-driver-multi-domain-verification-gateway-v1/README.md` | `14cc2075a6214e1f8ffa90122b92f12f1b6d76b82ea9d428f1fcb51ea35a1e3e` |
| `repo://codex/bundles/process-driver-multi-domain-verification-gateway-v1/reviews/01-execution-report.md` | `991c93e80f02f0ba1ccce478e449d38ce65f4e83d83c34afa3cf399097ef78df` |

## Command Transcripts
- Focused ArtifactEvidence contradiction and contract tests: `bundle://proof/SB035/transcripts/focused-artifact-evidence-contradiction-tests.txt`
- Source/no-drift/anti-stub audit: `bundle://proof/SB035/transcripts/artifact-evidence-contradiction-source-scan-and-anti-stub-audit.txt`

## Source Assertions
- `ArtifactEvidenceDiagnosticRules` uses Core `ProcessArtifactProjectionEvidenceDescriptorRules.IsDefaultProjectionOrder` to detect projection order drift and duplicate projection sources.
- `ArtifactEvidenceDiagnosticRules` detects supplied projection source-order entries that have no matching supplied lineage descriptor.
- `ArtifactEvidenceVerificationRequest` accepts supplied `ProcessArtifactExpectationSnapshot` and `ProcessArtifactRecordSnapshot` values for trust, sensitivity, and satisfaction checks without reading process storage.
- `ArtifactEvidenceDiagnosticRules` uses Core `ProcessArtifactExpectationMatcher.DiagnoseStrongExpectedArtifactMatch` and `ProcessArtifactExpectationSatisfactionRules.Diagnose` instead of duplicating satisfaction semantics.
- `ProcessDriverDiagnosticCategory` now has typed artifact diagnostics for missing lineage, trust/sensitivity mismatch, and satisfaction inconsistency.
- Driver abstraction contract version is `1.10.0`; public type count remains `34` and type-name hash remains `f92df2a77fbc8800345444c17edca2929f97328f9266dccb54d37bd4dd4781c5`.
- No runtime host, registry, selector, provider, DI, module, HTTP, process, file, directory, DbContext, workspace, storage, UI/media, secret-like, or stub behavior was added.

## Validation Results
- Focused ArtifactEvidence contradiction and contract tests passed: 18 passed, 0 failed, 0 skipped.
- Source/no-drift/anti-stub audit passed.
- No UI/media drift occurred.

## Closure Gate
- Entry gate: passed after SB034.
- Closure gate: passed.
- Progression decision: SB036 Gate L may proceed.
