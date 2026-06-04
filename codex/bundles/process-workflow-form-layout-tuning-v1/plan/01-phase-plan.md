# Phase Plan

## Phase Sequence

1. `SB01` records current-state inventory and imagegen proposals.
2. Run the prepared-stage bundle validator.
3. `SB02` implements the Processes step form tab layout and compact child editor adjustments.
4. `SB03` implements the Workflows editor inspector tab layout.
5. `SB04` runs builds, source assertions, anti-stub audit, browser proof, raw-note closure, and completed-stage validation.

## Subbundle Dependency Map

```mermaid
gantt
title Process and workflow form layout tuning
dateFormat  YYYY-MM-DD
section Planning
SB01 layout inventory and image proposals :done, sb01, 2026-05-31, 1d
section Implementation
SB02 process step form tabs :after sb01, sb02, 1d
SB03 workflow editor form tabs :after sb01, sb03, 1d
section Closure
SB04 validation and closure :after sb02, sb04a, 1d
SB04 workflow validation dependency :after sb03, sb04b, 1d
```

- `SB02` and `SB03` both depend on `SB01`.
- `SB04` depends on both implementation subbundles.

## Critical Subbundles

- `SB04` is the critical closure subbundle because it owns the final proof that the layout-only implementation compiles, renders, and closes every raw note.
- The UI implementation subbundles remain proof-gated by `SB04`; this bundle does not introduce production signals, states, records, events, runtime branches, persistence, or security policy.

## Phase Gates

- Prepared gate: `scripts/validate_bundle.py --stage prepared` must pass before product edits.
- `SB01` progression gate: image proposals and source inventory exist and are mapped to requirements.
- `SB02` progression gate: Processes step form tabs compile and source assertions show the long mixed stack was split.
- `SB03` progression gate: Workflows editor inspector tabs compile and source assertions show definition/node/executor/routes/preview forms are separated.
- Final closure gate: builds, source assertions, anti-stub audit, browser screenshots, raw-note closure, and completed-stage validator agree.
