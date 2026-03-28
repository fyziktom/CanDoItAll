# ProjectStructure Execution-Grade Codex Bundle (v2)

This bundle is an English-only, execution-grade refactoring and performance package for the `ProjectStructurePage` workbench in the `CanDoItAll` repository.

## Primary outcome

The bundle is designed to let Codex improve the current implementation **step by step without breaking functionality** while moving the hot path toward a more JS-owned renderer model.

It assumes the target architecture is:

- **JavaScript owns the hot path**: scene rendering, retained element maps, pointer ownership, drag loop, pan/zoom, viewport culling, and transient floating-window geometry.
- **C# owns the domain**: typed models, graph/service logic, create/edit/delete commands, action catalogs, adapters, and persisted state that truly belongs on the server.
- **HTML stays for overlays**: toolbox, selection window, dialogs, uploads, previews, and forms remain HTML/Blazor unless benchmarking later proves otherwise.

## Most important conclusions from the audit

1. `ProjectStructurePage` is **not currently a true HTML5 canvas renderer** for the live scene. The runtime path is DOM + SVG; real `<canvas>` drawing is used in the export path only.
2. The largest performance problems today are:
   - full scene-layer rebuilds,
   - too much InteractiveServer chatter,
   - too many DB writes in active interaction loops,
   - full surface reloads for simple mutations,
   - incomplete overlay input isolation.
3. The biggest product risk is **regression**, not lack of ideas. Therefore the bundle maps features, existing tests, screenshot gates, and retry rules explicitly.

## What is in this bundle

- `00_EXECUTIVE_SUMMARY.md` – fast executive summary.
- `01_RUNTIME_ARCHITECTURE_AUDIT.md` – how the current runtime is actually built.
- `02_FEATURE_PRESERVATION_MAP.md` – feature inventory that must survive the refactor.
- `03_TARGET_ARCHITECTURE_AND_OWNERSHIP.md` – recommended JS/C#/HTML ownership split.
- `04_PHASED_EXECUTION_PLAN.md` – ordered rollout with phases and success conditions.
- `05_PERFORMANCE_HOTSPOTS.md` – prioritized performance findings with evidence.
- `06_PERFORMANCE_BUDGETS_AND_ACCEPTANCE.md` – concrete done criteria.
- `07_VALIDATION_GATES_AND_SCREENSHOT_SCENARIOS.md` – mandatory validation matrix.
- `08_CODEX_RETRY_PROTOCOL.md` – fix-and-rerun rules for Codex.
- `09_LINE_REFERENCE_INDEX.md` – file/line evidence index.
- `10_HTML_VS_JS_RENDERER_BOUNDARY.md` – what should stay HTML vs JS-owned scene rendering.
- `11_DUPLICATION_AND_SHARED_SURFACE_RISK.md` – CanvasLib vs ComponentKit and cross-surface impact.
- `12_LIMITATIONS_AND_ASSUMPTIONS.md` – honesty section about this audit.

Supporting machine-readable files are in:
- `traceability/`
- `meta/`
- `codex/`

## How to use the bundle

1. Read `00_EXECUTIVE_SUMMARY.md`.
2. Read `02_FEATURE_PRESERVATION_MAP.md` before editing anything.
3. Give Codex `codex/MASTER_PROMPT.md`.
4. Execute the tasks in `codex/TASK_SEQUENCE.md`, one at a time.
5. Do not advance to the next task unless the current task passes its code, browser, screenshot, and performance gates.

## Important limitations

This was a **static source audit**. The environment available to me did **not** include the `dotnet` CLI, so I could not run the build, unit tests, Playwright suite, or a profiler here.  
Because of that, the bundle is deliberately strong on:
- source evidence,
- preservation mapping,
- execution sequencing,
- validation protocol.

It is not claiming a runtime-verified patch set.

## Bundle metrics

- Runtime and related files inventoried: 24
- Existing tests inventoried: 56
- Feature items mapped: 34
- Performance hotspots documented: 12
- Execution subbundles/tasks: 15
