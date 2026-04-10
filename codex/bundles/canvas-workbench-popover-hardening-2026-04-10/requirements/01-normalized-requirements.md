# Normalized Requirements

## Requirements

- `R001` The shared `CanvasWorkbench` annotation hover path must not throw an exception when `syncSceneHoverState` resolves an annotation hit target.
- `R002` Canvas annotation popovers must remain functional across the annotation-bearing node render paths that register shared scene hot zones.
- `R003` Canvas-specific hover state must be initialized and explicitly cleared during hide, click, refresh, and other state-reset flows so stale hover keys do not suppress later popovers.
- `R004` Popover rendering must tolerate disabled popover chrome, missing popover DOM references, or disconnected host state without throwing.
- `R005` Existing annotation actions, click behavior, overlay layering, delete-mode behavior, and other current workbench functions must be preserved.
- `R006` The fix must include an audit and repair of nearby JS anti-patterns in the same mechanism, especially split-file cross-reference fragility and stale state coupling.
- `R007` Closure proof must include targeted validation plus real browser interaction on the shared canvas route and a workbench route when the environment allows it.
- `R008` The follow-up must inventory the broader `CanvasLib` JavaScript surface and explicitly justify which long-file hotspots are executed in this bundle versus deferred.
- `R009` The largest verified workbench-runtime hotspots must be split into smaller ordered feature-slice files without changing the external `canvasWorkbench` API or the shared `CanvasWorkbenchSurface` contract.
- `R010` Where the current runtime contains exact cleanup or helper duplication inside the targeted files, the refactor should consolidate that logic instead of copying it into the new slices.
- `R011` Final closure proof for the organization pass must demonstrate that the split files still preserve hover, click, context, drag, and runtime-API behavior on the real workbench route.
- `R012` Exported shared `canvasWorkbench` runtime methods must tolerate null or disconnected hosts during Blazor lifecycle churn instead of throwing and breaking the circuit.
- `R013` `CanvasWorkbench` after-render synchronization must avoid fragile multi-call JS sequencing when create or update, maximized state, fit-view, and selection sync all depend on the same host element.
- `R014` Reopened closure proof must cover the reachable CanvasLib surfaces in the CanDoItAll app, and any route that cannot reach its canvas because of a non-canvas failure must be logged explicitly as blocked rather than treated as proved.

## Non-Goals

- Replacing the current `CanvasWorkbench` architecture with a different rendering model.
- Changing the C# `CanvasWorkbenchSurface` or consumer-page contracts unless the JS repair proves that unavoidable.
- Reworking unrelated canvas systems such as context menus, minimap, or floating windows beyond the directly adjacent hover and popover mechanism.
- Launching a separate calendar-runtime refactor without a comparable proof surface in this same turn.
- Fixing unrelated non-canvas route failures such as missing Prompt Factory manifest assets inside this bundle unless the canvas refactor is proven to have caused them.
