# Phase Plan

## Phase Sequence

1. Run bundle validators and current-code scan.
2. Execute query-shape and regression-test repairs.
3. Run targeted Cognitive Memory unit/integration validation.
4. Start or reach the web app and run Cognitive Memory API status/recall validation.
5. Update execution report, raw-note closure, residual risks, and completed-stage validator.

## Subbundle Dependency Map

```mermaid
gantt
title Cognitive Memory Senior Validation Repair
dateFormat  YYYY-MM-DD
section Critical Query Foundation
01 query shape and architecture repairs :active, s01, 2026-05-19, 1d
section API And Closure
02 memory API quality validation and closure :after s01, s02, 1d
```

## Critical Subbundles

- `01-01-query-shape-and-architecture-repairs` is a critical foundation because recall and signal query shape controls the memory context visible to agents and downstream validation.
- `02-02-memory-api-quality-validation-and-closure` is final closure. It must not pass if API status, memory quality, tests, or bundle validators are missing.

## Phase Gates

- Prepared gate: `validate_bundle.py <bundle> --stage prepared` passes after bundle files are populated.
- Subbundle 01 entry: original bundle validators and code scans are complete.
- Subbundle 01 closure: code repair tests pass and no public API/schema contract changed.
- Subbundle 02 entry: subbundle 01 closure passed.
- Subbundle 02 closure: API status/recall proof or a precise environment blocker is recorded, raw notes are closed, and completed-stage validation passes.
