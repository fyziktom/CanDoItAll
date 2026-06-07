# SB001 Semantic Invariants

## Invariants

- Invariant ID: `SB001-INV-001`
- Source raw note: `Do not rush Process Core unless clearly justified.`
- Expected behavior: The active branch and source tree are verified before any production refactor starts.
- Disallowed shallow implementation: Reusing previous-bundle proof without active source scans or build proof.
- Failing-first test: `N/A - proof-only baseline; guarded scans act as the negative check for forbidden production drift.`
- Passing test: `bundle://proof/SB001/transcripts/baseline-build.txt`
- Changed source files: `N/A - production source unchanged.`
- Production assertions: `bundle://proof/SB001/transcripts/guarded-source-scans.txt`
- Red-team negative case: Documentation and tests may mention forbidden tokens, but production project/API scans must remain clean.
- Downstream dependency check: Later subbundles depend on this baseline for no-Core, no-driver, and no-UI drift assumptions.

## Raw Note Closure

- Do not rush Process Core: `Partially solved by baseline guardrails; final decision remains owned by SB036.`
- Preserve existing behavior: `Partially solved by baseline build; behavior-specific parity remains owned by later phase gates.`
- No production driver API: `Partially solved by baseline production-source scan; final driver closure remains owned by SB033/SB036.`
- No UI/mobile proof: `Partially solved by no-UI/media diff scan; final scan remains owned by SB034/SB036.`
