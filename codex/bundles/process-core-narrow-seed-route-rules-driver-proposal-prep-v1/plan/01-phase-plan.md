# Phase Plan


## Execution Order

- Phase 1: SB001-SB003 baseline and guardrails.
- Phase 2: SB004-SB006 Core seed project skeleton.
- Phase 3: SB007-SB009 route pure-rule move and proof.
- Phase 4: SB010-SB012 module adapter and compatibility proof.
- Phase 5: SB013-SB015 subprocess rehearsal proof.
- Phase 6: SB016-SB018 artifact expectation rehearsal proof.
- Phase 7: SB019-SB021 Core hygiene and packaging proof.
- Phase 8: SB022-SB024 driver proposal docs/tests only.
- Phase 9: SB025-SB027 broad smoke and source scans.
- Phase 10: SB028-SB030 final red-team decision.

## Subbundle Dependency Map

```mermaid
flowchart TD
  P1[Phase 1: Baseline + guardrails] --> P2[Phase 2: Core seed project]
  P2 --> P3[Phase 3: Route pure-rule move]
  P3 --> P4[Phase 4: Module adapter + compatibility]
  P4 --> P5[Phase 5: Subprocess rehearsal]
  P4 --> P6[Phase 6: Artifact expectation rehearsal]
  P5 --> P7[Phase 7: Core hygiene + packaging]
  P6 --> P7
  P7 --> P8[Phase 8: Driver proposal docs/tests only]
  P8 --> P9[Phase 9: Broad smoke + source scans]
  P9 --> P10[Phase 10: Final red-team decision]
```

## Critical Subbundles

- SB003: baseline guard
- SB006: Core project guard
- SB009: route pure-rule move proof
- SB012: module adapter parity proof
- SB015: subprocess rehearsal proof
- SB018: artifact expectation rehearsal proof
- SB021: Core hygiene proof
- SB024: driver proposal no-production proof
- SB027: broad smoke proof
- SB030: final red-team decision

## Phase Gates

Each critical gate must include:

- build proof,
- focused unit tests,
- focused dispatch/route integration where applicable,
- source scan for forbidden dependencies,
- no-Core-broad / no-driver proof,
- no UI/media drift proof.
