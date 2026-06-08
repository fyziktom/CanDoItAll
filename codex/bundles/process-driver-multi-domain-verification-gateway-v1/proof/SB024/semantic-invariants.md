# SB024 Semantic Invariants

## SB024_INV_001
- Invariant ID: `SB024_INV_001`
- Source raw note: `Prepare broader phases toward stable Core and domain drivers`.
- Expected behavior: Gate H can close only when transcript and runtime evidence read-only drivers deny untrusted, mismatched, oversized, and missing supplied-content envelopes before semantic parsing, while preserving mutation-free responses and diagnostic redaction/non-leak behavior.
- Disallowed shallow implementation: non-empty diagnostics from one bad request, content-type checks without URI/hash/size coverage, positive-only verifier tests, source scans that ignore runtime/IO/integration surfaces, or denial messages that leak rejected local paths or remote hosts.
- Failing-first test: `bundle://proof/SB024/transcripts/red-team-evidence-boundary-shallow-proof-rejection.txt` rejects closure without all four boundary denial families, mutation-free assertions, focused pass count, and no-side-effect source proof.
- Passing test: `bundle://proof/SB024/transcripts/gate-h-proof-index.txt` verifies SB022/SB023 manifests, clean build, 34/34 focused tests, Gate H source scan, red-team rejection, and this invariant contract.
- Changed source files: `repo://src/CanDoItAll.Processes.Drivers.Abstractions/Evidence/ProcessDriverSuppliedEvidenceContentRules.cs`; `repo://src/CanDoItAll.Processes.Drivers.TranscriptVerification/TranscriptVerificationRequestPolicy.cs`; `repo://src/CanDoItAll.Processes.Drivers.RuntimeEvidence/RuntimeEvidenceVerificationRequestPolicy.cs`; `repo://tests/CanDoItAll.Tests.Unit/ProcessDriverTranscriptVerificationAlphaTests.cs`; `repo://tests/CanDoItAll.Tests.Unit/ProcessDriverRuntimeEvidenceConsistencyAlphaTests.cs`; `repo://tests/CanDoItAll.Tests.Unit/ProcessDriverContractApiVerificationBoundaryTests.cs`.
- Production assertions: supplied-content envelopes are checked for lane-specific content kind, expected content type, approved URI, bounded positive size, valid SHA-256 hash, and evidence-reference hash binding before transcript parsing or runtime descriptor contradiction analysis.
- Security assertions: denial diagnostics do not include rejected local path or remote host details; source scan proves no runtime host, registry, selector, provider, DI, process, HTTP, file, directory, DbContext, manager command, workspace, storage, UI/media, or secret-like drift in Gate H targets.
- Adversarial negative case: a closure that relies on non-empty diagnostics and skips untrusted URI, mismatch, oversized, missing-content, no-mutation, or non-leak proof is rejected with simulated verifier exit code 1.
- Downstream dependency check: SB025 and later audit/redaction phases may proceed only from an evidence boundary that denies bad supplied content before any driver emits accepted diagnostics; if boundary checks or no-side-effect scans fail, reopen SB022-SB024.

## Production Behavior Artifact Matrix
| Artifact | Producer | Consumer | Lifecycle | Negative-test citation |
| --- | --- | --- | --- | --- |
| `ProcessDriverSuppliedEvidenceContent` | Process module read-only adapters and verifier callers | Transcript and runtime evidence request policies | Constructed from supplied in-memory payload material, validated before parsing, returned only as evidence metadata | `Process_driver_transcript_alpha_SB023_INV_001`; `Runtime_evidence_consistency_alpha_SB023_INV_001` |
| Supplied-content denial diagnostic | Transcript and runtime request policies | `ProcessDriverVerificationResponse.Diagnostics` and audit fact mapping | Emitted when supplied content URI, content type, size, hash, or binding is invalid; response remains mutation-free | `Process_driver_transcript_alpha_SB024_INV_003`; `Runtime_evidence_consistency_alpha_SB024_INV_001` |
| Mutation-free denied response | Transcript and runtime alpha verifiers | Gateway and downstream audit/redaction phases | Denial short-circuits semantic parsing and preserves `NoMutationPerformed: true` | `bundle://proof/SB024/transcripts/gate-h-focused-evidence-boundary-tests.txt` |
