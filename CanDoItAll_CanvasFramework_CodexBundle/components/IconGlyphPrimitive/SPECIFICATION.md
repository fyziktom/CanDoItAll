# IconGlyphPrimitive Specification

## Purpose

Render small icon glyphs, state icons, and symbolic markers with unified sizing and alignment rules.

## User scenarios

- Node type icons in Project Structure and Prompt Factory.
- Toolbar and context menu icons in the workbench shell.
- Small semantic markers such as warning, progress, attachment, or locked state.

## Functional scope

### Minimum viable version

Shared glyph contract with icon source, size, tone, and baseline alignment.

### Target advanced version

Sprite-sheet or SVG symbol support, semantic icon variants, and density-aware sizing rules.

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
- src/CanDoItAll.Modules.Factory/PromptFactoryCanvasCatalog.cs#L1-L645
- src/CanDoItAll.Modules.Workbench/ProjectStructureCanvasCatalog.cs#L1-L326
- src/CanDoItAll.ComponentKit/Canvas/Graph/IconGlyphPrimitive.cs
- tests/CanDoItAll.Tests.Components/IconGlyphPrimitiveTests.cs

## Dependencies

- CanvasThemeTokenPack
- TextBlockPrimitive

## Performance requirements

Prefer inline SVG symbols or font-independent paths over many external image requests.

## UX/UI notes

Icons must reinforce meaning rather than becoming decorative noise.

## Accessibility notes

Decorative icons must be hidden from semantics; meaningful icons need text equivalents.

## Recommended implementation in Blazor + C#

Define typed props/state in C# and keep business semantics there. JS should only own the pieces that are directly tied to browser APIs or hot-path rendering.

## Recommended JS layer

No dedicated JS bridge should be required. If browser-specific behavior appears later, add it through the shared JsInteropBridge instead of ad hoc scripts.

## Animation guidance

Allow subtle opacity or transform transitions only where they improve perceived smoothness without adding lag.

## Render strategy

Use a hybrid strategy: DOM for rich card/editing content, SVG or canvas for geometric overlays, and CSS transforms for viewport movement.

## Test recommendations

Validate size alignment in cards, menus, and chip compositions.

## Validation recommendations

- Validate all typed input contracts and reject invalid IDs or impossible bounds early.
- Add at least one component test and one targeted logic test for the primary behavior.
- Confirm no duplicate shared abstraction is created next to the existing framework entry points.
- Verify read-only/disabled behavior and error fallback handling.
- Check performance under large or rapidly changing scenes when the component participates in hot paths.

## Future extensions

- Sprite-sheet or SVG symbol support, semantic icon variants, and density-aware sizing rules.
- Allow plugins or domain adapters to extend this component without forking the shared framework.

## API proposal

```csharp
public sealed class IconGlyphPrimitiveProps
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

## Key repository references

- `src/CanDoItAll.ComponentKit/wwwroot/canvas-workbench.css#L1-L2309`
- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099`
- `src/CanDoItAll.Modules.Factory/PromptFactoryCanvasCatalog.cs#L1-L645`
- `src/CanDoItAll.Modules.Workbench/ProjectStructureCanvasCatalog.cs#L1-L326`
