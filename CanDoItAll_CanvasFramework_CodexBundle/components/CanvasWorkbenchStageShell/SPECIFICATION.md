# CanvasWorkbenchStageShell Specification

## Purpose

Shared stage layout that wraps the canvas shell with eyebrow/title copy, stats, inspector area, and supporting panel zones.

## User scenarios

- Render left canvas and right inspector layout.
- Expose lower supporting panels and custom toolbar slots.
- Provide a consistent product-family stage frame for future editors.

## Functional scope

### Minimum viable version

Retain current stage shell while tightening slot naming and documentation.

### Target advanced version

Density variants, collapsible inspector panes, responsive breakpoints, and test IDs for diagnostics.

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

- src/CanDoItAll.ComponentKit/Components/CanvasWorkbenchStage.razor#L1-L82
- src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor#L1-L1399
- src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor#L1-L2866
- src/CanDoItAll.ComponentKit/Components/CanvasWorkbenchStage.razor
- src/CanDoItAll.ComponentKit/Canvas/Graph/CanvasWorkbenchStageShell.cs
- tests/CanDoItAll.Tests.Components/CanvasWorkbenchStageTests.cs

## Dependencies

- CanvasWorkbenchShell
- CanvasThemeTokenPack

## Performance requirements

Keep layout CSS-driven and avoid JS-driven stage resizing except when the host runtime needs measurement invalidation.

## UX/UI notes

The stage shell is a major contributor to product consistency and should stay visually stable during framework refactoring.

## Accessibility notes

Maintain logical heading order and semantic relationships between canvas and inspector zones.

## Recommended implementation in Blazor + C#

Use Blazor components for structure and state orchestration. Only push to JS the overlay positioning, pointer-heavy geometry, or runtime-owned rendering details.

## Recommended JS layer

No dedicated JS bridge should be required. If browser-specific behavior appears later, add it through the shared JsInteropBridge instead of ad hoc scripts.

## Animation guidance

Allow subtle opacity or transform transitions only where they improve perceived smoothness without adding lag.

## Render strategy

Use a hybrid strategy: DOM for rich card/editing content, SVG or canvas for geometric overlays, and CSS transforms for viewport movement.

## Test recommendations

Add component tests for slot rendering and responsive class changes.

## Validation recommendations

- Validate all typed input contracts and reject invalid IDs or impossible bounds early.
- Add at least one component test and one targeted logic test for the primary behavior.
- Confirm no duplicate shared abstraction is created next to the existing framework entry points.
- Verify read-only/disabled behavior and error fallback handling.
- Check performance under large or rapidly changing scenes when the component participates in hot paths.

## Future extensions

- Density variants, collapsible inspector panes, responsive breakpoints, and test IDs for diagnostics.
- Allow plugins or domain adapters to extend this component without forking the shared framework.

## API proposal

```csharp
public sealed class CanvasWorkbenchStageShellProps
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

## Key repository references

- `src/CanDoItAll.ComponentKit/Components/CanvasWorkbenchStage.razor#L1-L82`
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor#L1-L1399`
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor#L1-L2866`
