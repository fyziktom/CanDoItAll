# SB025 Proof Manifest

## Status
- Subbundle: `SB025`
- Status: `Completed`
- Owned requirement: `REQ-009`
- Scope result: audit facts now carry caller, explicit lane, requested operation, typed evidence references, denial reason, diagnostic summary, and output hash across transcript/runtime driver output and process transcript preflight denial output.

## Changed File Hashes
| File | SHA-256 |
| --- | --- |
| `repo://src/CanDoItAll.Processes.Drivers.Abstractions/Audit/ProcessDriverAuditFact.cs` | `18cc30f4780b747fcf20e8efa42a6f6c6a7717f2ab756e779a33d4a90ca5ebab` |
| `repo://src/CanDoItAll.Processes.Drivers.Abstractions/Verification/ProcessDriverContractVersion.cs` | `47ba247c8fafd15e9d829919ddf65c5a9f217162ab4ff28732006ba05ce349f0` |
| `repo://src/CanDoItAll.Processes.Drivers.TranscriptVerification/TranscriptVerificationAuditFactBuilder.cs` | `82b56949818b4c6554a37b38648b7742db2a0e54d54d7f1d8cfb0149ad33c20f` |
| `repo://src/CanDoItAll.Processes.Drivers.RuntimeEvidence/RuntimeEvidenceAuditFactMapper.cs` | `afeb7dc1bdcc8a05dbd8b8bcf171e3bebc5c852eaee4eb03d0818c36e31f4a33` |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessTranscriptVerificationPreflightPolicy.cs` | `6bddb879854452e4994cedb5339cbf4bca140184a6bfba24448ec673d0f94c49` |
| `repo://tests/CanDoItAll.Tests.Unit/ProcessDriverVerificationTestHarness.cs` | `5501b2b3015b1a346b8ee91e52a1509f73cb3dbd9da1648dd5ce0841654b750f` |
| `repo://tests/CanDoItAll.Tests.Unit/ProcessDriverVerificationTestHarnessTests.cs` | `40e494041282e3bdd22731c31fedce4ac74568e7c746ef91da408ae0528a2fcd` |
| `repo://tests/CanDoItAll.Tests.Unit/ProcessDriverContractApiVerificationBoundaryTests.cs` | `8b146f4863dd24fee2b5bf254b4747e3f91e16a446608ed978aefffa73a687cb` |
| `repo://tests/CanDoItAll.Tests.Unit/ProcessDriverTranscriptVerificationAlphaTests.cs` | `2d9993f8ca3e8808739fb05e19b153746a6823a15dbb044e6a765d39fbebeb98` |
| `repo://tests/CanDoItAll.Tests.Unit/ProcessDriverRuntimeEvidenceConsistencyAlphaTests.cs` | `f727d66b0be4e2c73ad9f6aed9add6240c8a8f29b632120198a4fee856769253` |
| `repo://codex/bundles/process-driver-multi-domain-verification-gateway-v1/architecture/07-driver-abstraction-api-versioning-snapshot.md` | `98c809062011def1bb46e2ecf3b8fe1ae2b18f10ac657d10564b19e013a9f23b` |
| `repo://codex/bundles/process-driver-multi-domain-verification-gateway-v1/architecture/08-explicit-verification-gateway-design.md` | `db0ec93faaa7a34de01e5521e12a1684d01dec7b893ca86733d93937c8918d49` |
| `repo://codex/bundles/process-driver-multi-domain-verification-gateway-v1/subbundles/sb025-normalize-audit-facts-across-all-drivers-include-caller-lane-operation/README.md` | `867c0cc55ee32452b04eb6c8a435700d994e02a0e82ab00fd3df196d0aafc363` |
| `repo://codex/bundles/process-driver-multi-domain-verification-gateway-v1/reviews/01-execution-report.md` | `4991a0ba8c27c8a61efa06039c6816141cab55f7bb2d83533fdc8bb6f70dfffc` |

## Command Transcripts
- Focused audit fact normalization tests: `bundle://proof/SB025/transcripts/focused-audit-fact-normalization-tests.txt`
- Source/no-drift/anti-stub audit: `bundle://proof/SB025/transcripts/audit-fact-normalization-source-scan-and-anti-stub-audit.txt`

## Source Assertions
- `ProcessDriverAuditFact` now exposes explicit `Lane` and typed `EvidenceReferences` fields.
- Transcript and runtime audit fact producers populate lane, requested operation, evidence references, denial reason, redaction, diagnostic summary, and output hash.
- Process transcript verification preflight denial output populates the same audit fact shape.
- Shared test harness assertions now require explicit lane, non-empty typed evidence references, and valid output hashes.
- Driver contract version is `1.5.0`; public driver-abstraction type count remains `34` with unchanged type-name surface hash.
- No runtime host, registry, selector, provider, DI, process, HTTP, file, directory, DbContext, manager-command, UI/media, or secret-like behavior was added.

## Validation Results
- Focused audit fact normalization tests passed: 38 passed, 0 failed, 0 skipped.
- Source/no-drift/anti-stub audit passed.
- No UI/media drift occurred.

## Closure Gate
- Entry gate: passed after SB024 Gate H.
- Closure gate: passed.
- Progression decision: SB026 may proceed.
