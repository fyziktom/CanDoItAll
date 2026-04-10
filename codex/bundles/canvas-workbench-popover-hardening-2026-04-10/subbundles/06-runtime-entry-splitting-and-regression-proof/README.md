# Runtime entry splitting and regression proof

## Status

- `Completed`

## Objective

- Split `07-runtime-entry.js` into smaller ordered slices for interaction routing and render-pipeline helpers, consolidate exact duplicated cleanup where proven, and close the organization extension with build and browser proof.

## Covered Inputs

- `N006` long JS files deserve splitting
- `N007` share helper functions and useful refactoring
- `N008` execute those subbundles and verify it is working
- `R009` split the largest verified workbench-runtime hotspots
- `R010` consolidate exact duplicate cleanup or helpers
- `R011` prove behavior preservation after the split

## Prerequisites

- `05-canvas-renderer-scene-split`

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\07-runtime-entry.js`
- `C:\repositories\CanDoItAll\tools\canvaslib\asset-manifest.json`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\CanDoItAll.Web.csproj`

## Deliverables

- A new runtime slice for interaction routing and delete-mode hit testing at `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\07a-runtime-interaction-router.js`
- A new runtime slice for render-pipeline helpers and popover rendering at `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\07b-runtime-rendering.js`
- A smaller `07-runtime-entry.js` focused on bootstrap, runtime API, and disposal
- Consolidated shared cleanup for duplicated dispose flows where exact duplication exists
- Final build, browser proof, and completed-stage bundle validation

## Dependency Impact

- This is the closure phase for the organization extension. If the split or cleanup is wrong, every workbench surface that relies on the `canvasWorkbench` runtime contract is at risk.

## Validation Depth

- `End-to-end regression and closure`

## Implementation Steps

1. Move interaction-routing helpers and event attachment out of `07-runtime-entry.js` into an ordered runtime slice.
2. Move render-pipeline helpers and popover rendering out of `07-runtime-entry.js` into a second ordered runtime slice.
3. Consolidate exact duplicated cleanup inside the remaining runtime-entry file.
4. Update runtime asset ordering and regenerate the body-assets component.
5. Run the real build, browser proof, and completed-stage bundle validator.

## Scope Exceptions

- `none`

## Do Not Do

- Do not change external `canvasWorkbench` method names or signatures.
- Do not convert the current runtime into classes or inheritance-heavy abstractions.
- Do not widen this phase into unrelated calendar-runtime refactors.

## Acceptance Checklist

- `07-runtime-entry.js` is materially smaller and focused on bootstrap/runtime API concerns.
- Interaction routing and render helpers live in dedicated ordered runtime files.
- Duplicated cleanup inside the targeted file is consolidated instead of copied.
- Build and workbench browser proof both pass after the split.

## Proof Required

- Targeted build on `src\CanDoItAll.Web\CanDoItAll.Web.csproj`
- Workbench browser proof on `/projects/a17989b9-a8df-4671-9a3a-2d1fdcdfc2fd/structure`
- Completed-stage bundle validation
- Updated execution report with commands, artifacts, and residual risks

## Browser Validation Logging

- Route under test: `/projects/a17989b9-a8df-4671-9a3a-2d1fdcdfc2fd/structure`
- Required viewport passes: `1600x900`, then `1280x800`
- Required Playwright actions: load the route, hover an annotation, click a node, open a context menu, run a synthetic drag through the runtime API if available, and inspect the console for JS errors
- Expected screenshots: one large-screen proof screenshot after the final split
- Required visual review: canvas renders, popover opens, context menu appears, and no missing layers or obvious z-order regressions

## Progression Gate

- The bundle may close only after build, browser proof, and completed-stage validation all pass with the split runtime files in place.

## Closure Note

- Completed on `2026-04-10`.
- `07-runtime-entry.js` now focuses on bootstrap, runtime API, export helpers, and disposal, while `07a-runtime-interaction-router.js` owns event routing and `07b-runtime-rendering.js` owns render-pipeline helpers.
- Real browser proof passed on the workbench route after closing the startup modal: hover, click, re-hover, and app-level context-menu behavior stayed intact, and the final large-screen screenshot was recorded.

## Suggested Agent Prompt

```text
Implement subbundle 06 only. Split 07-runtime-entry.js into smaller ordered runtime slices, consolidate exact duplicated cleanup, and close the organization extension with build and browser proof.
```
