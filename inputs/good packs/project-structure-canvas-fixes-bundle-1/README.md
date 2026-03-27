# Project Structure Canvas Fixes Bundle 1

This bundle is a planning and execution pack for the Project Structure canvas UI. It does not implement the fixes yet. It organizes the work into atomic sub-bundles that can be delivered against the already running watch session at `https://localhost:7271`.

## Status

Ready for implementation on March 25, 2026.

## Target Result

- the canvas toolbar stays pinned at the true top of the canvas and remains reachable at all times
- the canvas uses the full available page width by removing the dedicated right inspector column
- the current right panel behavior is preserved, but moved into the canvas as a draggable, resizable, minimizable, normalizable, hideable, and restorable window
- the current Canvas Health overlay stops blocking the toolbar and becomes a proper floating canvas window
- node-aware inspector states still work for empty selection, single selection, and multi-selection
- multi-selection exposes shared actions for status, progress, marker, priority, and grouping where those actions are valid
- the panel layout becomes denser and more space-efficient without losing clarity
- file and media nodes expose better preview and open actions, including a deliberate local-open path for supported file types

## Current Diagnosis

- the project structure page still uses the `Inspector` slot of `CanvasWorkbenchStage`, so the shared workbench grid reserves a large right column before the canvas even renders
- the Canvas Health overlay is a fixed absolute block inside `OverlayContent`, so it can sit in the same top region as the toolbar and block access
- the toolbar itself is a static absolute rail with horizontal scrolling behavior, but no contract that floating panels must stay below it
- prompt factory already contains a better pattern: it renders the inspector inside `OverlayContent`, uses JS interop to clamp drag movement below the toolbar, and supports resize plus hide/show

## Main Risks

- a CSS-only fix will not be enough; the structure page needs a real in-canvas window model
- local file opening cannot be solved safely by a browser link alone; it needs an explicit trusted host-side open action
- panel migration must preserve all existing inspector functions before density cleanup starts
- if panel state is not persisted, users will lose their preferred workspace arrangement after reloads

## Acceptance Gates

- no dedicated right inspector column remains on the structure page at desktop widths
- toolbar controls are always reachable and no default floating panel spawns on top of them
- every in-canvas window supports drag, resize, minimize, normalize, hide, and show
- single-select and multi-select preserve current behavior and gain the missing common actions called out in this bundle
- media and file nodes keep preview/new-tab behavior and add a safe local-open path where supported
- panel layout becomes visibly denser with less dead space, fewer oversized gaps, and reduced unnecessary text

## Bundle Map

- `01-current-state-and-user-problems.md`
- `02-root-causes-and-reuse-path.md`
- `03-target-canvas-window-system.md`
- `04-selection-panel-density-and-parity.md`
- `05-file-and-media-actions.md`
- `06-validation-and-acceptance-plan.md`
- `07-execution-subbundles.md`
- `08-qa-architect-approval.md`
- `subbundles/01-canvas-shell-and-toolbar-checklist.md`
- `subbundles/02-shared-floating-window-host-checklist.md`
- `subbundles/03-selection-panel-parity-and-density-checklist.md`
- `subbundles/04-file-media-and-local-open-checklist.md`
- `subbundles/05-validation-and-regression-checklist.md`
- `artifacts/README.md`
- `tools/README.md`

## Runtime Context

- active managed app session: `app_375a0462f72245b7a0edab3e357f530d`
- mode: `WatchRun`
- project: `src\CanDoItAll.Web\CanDoItAll.Web.csproj`
- health URL: `https://localhost:7271/_dev/runtime`
- watch state at bundle preparation time: `WaitingForChanges`
