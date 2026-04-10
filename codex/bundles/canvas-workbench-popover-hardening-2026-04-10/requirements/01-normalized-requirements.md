# Normalized Requirements

## Requirements

- `R001` The shared `CanvasWorkbench` annotation hover path must not throw an exception when `syncSceneHoverState` resolves an annotation hit target.
- `R002` Canvas annotation popovers must remain functional across the annotation-bearing node render paths that register shared scene hot zones.
- `R003` Canvas-specific hover state must be initialized and explicitly cleared during hide, click, refresh, and other state-reset flows so stale hover keys do not suppress later popovers.
- `R004` Popover rendering must tolerate disabled popover chrome, missing popover DOM references, or disconnected host state without throwing.
- `R005` Existing annotation actions, click behavior, overlay layering, delete-mode behavior, and other current workbench functions must be preserved.
- `R006` The fix must include an audit and repair of nearby JS anti-patterns in the same mechanism, especially split-file cross-reference fragility and stale state coupling.
- `R007` Closure proof must include targeted validation plus real browser interaction on the shared canvas route and a workbench route when the environment allows it.

## Non-Goals

- Replacing the current `CanvasWorkbench` architecture with a different rendering model.
- Changing the C# `CanvasWorkbenchSurface` or consumer-page contracts unless the JS repair proves that unavoidable.
- Reworking unrelated canvas systems such as context menus, minimap, or floating windows beyond the directly adjacent hover and popover mechanism.
