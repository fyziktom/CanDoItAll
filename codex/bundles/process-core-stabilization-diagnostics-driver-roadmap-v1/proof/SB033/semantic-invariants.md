# SB033 Semantic Invariants

## Invariant SB033-INV-001
- Invariant ID: `SB033-INV-001 broad smoke proves behavior and boundary preservation`.
- Raw note literal closure: preserve functionality while stabilizing Process Core and preparing driver readiness safely.
- Expected behavior: build, full unit tests, current/historical architecture guards, focused process-dispatch integration, Core dependency scans, driver-token scans, UI/media scans, and anti-stub scans all pass.
- Shallow-pass trap: relying on focused tests only while the full unit project still hangs or old architecture guards still use obsolete no-Core assumptions.
- Adversarial negative proof: stale `RunGit` stdout/stderr handling deadlocked in dirty worktrees; the helper now drains both streams asynchronously and full unit tests pass.
- Semantic positive proof: `bundle://proof/SB031/transcripts/full-unit-tests.txt`, `bundle://proof/SB031/transcripts/architecture-megaclass-tests.txt`, and `bundle://proof/SB031/transcripts/process-dispatch-integration-tests.txt` passed.
- Anti-stub audit: `bundle://proof/SB033/transcripts/anti-stub-audit.txt`.
- Production assertions: `bundle://proof/SB033/transcripts/source-assertions.txt`, `bundle://proof/SB033/transcripts/core-forbidden-dependency-scan.txt`, and `bundle://proof/SB033/transcripts/production-driver-token-scan.txt`.
- Passing tests: full unit project, architecture mega-class, and focused process-dispatch integration.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative-test citation |
| --- | --- | --- | --- | --- |
| `RunGit async stream drain` | `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs` | Architecture tests that run git in dirty worktrees | Test-only helper fix; prevents stdout/stderr deadlock while preserving exit-code/error assertions. | `Process_core_pre_extraction_consolidation_SB002_INV_001_guards_core_driver_ui_drift_and_collapsed_rows` |
| `Broad smoke transcript set` | `bundle://proof/SB031/transcripts/` and `bundle://proof/SB033/transcripts/` | SB033/SB036 final closure | Artifact-backed proof for build, full unit, architecture, integration, source scans, UI/media scan, and anti-stub audit. | `Process_core_stabilization_SB034_SB036_INV_001_closes_final_handoff_with_scorecard_and_driver_denial` |

