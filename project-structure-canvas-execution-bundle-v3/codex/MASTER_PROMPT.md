# Codex master prompt

You are working in the `CanDoItAll` repository on the shared canvas/runtime stack, with `ProjectStructurePage` as the primary tuning target.

## Mission

Implement the refactor described in this bundle **step by step** so that:

- the runtime workbench becomes a real canvas-based scene renderer,
- all mapped product features are preserved,
- the toolbox becomes reliable and compact,
- `CanvasLib` becomes easier to manage,
- PromptFactory remains compatible,
- validation stays green after every task.

## Hard constraints

1. Preserve all features in `03_FEATURE_PRESERVATION_MAP.md`.
2. Keep source-code comments in English.
3. Use plain JavaScript only. Do not introduce TypeScript.
4. Keep typed domain and persistence logic in C#.
5. Keep toolbox, windows, dialogs, context menus, editors, and accessibility mirror in HTML/Blazor unless the task explicitly says otherwise.
6. Use the existing `CanvasBenchmark` sandbox page as benchmark evidence instead of inventing a parallel benchmark harness.
7. If any gate fails, fix it and rerun until green.
8. Do not silently delete preview-boundary components used by PromptFactory support surfaces.
9. Do not treat `CanDoItAll.ComponentKit` as the active runtime path.

## Architecture intent

### JS owns
- renderer,
- hit testing,
- drag/pan/zoom loop,
- dirty redraw,
- culling,
- canvas export composition,
- runtime metrics.

### C# owns
- typed surface contracts,
- adapters,
- product semantics,
- services and persistence,
- committed state.

### HTML/Blazor owns
- toolbox,
- floating windows,
- dialogs,
- composers,
- context menus,
- accessibility mirror.

## Execution rule

Read:
1. `00_EXECUTIVE_SUMMARY.md`
2. `03_FEATURE_PRESERVATION_MAP.md`
3. `08_TOOLBOX_FUNCTIONAL_AND_UX_SPEC.md`
4. `05_CANVASLIB_REORGANIZATION_PLAN.md`
5. `06_FILE_SPLIT_PLAN.md`

Then execute the tasks in `codex/TASK_SEQUENCE.md` in order.

Do not jump ahead to a later renderer task before the guard-rail tasks are green.
