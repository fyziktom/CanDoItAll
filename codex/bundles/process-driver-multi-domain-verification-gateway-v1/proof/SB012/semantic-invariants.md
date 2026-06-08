# SB012 Semantic Invariants

## SB012_INV_001
- Invariant ID: `SB012_INV_001`
- Source raw note: `Prepare broader phases toward stable Core and domain drivers`.
- Expected behavior: Gate D can close only when transcript verifier parity, malicious corpus handling, redaction, supplied evidence URI/hash policy, and no-mutation behavior are proven by build/test/source-scan artifacts.
- Disallowed shallow implementation: status-only report rows, non-empty diagnostic-only claims, fixture-only parsing, malicious corpus omitted, or security scans that ignore file/directory/process/HTTP/workspace/storage/DI/external integration surfaces.
- Failing-first test: `bundle://proof/SB012/transcripts/red-team-transcript-status-only-rejection.txt` rejects non-empty diagnostic/status-only closure.
- Passing test: `bundle://proof/SB012/transcripts/gate-d-proof-index.txt` verifies SB010/SB011 manifests, build, focused transcript tests, no-mutation scan, corpus coverage, and red-team rejection.
- Changed source files: `repo://src/CanDoItAll.Processes.Drivers.TranscriptVerification/TranscriptDiagnosticRules.cs`; `repo://src/CanDoItAll.Processes.Drivers.TranscriptVerification/TranscriptVerificationEvidencePolicy.cs`; `repo://src/CanDoItAll.Processes.Drivers.TranscriptVerification/TranscriptDiagnosticParsers.cs`; `repo://src/CanDoItAll.Processes.Drivers.TranscriptVerification/TranscriptVerificationAlphaVerifier.cs`; `repo://tests/CanDoItAll.Tests.Unit/ProcessDriverTranscriptVerificationAlphaTests.cs`.
- Production assertions: verifier responses set `NoMutationPerformed: true`; package source has no file, directory, process, HTTP, workspace, storage, DI, Modules, Infrastructure, AgentFramework, Graph, Office, Gmail, or DbContext surface.
- Security assertions: malicious corpus includes prompt injection, path-like text, secret-like text, oversized content, and mixed .NET/Rust markers; diagnostics and audit summaries do not leak malicious content.
- Adversarial negative case: report-only or diagnostic-only closure without corpus/security/no-mutation/build proof is rejected with simulated verifier exit code 1.
- Downstream dependency check: SB013 and later phases may proceed only from the SB012 transcript verifier security baseline; if focused transcript tests, corpus count, or no-mutation scans fail, downstream phases must reopen.

## Artifact Matrix
| Artifact | Role | Required signal |
| --- | --- | --- |
| `gate-d-solution-build-no-restore.txt` | Build proof | Solution build succeeds. |
| `gate-d-focused-transcript-verifier-tests.txt` | Behavioral proof | Transcript verifier focused tests pass. |
| `gate-d-transcript-security-no-mutation-scan.txt` | Security/source proof | Transcript verifier remains supplied-text-only, redacted, and no-mutation. |
| `red-team-transcript-status-only-rejection.txt` | Adversarial proof | Status-only transcript closure is rejected. |
| `gate-d-proof-index.txt` | Positive proof index | Gate D proof artifacts and upstream manifests are verified. |
