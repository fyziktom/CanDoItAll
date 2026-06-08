# SB015 Semantic Invariants

## SB015_INV_001
- Invariant ID: `SB015_INV_001`
- Source raw note: `Prepare broader phases toward stable Core and domain drivers`.
- Expected behavior: Gate E can close only when runtime evidence verifier parity, expanded contradiction coverage, no-mutation behavior, and no runtime-host surface are proven by build/test/source-scan artifacts.
- Disallowed shallow implementation: status-only rows, non-empty diagnostic-only claims, fixture-only parsing, report-only proof, missing SB013/SB014 manifests, or source scans that ignore process/file/directory/HTTP/workspace/storage/DI/runtime-host tokens.
- Failing-first test: `bundle://proof/SB015/transcripts/red-team-runtime-evidence-report-only-rejection.txt` rejects report-only or diagnostic-only closure without build, focused tests, no-side-effect scan, and upstream manifests.
- Passing test: `bundle://proof/SB015/transcripts/gate-e-proof-index.txt` verifies SB013/SB014 manifests, clean build, focused runtime evidence tests, no-side-effect scan, and red-team rejection.
- Changed source files: `repo://src/CanDoItAll.Processes.Drivers.RuntimeEvidence/RuntimeEvidenceConsistencyAlphaVerifier.cs`; `repo://src/CanDoItAll.Processes.Drivers.RuntimeEvidence/RuntimeEvidenceDescriptorNormalizer.cs`; `repo://src/CanDoItAll.Processes.Drivers.RuntimeEvidence/RuntimeEvidenceVerificationRequestPolicy.cs`; `repo://src/CanDoItAll.Processes.Drivers.RuntimeEvidence/RuntimeEvidenceContradictionRules.cs`; `repo://src/CanDoItAll.Processes.Drivers.RuntimeEvidence/RuntimeEvidenceDiagnosticFactory.cs`; `repo://src/CanDoItAll.Processes.Drivers.RuntimeEvidence/RuntimeEvidenceAuditFactMapper.cs`; `repo://tests/CanDoItAll.Tests.Unit/ProcessDriverRuntimeEvidenceConsistencyAlphaTests.cs`.
- Production assertions: verifier responses set `NoMutationPerformed: true`; the package source has no file, directory, process, HTTP, workspace, storage, DI, Modules, Infrastructure, AgentFramework, runtime host, driver registry, driver selector, or scheduler surface.
- Security assertions: contradiction diagnostics and audit facts are derived only from supplied descriptor evidence and bounded summaries; no arbitrary content fetch or state mutation is permitted.
- Adversarial negative case: report-only or diagnostic-only closure without source-backed no-side-effect proof is rejected with simulated verifier exit code 1.
- Downstream dependency check: SB016 and later phases may proceed only from the SB015 runtime evidence no-side-effect baseline; if focused runtime tests, contradiction matrix coverage, or no-side-effect scans fail, downstream phases must reopen.

## Artifact Matrix
| Artifact | Role | Required signal |
| --- | --- | --- |
| `gate-e-solution-build-no-restore.txt` | Build proof | Solution build succeeds. |
| `gate-e-focused-runtime-evidence-tests.txt` | Behavioral proof | Runtime evidence focused tests pass. |
| `gate-e-runtime-evidence-no-side-effect-scan.txt` | Source proof | Runtime evidence verifier remains deterministic and no-side-effect. |
| `red-team-runtime-evidence-report-only-rejection.txt` | Adversarial proof | Report-only runtime evidence closure is rejected. |
| `gate-e-proof-index.txt` | Positive proof index | Gate E proof artifacts and upstream manifests are verified. |
