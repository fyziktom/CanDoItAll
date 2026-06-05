# SB15 Semantic Invariants

- Invariant ID: `SB15_INV_001`
- Source raw note: RN-001, RN-002, RN-003, and RN-004.
- Expected behavior: Full build and focused dispatch tests pass after route, claim, heartbeat, start-transition, and finalizer-context helper movement.
- Disallowed shallow implementation: Relying on helper source scans without compiling the solution or proving route/claim/heartbeat behavior through focused tests.
- Failing-first test: `bundle://proof/SB15/transcripts/sb15-failing-first-policy-trap.txt` exits `1` when supplied a simulated prohibited mobile proof path.
- Passing test: `bundle://proof/SB15/transcripts/sb15-full-build.txt`, `bundle://proof/SB15/transcripts/sb15-focused-dispatch-integration-tests.txt`, and `bundle://proof/SB15/transcripts/sb15-focused-architecture-tests.txt`.
- Changed source files: N/A - critical runtime smoke and proof-policy gate only.
- Production assertions: `bundle://proof/SB15/source-assertions/runtime-smoke-proof-policy.md`.
- Red-team negative case: `bundle://proof/SB15/transcripts/sb15-failing-first-policy-trap.txt`.
- Downstream dependency check: SB16 can proceed to final red-team with a passing full build and focused dispatch proof.

- Invariant ID: `SB15_INV_002`
- Source raw note: RN-002 and RN-004.
- Expected behavior: Proof remains runtime/service-only with no UI diff, no Process Core/driver API, no MAF back-dependency, and no small/medium/mobile artifacts.
- Disallowed shallow implementation: Satisfying runtime smoke with browser/mobile screenshots, UI changes, or docs that hide production driver API drift.
- Failing-first test: `bundle://proof/SB15/transcripts/sb15-failing-first-policy-trap.txt`.
- Passing proof: `bundle://proof/SB15/transcripts/sb15-runtime-proof-policy-scan.txt`.
- Changed source files: N/A - policy gate only.
- Production assertions: `bundle://proof/SB15/source-assertions/runtime-smoke-proof-policy.md`.
- Red-team negative case: `bundle://proof/SB15/transcripts/sb15-failing-first-policy-trap.txt`.
- Downstream dependency check: SB16 must preserve the same proof policy.
