# SB033 Critical Proof Manifest

## Scope
- Subbundle: `SB033 - Gate K - broad smoke closure`
- Objective: close broad smoke with build, unit, architecture, integration, source-scan, and warning-policy proof.

## Command Transcripts
- Build: `bundle://proof/SB033/transcripts/build.txt`
- Full unit tests: `bundle://proof/SB031/transcripts/full-unit-tests.txt`
- Architecture mega-class: `bundle://proof/SB031/transcripts/architecture-megaclass-tests.txt`
- Focused process-dispatch integration tests: `bundle://proof/SB031/transcripts/process-dispatch-integration-tests.txt`
- Source assertions: `bundle://proof/SB033/transcripts/source-assertions.txt`
- Production driver token scan: `bundle://proof/SB033/transcripts/production-driver-token-scan.txt`
- Core forbidden dependency scan: `bundle://proof/SB033/transcripts/core-forbidden-dependency-scan.txt`
- UI/media drift scan: `bundle://proof/SB033/transcripts/ui-media-drift-scan.txt`
- Anti-stub audit: `bundle://proof/SB033/transcripts/anti-stub-audit.txt`
- Changed-file hashes: `bundle://proof/SB033/transcripts/changed-file-hashes.txt`
- Semantic invariants: `bundle://proof/SB033/semantic-invariants.md`

## Results
- Solution build passed with three unrelated pre-existing warnings.
- Full unit project passed: 1039 tests.
- Architecture mega-class passed: 92 tests.
- Focused process-dispatch integration passed: 539 tests.
- Core dependency scans, production driver token scans, UI/media drift scans, and anti-stub audits passed.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative-test citation |
| --- | --- | --- | --- | --- |
| `RunGit async stream drain` | `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs` | Architecture tests that run git in dirty worktrees | Test-only helper fix; prevents stdout/stderr deadlock while preserving exit-code/error assertions. | `Process_core_pre_extraction_consolidation_SB002_INV_001_guards_core_driver_ui_drift_and_collapsed_rows` |
| `Broad smoke transcript set` | `bundle://proof/SB031/transcripts/` and `bundle://proof/SB033/transcripts/` | SB033/SB036 final closure | Artifact-backed proof for build, full unit, architecture, integration, source scans, UI/media scan, and anti-stub audit. | `Process_core_stabilization_SB034_SB036_INV_001_closes_final_handoff_with_scorecard_and_driver_denial` |

## Downstream Gate
- SB034-SB036 final decision and handoff may proceed only while broad smoke remains green.
