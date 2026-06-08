# SB004 Proof Manifest

## Status
- Subbundle: `SB004`
- Status: `Completed`
- Owned requirement: `REQ-002`
- Scope result: stale historical architecture fixture tests are explicitly quarantined; current bundle fixture ownership is restored by a new Gate A guard test.

## Changed File Hashes
| File | SHA-256 |
| --- | --- |
| `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs` | `294abdc55194336ab8fa034067c63609e35b6d356ad20288a8d42eb4befefbdb` |
| `repo://codex/bundles/process-driver-multi-domain-verification-gateway-v1/subbundles/sb004-fix-or-quarantine-stale-architecture-fixture-path-tests-with-explicit-/README.md` | `6321f303dec5695c094a6a3c6cc48f19947b60ceb272a5eed9311a1edc817909` |
| `repo://codex/bundles/process-driver-multi-domain-verification-gateway-v1/reviews/01-execution-report.md` | `ec9ffd0d90372a679b7a4a4085114bff763c23ee7a8f5dbd3791ab8d25cc6ce8` |

## Command Transcripts
- Focused architecture tests: `bundle://proof/SB004/transcripts/architecture-fixture-quarantine-focused-tests.txt`
- Broad unit run excluding only remaining TuningRequest debt: `bundle://proof/SB004/transcripts/unit-tests-excluding-tuningrequest-after-fixture-quarantine.txt`
- Source/skip/no-drift audit: `bundle://proof/SB004/transcripts/source-skip-and-no-drift-audit.txt`

## Source Assertions
- `ProcessAgentExecutionBoundaryArchitectureTests` now has a single shared quarantine reason for exactly 21 removed historical bundle fixture tests.
- `Process_driver_multi_domain_gate_a_owns_current_bundle_fixture_and_rejects_report_only_closure` reads the current `process-driver-multi-domain-verification-gateway-v1` bundle and asserts SB003 Gate A proof manifest, semantic invariants, red-team rejection, and proof-index artifacts.
- No production files under `repo://src` changed in SB004.
- No UI/media files changed in SB004.

## Validation Results
- Focused architecture test class passed: 80 passed, 21 skipped, 0 failed.
- Broad unit run excluding only `TuningRequestServiceTests` passed: 1055 passed, 21 skipped, 0 failed.
- The stale architecture fixture failure bucket is removed from the active failure set; SB005 still owns TuningRequest cleanup debt.

## Closure Gate
- Entry gate: passed after SB003 Gate A.
- Closure gate: passed.
- Progression decision: SB005 may proceed; SB006 Gate B cannot close until TuningRequest cleanup is fixed or explicitly quarantined.
