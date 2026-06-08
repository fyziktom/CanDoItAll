# SB009 Semantic Invariants

## SB009_INV_001
- Invariant ID: `SB009_INV_001`
- Source raw note: `Prepare broader phases toward stable Core and domain drivers`.
- Expected behavior: Gate C can close only when Core and driver abstraction public API snapshots are refreshed from the live branch, focused API boundary tests pass, reverse-dependency scans prove the package graph remains clean, and runtime host/registry/selector/DI/manager-command surfaces remain absent.
- Disallowed shallow implementation: report-only status updates, non-empty diagnostics without command transcripts, fixture-only claims, snapshot hashes not tied to reflected public types, or dependency scans that ignore runtime-host tokens.
- Failing-first test: `bundle://proof/SB009/transcripts/red-team-report-only-api-governance-rejection.txt` rejects report-only API governance closure.
- Passing test: `bundle://proof/SB009/transcripts/gate-c-proof-index.txt` verifies SB007/SB008 manifests, snapshots, build, focused tests, reverse-dependency scan, runtime-host denial, and red-team rejection.
- Changed source files: `repo://tests/CanDoItAll.Tests.Unit/ProcessDriverContractApiVerificationBoundaryTests.cs`.
- Production assertions: no production Process Core or driver abstraction source changed; Core references Contracts only and has no driver/runtime-host tokens; driver abstractions have no project/package references and no runtime-host/DI/module/infrastructure tokens.
- Snapshot assertions: Core public surface count/hash are `64` and `99e2a6a6033d749f388a440360e4ef6db5b92c1d1fb2949a9f22d321ccd606d1`; driver abstraction count/hash/version are `28`, `2c4e557a2e0118a4a64f60f18830dd25c365ecca2939a3f4567084fb132be5fc`, and `1.2.0`.
- Adversarial negative case: report-only closure without reflected snapshots, reverse-dependency scan, focused tests, and runtime-host denial is rejected with simulated verifier exit code 1.
- Downstream dependency check: SB010 and later phases may proceed only from the SB009 snapshot hashes and package graph; if either public surface changes or dependency scan fails, downstream phases must reopen.

## Artifact Matrix
| Artifact | Role | Required signal |
| --- | --- | --- |
| `gate-c-solution-build-no-restore.txt` | Build proof | Solution build succeeds. |
| `gate-c-focused-contract-api-boundary-tests.txt` | Behavioral proof | API boundary tests pass. |
| `gate-c-api-governance-reverse-dependency-scan.txt` | Source proof | Public snapshots and dependency scans match live source. |
| `red-team-report-only-api-governance-rejection.txt` | Adversarial proof | Report-only API governance closure is rejected. |
| `gate-c-proof-index.txt` | Positive proof index | Gate C proof artifacts and upstream manifests are verified. |
