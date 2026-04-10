# Target Solution

## Intended Runtime Contract

- Canvas-rendered annotation hover must route through one safe popover entry path that is valid in split runtime files and resilient to file load order.
- Canvas annotation hover state must be treated as explicit state, not an implicit side effect of whether the popover happens to be visible.
- Scene-hover cleanup must clear the canvas annotation key when canvas annotation interaction is complete, while DOM badge popovers keep their existing independent behavior.
- Popover rendering must no-op safely when popover chrome is disabled, when the host is disconnected, or when title and body elements are unavailable.

## Minimal Change Strategy

- Keep the fix inside the shared JS runtime files under `wwwroot/js/runtime/workbench`.
- Reuse the existing late-runtime pattern already present in `02-layout-and-legacy-render.js` instead of inventing a new abstraction layer.
- Add only the state initialization and reset logic necessary to keep canvas annotation hover coherent across rerenders and click paths.
- Preserve current popover content, placement logic, and action dispatch unless the hardening work requires a guarded wrapper around them.

## Boundaries

- No new C# interfaces or page-level fallbacks.
- No silent suppression of errors beyond explicit null and connectivity guards in the shared popover path.
- No workbench-route-only patch that leaves the shared sandbox and other `CanvasWorkbench` consumers behind.
