# T07 — Deterministic asset build pipeline and centralized asset includes

## Phase
P1

## Goal
Introduce a plain-JS asset pipeline that builds generated runtime CSS/JS from small source fragments and replace duplicated app-level script lists with centralized CanvasLib asset include helpers.

## Why this task exists
This task is required to unblock the later real-canvas migration safely and to reduce the risk of breaking existing product behavior.

## Depends on
T06

## Primary files
- `package.json`
- `tools/canvaslib/build-assets.cjs`
- `tools/canvaslib/verify-assets.cjs`
- `src/CanDoItAll.Components.CanvasLib/wwwroot/js-src/**`
- `src/CanDoItAll.Components.CanvasLib/wwwroot/css-src/**`
- `src/CanDoItAll.Components.CanvasLib/Components/**/CanvasLib*Assets*.razor`
- `src/CanDoItAll.Web/Components/App.razor`
- `src/CanDoItAll.Components.Sandbox/Components/App.razor`

## Feature IDs that must remain green
F33, F34, F39

## Implementation checklist
- Add plain-JS build and verify scripts for generated assets.
- Create `js-src` and `css-src` source trees and make generated public outputs derive from them.
- Introduce centralized CanvasLib asset include helpers and replace duplicated App.razor script lists.
- Keep public asset URLs stable or provide compatibility shims during the transition.

## Validation
- Public runtime asset URLs stay stable or are migrated with explicit compatibility shims.
- Generated assets are reproducible and verify-assets fails when committed output is stale.
- Web and Sandbox app entrypoints no longer contain long duplicated script tag lists.

## Done when
- CanvasLib assets have one source of truth.
- The repo can regenerate public JS/CSS bundles without TypeScript or an external bundler.

## Notes
- Keep comments in source code in English.
- If the task changes shared canvas code, also validate PromptFactory and relevant sandbox surfaces.
- Do not suppress failing tests to get past this task.
