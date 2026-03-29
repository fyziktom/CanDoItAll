# ProjectStructure Canvas Execution Bundle (v3)

This bundle is an English-only, execution-grade package for the next `ProjectStructurePage` and `CanvasLib` refactor.

It was produced after reviewing the repo **after the previous bundle had already been applied**. The goal of this bundle is not only to improve performance, but also to make the shared canvas codebase significantly more maintainable and easier to evolve.

## Validation Summary

- Bundle preparation status: `Prepared legacy bundle`
- Bundle readiness gate: `Manual execution audit completed`
- Execution status: `Reopened after validation`
- Subbundle gate review: `Foundation fixes, browser regressions, shared-consumer checks, and benchmark smoke are green; true-canvas migration tasks remain open`
- Final closure gate: `Not eligible`
- Browser validation analytics: `Recorded in reviews/01-execution-report.md`

This bundle predates the newer normalized `plan/` and `subbundles/` schema. The execution report added in `reviews/01-execution-report.md` is the authoritative status record for the current pass.

## Current conclusion

The applied work improved a few important things:

- multi-node move persistence is now batched,
- the current workbench runtime has partial retained rendering and viewport filtering,
- floating windows are more isolated than before,
- some page logic was split into partial classes.

However, the main architectural problem is still present:

- the runtime workbench scene is **still mostly DOM + SVG**, not a real HTML5 canvas renderer,
- `ProjectStructurePage` still performs too much eager persistence and still forces expensive reloads in important paths,
- the toolbox is still not finished functionally or ergonomically,
- `CanvasLib` still mixes runtime, preview, and legacy concerns,
- the largest JS/CSS/Razor files are still monolithic.

## Primary objective of this bundle

Move the runtime scene toward a **real canvas renderer** while preserving all existing features and keeping the right ownership split:

- **JS** owns the hot path: rendering, hit testing, drag, pan/zoom, dirty regions, culling, canvas composition, and runtime metrics.
- **C#** owns typed models, adapters, product semantics, service calls, persistence, and final committed state.
- **HTML/Blazor** remains for windows, toolbox, dialogs, context menus, accessibility mirror, editors, and other UI that should not be painted into the scene.

This is intentionally **not** a recommendation to paint every piece of UI into the canvas.  
Dense scene layers should move to canvas. Rich controls and overlays should remain HTML.

## What is inside

### Audit and architecture documents
- `00_EXECUTIVE_SUMMARY.md`
- `01_IMPLEMENTATION_GAP_REVIEW.md`
- `02_CURRENT_RUNTIME_AUDIT.md`
- `03_FEATURE_PRESERVATION_MAP.md`
- `04_TARGET_ARCHITECTURE_AND_RENDERING_BOUNDARIES.md`
- `05_CANVASLIB_REORGANIZATION_PLAN.md`
- `06_FILE_SPLIT_PLAN.md`
- `07_TRUE_CANVAS_MIGRATION_PLAN.md`
- `08_TOOLBOX_FUNCTIONAL_AND_UX_SPEC.md`
- `09_PERFORMANCE_HOTSPOTS_AND_BUDGETS.md`
- `10_VALIDATION_GATES_AND_RETRY_PROTOCOL.md`
- `11_LINE_REFERENCE_INDEX.md`
- `12_SHARED_CONSUMERS_AND_LEGACY_PLAN.md`
- `13_ASSET_LOADING_AND_BUILD_PIPELINE_PLAN.md`
- `14_LIMITATIONS_AND_ASSUMPTIONS.md`

### Codex execution material
- `codex/MASTER_PROMPT.md`
- `codex/TASK_SEQUENCE.md`
- `codex/VALIDATION_PROMPT.md`
- `codex/RETRY_PROTOCOL.md`
- `codex/tasks/*.md`

### Machine-readable support files
- `traceability/features.csv`
- `traceability/tasks_to_features.csv`
- `traceability/hotspots.csv`
- `traceability/runtime_files.csv`
- `traceability/js_function_inventory.csv`
- `traceability/component_inventory.csv`
- `traceability/existing_test_inventory.csv`
- `traceability/current_gaps.json`
- `traceability/old_to_new_canvaslib_mapping.csv`

### Reference asset
- `references/visual-studio-toolbox-reference.png`

## How Codex should use this bundle

1. Read `00_EXECUTIVE_SUMMARY.md`.
2. Read `03_FEATURE_PRESERVATION_MAP.md` before editing any shared-canvas code.
3. Read `08_TOOLBOX_FUNCTIONAL_AND_UX_SPEC.md` before touching the toolbox.
4. Read `05_CANVASLIB_REORGANIZATION_PLAN.md` and `06_FILE_SPLIT_PLAN.md` before moving files.
5. Execute tasks in `codex/TASK_SEQUENCE.md` **one by one**.
6. Do not advance if any validation gate is red.
7. Keep rerunning until all targeted tests, browser checks, screenshots, and performance gates are green.

## Important honesty note

This bundle is based on a **static source audit** of the uploaded repository snapshot.  
The environment available to me did **not** include the `dotnet` CLI, so I could not run builds, tests, or Playwright here. The validation material in this bundle is therefore a detailed execution plan and audit, not a runtime-verified patch set.

Generated: 2026-03-29 14:53 UTC
