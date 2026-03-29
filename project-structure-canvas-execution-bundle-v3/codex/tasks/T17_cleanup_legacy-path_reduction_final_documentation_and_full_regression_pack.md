# T17 — Cleanup, legacy-path reduction, final documentation, and full regression pack

## Phase
P3

## Goal
After parity is proven, clean up dead DOM/SVG runtime code, keep only intentional compatibility shims, document the new folder structure, and run the final regression pack across Web, Sandbox, ProjectStructure, PromptFactory, and export flows.

## Why this task exists
This task is required to unblock the later real-canvas migration safely and to reduce the risk of breaking existing product behavior.

## Depends on
T09, T15, T16

## Primary files
- `src/CanDoItAll.Components.CanvasLib/**`
- `src/CanDoItAll.ComponentKit/**`
- `src/CanDoItAll.Modules.Workbench/wwwroot/js/workbenchInterop.js`
- `README and CanvasLib docs`
- `tests/CanDoItAll.Tests.Components/**`
- `tests/CanDoItAll.Tests.Playwright/**`

## Feature IDs that must remain green
F33, F34, F37, F38, F39, F40

## Implementation checklist
- Remove dead or misleading runtime paths after parity is proven.
- Keep only intentional compatibility shims and legacy notes.
- Update CanvasLib docs, asset docs, and structure docs to match reality.
- Run the final regression and archive benchmark/screenshot evidence.

## Validation
- Final targeted component tests and browser suite are green.
- The repo docs match the new structure and asset pipeline.
- Dead or misleading runtime paths are removed or clearly marked as compatibility-only.
- Final screenshots and benchmark artifacts are archived.

## Done when
- The codebase is simpler, the runtime renderer is actually canvas-based, and the migration is documented.

## Notes
- Keep comments in source code in English.
- If the task changes shared canvas code, also validate PromptFactory and relevant sandbox surfaces.
- Do not suppress failing tests to get past this task.
