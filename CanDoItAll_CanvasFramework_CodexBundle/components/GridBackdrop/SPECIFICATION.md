# GridBackdrop Specification

## Purpose

Render the shared grid, ruler cues, and spatial backdrop for graph-like canvas surfaces.

## User scenarios

- Show subtle grid lines for Project Structure placement.
- Provide zoom-aware guide spacing in Prompt Factory.
- Allow optional off state for distraction-free presentation mode.

## Functional scope

### Minimum viable version

Static zoom-aware grid backdrop with theme token support and optional major/minor line rendering.

### Target advanced version

Adaptive grid with guide labels, density changes by zoom level, ruler ticks, and canvas-minimap reuse.

## Visual behavior

The component should render using the shared canvas/workbench visual language, with consistent spacing, typography, contrast, and selected/hover/disabled states.

## Interaction behavior

If interactive at all, it should emit small semantic events such as click, hover, or focus without owning domain actions.

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

- src/CanDoItAll.ComponentKit/wwwroot/canvas-workbench.css#L1-L2309
- src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099
- src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor#L1-L1399
- src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor#L1-L2866
- src/CanDoItAll.ComponentKit/Canvas/Graph/GridBackdrop.cs
- src/CanDoItAll.ComponentKit/wwwroot/js/grid-backdrop.js
- tests/CanDoItAll.Tests.Components/GridBackdropTests.cs

## Dependencies

- CanvasSceneHost
- ViewportController
- SnapGuideSystem

## Performance requirements

Render via CSS background or lightweight canvas layer; avoid per-frame DOM element grids.

## UX/UI notes

Grid contrast must remain subtle; it should help orientation without competing with node cards.

## Accessibility notes

Grid is decorative and should not add keyboard stops or semantic noise.

## Recommended implementation in Blazor + C#

Define typed props/state in C# and keep business semantics there. JS should only own the pieces that are directly tied to browser APIs or hot-path rendering.

## Recommended JS layer

Implement a dedicated JS module with a narrow public API. Own pointer-heavy math, rendering loops, hit testing, and host integration there; keep business and persistence decisions in C#.

## Animation guidance

Allow subtle opacity or transform transitions only where they improve perceived smoothness without adding lag.

## Render strategy

Prefer lightweight SVG or canvas layers for geometry-heavy visuals; keep rich editable content in DOM where necessary.

## Test recommendations

Visual regression tests should confirm line density and alignment at multiple zoom levels.

## Validation recommendations

- Validate all typed input contracts and reject invalid IDs or impossible bounds early.
- Add at least one component test and one targeted logic test for the primary behavior.
- Confirm no duplicate shared abstraction is created next to the existing framework entry points.
- Verify read-only/disabled behavior and error fallback handling.
- Check performance under large or rapidly changing scenes when the component participates in hot paths.

## Future extensions

- Adaptive grid with guide labels, density changes by zoom level, ruler ticks, and canvas-minimap reuse.
- Allow plugins or domain adapters to extend this component without forking the shared framework.
- Add diagnostics and performance counters before increasing runtime complexity.

## API proposal

```csharp
public sealed class GridBackdropProps
{
    public string Id { get; init; } = string.Empty;
    public bool IsSelected { get; init; }
    public bool IsDisabled { get; init; }
}
```

## Internal structure

- Props model
- Measurement hook
- Render template
- State/style mapper

## Responsiveness

Primary target is desktop, but the component should still avoid brittle assumptions about host size, zoom level, or maximized state.

## Theming / styling / skinning

Consume semantic theme tokens rather than hardcoded colors or spacing. Stay dark/light-ready even if the current product ships only one theme at first.

## ASCII sketch

```text
+------------------------------+
| icon  title            chip  |
| subtitle / helper text       |
| metadata / preview / action  |
+------------------------------+
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
- Old ownership point is genuinely reduced or removed after extraction.
- Interop contract is stable, coarse-grained, and does not spam cross-boundary calls during hot interactions.

## Key repository references

- `src/CanDoItAll.ComponentKit/wwwroot/canvas-workbench.css#L1-L2309`
- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099`
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor#L1-L1399`
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor#L1-L2866`
