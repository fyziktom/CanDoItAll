# Phase Plan

## Phase Sequence

1. Prepare and validate this bundle before touching implementation code.
2. Execute `01 Asset ownership and duplicate retirement` first because every later phase depends on the canonical asset layout and the duplicate-decision baseline.
3. Execute `02 CanvasLib component topology reorganization` after `01` closes.
4. Execute `03 Canvas graph and contracts decomposition` after `01` closes and coordinate any shared workbench consumer changes discovered during `02`.
5. Execute `04 Validation and closure` only after `02` and `03` both pass their closure gates.
6. End with the raw-note closure audit and the final bundle validator.

## Subbundle Dependency Map

```mermaid
gantt
title CanvasLib maintainability stabilization dependency map
dateFormat  YYYY-MM-DD
section Foundations
01 Asset ownership and duplicate retirement :foundation, 2026-03-30, 2d
section Structural refactors
02 CanvasLib component topology reorganization :after foundation, components, 2d
03 Canvas graph and contracts decomposition :after foundation, graph, 2d
section Closure
04 Validation and closure :after graph, closure, 2d
```

- `04` may start only after both `02` and `03` have closed, even though the gantt anchor is shown after `03`.

## Critical Subbundles

- `01 Asset ownership and duplicate retirement` is a `Critical foundation`.
  - Required proof before downstream work continues:
    - duplicate inventory updated
    - asset tooling updated and passing
    - shared canvas routes load without missing asset failures
- `02 CanvasLib component topology reorganization` is a `Critical UI foundation`.
  - Required proof before closure:
    - consuming modules compile
    - shared components still render on browser routes
- `03 Canvas graph and contracts decomposition` is a `Critical behavioral foundation`.
  - Required proof before closure:
    - state serialization and contract consumers still pass
    - browser behaviors that depend on graph models still work

## Phase Gates

- Gate after preparation:
  - run `python C:\Users\lucys\.codex\skills\candoitall-bundle-preparation\scripts\validate_bundle.py C:\repositories\CanDoItAll\canvaslib-maintainability-stabilization-bundle-v1 --profile initiative --stage prepared`
  - repair any failures before implementation
- Gate before each subbundle:
  - confirm prerequisites are still valid
  - confirm no earlier critical foundation was weakened by intervening edits
- Gate after each subbundle:
  - capture the documented proof
  - update `reviews/01-execution-report.md`
  - decide whether downstream phases may continue
- Gate before closure:
  - rerun the required commands
  - perform the duplicate and line-count audits
  - rerun the bundle validator for `completed`
