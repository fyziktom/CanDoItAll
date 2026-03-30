# Phase Plan

## Phase Sequence

1. Audit and retire the active duplicate asset ownership problem in `ComponentKit`.
2. Split the workbench runtime and workbench stylesheet trees, then update the manifest and generated include components.
3. Split the calendar monolith and finish the generated public asset reorganization.
4. Rebuild, test, browser-validate, and run the final line-count closure audit.

## Subbundle Dependency Map

```mermaid
gantt
title CanvasLib Resource Reorganization Dependency Map
dateFormat  YYYY-MM-DD
section Foundations
01 Asset topology and duplicate retirement :done, sb01, 2026-03-30, 1d
section Workbench split
02 Workbench runtime and stylesheet split :after sb01, sb02, 1d
section Calendar split
03 Calendar and generated asset split :after sb02, sb03, 1d
section Closure
04 Validation and closure :after sb03, sb04, 1d
```

- `02` must not start until `01` proves which package owns the shipped static assets.
- `03` depends on the same manifest and include-generation machinery changed in `02`.
- `04` is only trustworthy if `01` through `03` all pass their closure gates and the browser proof still shows CanvasLib assets loading correctly.

## Critical Subbundles

- `01 Asset topology and duplicate retirement`
  - Critical foundation because weak proof here could leave the app silently loading legacy `ComponentKit` assets.
  - Requires build-level proof and source-level reference audit before downstream work may continue.
- `02 Workbench runtime and stylesheet split`
  - Critical foundation because it changes asset ordering and the main structure-canvas route.
  - Requires deeper validation: asset generation, targeted tests, and a browser smoke on the structure route before `03` may proceed.

## Phase Gates

- Gate after preparation:
  - Run `validate_bundle.py --stage prepared`.
  - Manually review input coverage, critical-path risks, and proof depth against the bundle-validator checklist.
- Gate before subbundle `02`:
  - Confirm no active source consumer requires `_content/CanDoItAll.ComponentKit/...`.
  - Confirm the duplicate retirement plan does not break the legacy project build.
- Gate after subbundle `02`:
  - Regenerate assets.
  - Verify manifest/include ordering.
  - Run targeted workbench proof before touching calendar files.
- Gate after subbundle `03`:
  - Regenerate assets again.
  - Validate both structure and calendar surfaces.
  - Confirm no CanvasLib generated asset remains above 2000 lines.
- Gate before closure:
  - Run final validators, targeted tests, Playwright proof, and line-count audit.
  - Reopen any earlier subbundle if proof shows stale duplicate assets, ordering errors, or remaining over-limit files.
