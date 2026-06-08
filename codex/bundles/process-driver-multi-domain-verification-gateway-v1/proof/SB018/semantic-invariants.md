# SB018 Semantic Invariants

## SB018_INV_001
- Invariant ID: `SB018_INV_001`
- Source raw note: `Prepare broader phases toward stable Core and domain drivers`.
- Expected behavior: Gate F can close only when transcript and runtime verifier tests both adopt the shared harness while retaining focused coverage, passing tests, no skips, and no production runtime-host surface.
- Disallowed shallow implementation: report-only claims, helper-only code with no verifier adoption, skipped focused tests, reduced focused test counts, or source scans that ignore runtime host, DI, process, HTTP, file, directory, UI/media, and secret drift.
- Failing-first test: `bundle://proof/SB018/transcripts/red-team-harness-adoption-report-only-rejection.txt` rejects report-only closure without build, focused tests, adoption scan, no-weakening proof, and upstream manifests.
- Passing test: `bundle://proof/SB018/transcripts/gate-f-proof-index.txt` verifies SB016/SB017 manifests, clean build, 17/17 focused tests, adoption/no-weakening source scan, and red-team rejection.
- Changed source files: `repo://tests/CanDoItAll.Tests.Unit/ProcessDriverVerificationTestHarness.cs`; `repo://tests/CanDoItAll.Tests.Unit/ProcessDriverVerificationTestHarnessTests.cs`; `repo://tests/CanDoItAll.Tests.Unit/ProcessDriverTranscriptVerificationAlphaTests.cs`; `repo://tests/CanDoItAll.Tests.Unit/ProcessDriverRuntimeEvidenceConsistencyAlphaTests.cs`.
- Production assertions: no production driver package was changed for this gate; the test harness source has no runtime host, DI, process, HTTP, file, directory, DbContext, registry, selector, manager command, or endpoint mapping surface.
- Security assertions: source scan proves no secret-like pattern and no UI/media target in the Gate F target set.
- Adversarial negative case: report-only or helper-only closure without adoption/no-weakening proof is rejected with simulated verifier exit code 1.
- Downstream dependency check: SB019 and later gateway phases may proceed only from a shared harness baseline used by transcript and runtime verifier tests; if the helper adoption scan, focused fact counts, or no-skip proof fails, downstream phases must reopen.

## Artifact Matrix
| Artifact | Role | Required signal |
| --- | --- | --- |
| `gate-f-solution-build-no-restore.txt` | Build proof | Solution build succeeds. |
| `gate-f-focused-harness-adoption-tests.txt` | Behavioral proof | Harness/transcript/runtime focused tests pass. |
| `gate-f-harness-adoption-no-weakening-scan.txt` | Source proof | Harness adoption exists without weakened coverage. |
| `red-team-harness-adoption-report-only-rejection.txt` | Adversarial proof | Report-only/helper-only closure is rejected. |
| `gate-f-proof-index.txt` | Positive proof index | Gate F proof artifacts and upstream manifests are verified. |
