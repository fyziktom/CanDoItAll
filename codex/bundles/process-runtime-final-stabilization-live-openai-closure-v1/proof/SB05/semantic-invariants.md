# SB05 Semantic Invariants

## Invariant SB05_INV_001
- Invariant ID: `SB05_INV_001`
- Source raw note: RN-004 requires process stabilization without prematurely extracting or approving new runtime driver surfaces.
- Expected behavior: Process Core remains deterministic and generic, read-only verification stays non-mutating, scheduler/workflow readback paths do not invoke drivers directly, and no fallback selector, reflection discovery, registry, or self-registration drift is introduced.
- Disallowed shallow implementation: Counting green runtime tests while silently adding driver runtime hooks, effectful manager-readback calls, or concrete bundle-path source coupling.
- Failing-first test: N/A; no production behavior change in this process boundary validation subbundle. The adversarial scan in `bundle://proof/SB05/transcripts/driver-runtime-drift-scan.txt` would fail on driver runtime selector, reflection discovery, or self-registration tokens.
- Passing test: `bundle://proof/SB05/transcripts/boundary-unit-tests.txt` exits zero with 32/32 boundary tests passing.
- Changed source files: no SB05 source edits. Verified source hashes are recorded in `bundle://proof/SB05/manifest.md`.
- Production assertions: `bundle://proof/SB05/transcripts/process-core-leakage-scan.txt`, `bundle://proof/SB05/transcripts/runtime-host-effectful-api-scan.txt`, and `bundle://proof/SB05/transcripts/scheduler-workflow-driver-hook-scan.txt` prove boundary-clean source state.
- Red-team negative case: `bundle://proof/SB05/transcripts/bundle-path-coupling-scan.txt` rejects proof-specific source coupling, and `bundle://proof/SB05/transcripts/driver-runtime-drift-scan.txt` rejects runtime selector/reflection/self-registration drift.
- Downstream dependency check: SB06 may proceed because boundary evidence does not invalidate runtime-stable classification.
