# SB16 Semantic Invariants

- Invariant ID: `SB16_INV_001`
- Source raw note: RN-001, RN-002, RN-003, and RN-004.
- Expected behavior: Final red-team proof confirms route/claim/heartbeat/concurrency/finalizer helper boundaries are present, behavior proof passed, and no prohibited scope drift exists.
- Disallowed shallow implementation: Closing the bundle without full build, focused tests, manifest/invariant presence checks, source scans, or adversarial negative proof.
- Failing-first test: `bundle://proof/SB16/transcripts/sb16-failing-first-red-team-trap.txt` exits `1` for simulated Process Core/driver API source.
- Passing test: `bundle://proof/SB16/transcripts/sb16-final-build.txt`, `bundle://proof/SB16/transcripts/sb16-final-focused-tests.txt`, and `bundle://proof/SB16/transcripts/sb16-final-red-team-scan.txt`.
- Changed source files: N/A - final red-team gate and documentation-only next cutline.
- Production assertions: `bundle://proof/SB16/source-assertions/final-red-team-and-next-cutline.md`.
- Red-team negative case: `bundle://proof/SB16/transcripts/sb16-failing-first-red-team-trap.txt`.
- Downstream dependency check: Any follow-up should start from `bundle://architecture/06-next-dispatch-cutline.md`.

- Invariant ID: `SB16_INV_002`
- Source raw note: RN-002 and RN-003.
- Expected behavior: The next recommended seam is candidate selection/hydration inside `CanDoItAll.Modules.Processes`; Process Core and production driver APIs remain explicitly out of scope.
- Disallowed shallow implementation: Treating driver-readiness documentation as approval to create production driver contracts or public process-core abstractions.
- Failing-first test: `bundle://proof/SB16/transcripts/sb16-failing-first-red-team-trap.txt`.
- Passing proof: `bundle://proof/SB16/transcripts/sb16-final-red-team-scan.txt` and `bundle://proof/SB16/transcripts/sb16-completed-bundle-validation.txt`.
- Changed source files: `repo://codex/bundles/process-dispatch-claim-route-boundary-v1/architecture/06-next-dispatch-cutline.md`.
- Production assertions: `bundle://proof/SB16/source-assertions/final-red-team-and-next-cutline.md`.
- Red-team negative case: `bundle://proof/SB16/transcripts/sb16-failing-first-red-team-trap.txt`.
- Downstream dependency check: Completed-stage bundle validation must pass before closure.
