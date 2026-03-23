# CanvasWorkbenchShell Specification

## Purpose

Primary reusable graph-workbench component that hosts the graph runtime, toolbar affordances, zoom rail, and typed events.

## User scenarios

- Project Structure graph editing.
- Prompt Factory canvas editing.
- Future graph-like editors such as dependency maps or visual planners.

## Functional scope

### Minimum viable version

Keep the current public surface but extract internal services and overlay composition points.

### Target advanced version

Pluggable shell with slot-based overlays, diagnostics toggles, minimap, and accessibility mirror integration.

## Visual behavior

The component should render using the shared canvas/workbench visual language, with consistent spacing, typography, contrast, and selected/hover/disabled states.

## Interaction behavior

This component owns interaction semantics within its boundary and must coordinate cleanly with SelectionModel, HoverFocusRouter, KeyboardShortcutRouter, and the surrounding shell.

## Component states

- Idle
- Measuring
- Rendered
- Updating
- Disabled
- ErrorFallback

## Edge cases

- Null or incomplete input model arriving during a mid-refresh state.
- Rapid successive updates caused by selection changes, drag loops, or remote persistence echoes.
- Read-only or disabled mode where visuals still need to render but actions must not execute.

## Error behavior

Render a safe fallback visual state, preserve the last good layout when possible, and surface diagnostics/test hooks for investigation.

## Responsibilities

- Provide a reusable visual or structural building block.
- Keep styling and state behavior consistent across canvases.
- Avoid domain-specific assumptions in shared components.

## Responsibility boundaries

- Does not own domain-specific business actions.
- Does not own page-level layout beyond its local rendering responsibility.
- Does not bypass shared state/selection services when interactive.

## Inputs

- Typed props/model.
- Current scene/viewport context.
- Selection or focus state when relevant.
- Theme and interaction settings.

## Outputs

- Rendered visual or interaction state.
- Typed callbacks/events.
- Optional diagnostics metadata.

## Parameters / props / configuration

- Id
- IsReadOnly
- IsDisabled
- ThemeKey
- Bounds
- Tone
- Variant
- SemanticKind

## Data model

Props model + optional measured layout state + style/state projection model.

## Integration points

- src/CanDoItAll.ComponentKit/Components/CanvasWorkbench.razor#L1-L572
- src/CanDoItAll.ComponentKit/Canvas/CanvasWorkbenchContracts.cs#L5-L340
- src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099
- src/CanDoItAll.ComponentKit/wwwroot/canvas-workbench.css#L1-L2309
- src/CanDoItAll.ComponentKit/Components/CanvasWorkbench.razor
- src/CanDoItAll.ComponentKit/Canvas/Graph/CanvasWorkbenchShell.cs
- src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js
- tests/CanDoItAll.Tests.Components/CanvasWorkbenchTests.cs

## Dependencies

- CanvasSceneHost
- ViewportController
- SelectionModel
- ContextMenuHost
- CreateActionPalette

## Performance requirements

The shell should orchestrate incremental refreshes rather than trigger full runtime rebuilds on every parameter change.

## UX/UI notes

Preserve the current polished shell behaviors while reducing hidden coupling to page-level workflows.

## Accessibility notes

Buttons, help overlays, and zoom controls must remain keyboard accessible and semantically labeled.

## Recommended implementation in Blazor + C#

Use Blazor components for structure and state orchestration. Only push to JS the overlay positioning, pointer-heavy geometry, or runtime-owned rendering details.

## Recommended JS layer

Implement a dedicated JS module with a narrow public API. Own pointer-heavy math, rendering loops, hit testing, and host integration there; keep business and persistence decisions in C#.

## Animation guidance

Allow subtle opacity or transform transitions only where they improve perceived smoothness without adding lag.

## Render strategy

Use a hybrid strategy: DOM for rich card/editing content, SVG or canvas for geometric overlays, and CSS transforms for viewport movement.

## Test recommendations

Add component tests for toolbar state, callback wiring, and overlay slot rendering.

## Validation recommendations

- Validate all typed input contracts and reject invalid IDs or impossible bounds early.
- Add at least one component test and one targeted logic test for the primary behavior.
- Confirm no duplicate shared abstraction is created next to the existing framework entry points.
- Verify read-only/disabled behavior and error fallback handling.
- Check performance under large or rapidly changing scenes when the component participates in hot paths.

## Future extensions

- Pluggable shell with slot-based overlays, diagnostics toggles, minimap, and accessibility mirror integration.
- Allow plugins or domain adapters to extend this component without forking the shared framework.
- Add diagnostics and performance counters before increasing runtime complexity.

## API proposal

```csharp
public sealed class CanvasWorkbenchShellProps
{
    public string Id { get; init; } = string.Empty;
    public bool IsOpen { get; init; }
    public bool IsReadOnly { get; init; }
}
```

## Internal structure

- Shell chrome
- Host slots
- Toolbar/action region
- State synchronization

## Responsiveness

The component should define how it collapses, docks, or repositions on narrower stage widths. Avoid assuming a permanent ultra-wide desktop canvas.

## Theming / styling / skinning

Consume semantic theme tokens rather than hardcoded colors or spacing. Stay dark/light-ready even if the current product ships only one theme at first.

## ASCII sketch

```text
+------------------------------------------------------+
| Stage / host                                          |
|  +---------------- background layer ----------------+  |
|  +---------------- connector layer ----------------+  |
|  +---------------- node/content layer -------------+  |
|  +---------------- overlay layer ------------------+  |
|  +---------------- diagnostics/a11y --------------+  |
+------------------------------------------------------+
```

## State diagram

```text
Idle -> Measuring -> Rendered -> Updating -> Rendered
               \-> ErrorFallback -> Rendered/Hidden
```

## Event list

- Measured
- Rendered
- Clicked
- Hovered
- Focused

## Callback list

- OnMeasured
- OnClicked
- OnHovered
- OnFocused

## Validation scenarios

- Happy-path render or interaction flow works from start to finish.
- Read-only and disabled states suppress actions without breaking layout.
- Error fallback preserves a stable UI and exposes diagnostics.
- Component integrates cleanly with its declared dependencies and does not bypass shared ownership boundaries.
- Interop contract is stable, coarse-grained, and does not spam cross-boundary calls during hot interactions.

## Key repository references

- `src/CanDoItAll.ComponentKit/Components/CanvasWorkbench.razor#L1-L572`
- `src/CanDoItAll.ComponentKit/Canvas/CanvasWorkbenchContracts.cs#L5-L340`
- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099`
- `src/CanDoItAll.ComponentKit/wwwroot/canvas-workbench.css#L1-L2309`
