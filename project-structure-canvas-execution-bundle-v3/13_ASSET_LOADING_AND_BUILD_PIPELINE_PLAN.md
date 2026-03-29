# Asset loading and build pipeline plan

## Problem

CanvasLib assets are currently loaded through long manual script lists in multiple app entrypoints.

This creates:
- drift risk,
- forgotten script regressions,
- unclear runtime-vs-preview loading boundaries.

## Target

### Source organization
- keep small JS source fragments in `wwwroot/js-src/**`,
- keep small CSS source fragments in `wwwroot/css-src/**`,
- generate stable public outputs in `wwwroot/js/**` and `wwwroot/canvas-workbench.css`.

### Build scripts
Add plain Node/CommonJS scripts:
- `tools/canvaslib/build-assets.cjs`
- `tools/canvaslib/verify-assets.cjs`

No TypeScript.  
No heavy external bundler is required.

## Suggested output strategy

### Runtime
- generated `canvasWorkbenchInterop.js`
- generated `canvas-floating-window.js`
- optional generated `canvaslib.runtime.js` if a consolidated public runtime file is cleaner

### Preview
- optional generated `canvaslib.preview.js` for preview-boundary components

The safest migration path is:
- keep old public filenames available,
- move the implementation behind generated bundles or compatibility shims,
- update app entrypoints only when the new includes are proven.

## Centralized include helpers

Add shared asset include helpers in CanvasLib, for example:
- `CanvasLibHeadAssets.razor`
- `CanvasLibBodyAssets.razor`

or a similar pair that emits:
- shared stylesheet links,
- runtime script tags,
- optional preview script tags,
- optional calendar script tags.

Then replace manual lists in:
- `CanDoItAll.Web/Components/App.razor`
- `CanDoItAll.Components.Sandbox/Components/App.razor`

## Verification rules

`verify-assets.cjs` should fail if:
- generated public files are stale,
- source fragments changed without rebuilding,
- required output files are missing.

## Optional npm scripts

Recommended root `package.json` scripts:
- `canvaslib:build-assets`
- `canvaslib:verify-assets`

## Non-goals

Do not:
- introduce TypeScript,
- introduce a large external build pipeline,
- make app entrypoints directly responsible for knowing every CanvasLib internal file.
