# SB003 Semantic Invariants

## Status
Completed.

## Invariant SB003_INV_001
Stable long-lived test fixtures must not embed concrete transient bundle folder paths.

## Shallow-Pass Trap
A shallow implementation could update status rows or delete old fixture files without proving the remaining stable architecture fixtures are still consumed by unit tests. SB003 rejects that by combining fixture normalization, a source-level regression guard, focused consumer tests, a full unit rerun, and a no-transient-path scan over `repo://src` and `repo://tests`.

## Failing-First And Negative Proof
- Failing-first evidence: `bundle://proof/SB002/transcripts/transient-path-classification-scan.txt` found 147 transient-path hits before SB003 cleanup.
- Adversarial negative proof: `bundle://proof/SB003/transcripts/red-team-transient-path-rejection.txt` proves the scan detects a planted transient-path fixture.
- Historical full-unit negative proof: `bundle://proof/SB003/transcripts/full-unit-tests.txt` exposed one timing-sensitive host test failure; the isolated test and no-build full rerun then passed.

## Positive Proof
- Focused Gate A tests passed: `bundle://proof/SB003/transcripts/gate-a-focused-unit-tests.txt`
- Isolated host timing rerun passed: `bundle://proof/SB003/transcripts/local-workspace-host-rerun.txt`
- Full unit no-build rerun passed: `bundle://proof/SB003/transcripts/full-unit-rerun-no-build.txt`
- No-transient-path scan passed: `bundle://proof/SB003/transcripts/no-transient-bundle-path-scan.txt`
- Anti-stub/runtime-host drift scan passed: `bundle://proof/SB003/transcripts/anti-stub-and-runtime-host-drift-scan.txt`

## Production Behavior Artifact Matrix
| Artifact | Producer | Consumer | Lifecycle | Proof |
| --- | --- | --- | --- | --- |
| Stable architecture fixture path guard | `ProcessDriverFakeProofResistanceTests` | Unit test suite | Fails if stable architecture fixtures contain transient bundle paths | `bundle://proof/SB003/transcripts/gate-a-focused-unit-tests.txt` |
| Normalized architecture fixture content | SB003 fixture edit | Architecture boundary and fake-proof resistance tests | Keeps historical proof fixtures portable and test-consumed | `bundle://proof/SB003/transcripts/full-unit-rerun-no-build.txt` |
| No transient source/test path scan | SB003 proof command | Downstream gates | Verifies no source or long-lived test file depends on concrete bundle folders | `bundle://proof/SB003/transcripts/no-transient-bundle-path-scan.txt` |

## Runtime-Host Boundary
SB003 does not introduce a generic driver runtime host, registry, selector, DI registration, manager command, scheduler hook, workflow hook, process-state mutation, or Process Core runtime orchestration. The anti-stub/runtime-host drift scan is the closure guard for this invariant.
