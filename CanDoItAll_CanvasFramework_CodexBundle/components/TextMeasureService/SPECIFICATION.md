# TextMeasureService Specification

## Purpose

Provide shared text measurement, wrapping, truncation, ellipsis, multi-line sizing, and font-cache behavior for graph and calendar surfaces.

## User scenarios

- Measure node titles and subtitles before final card layout.
- Apply consistent multi-line wrapping and ellipsis rules to calendar events and graph cards.
- Support icon+text chips and badges without repeated browser measurement code.

## Functional scope

### Minimum viable version

JS-backed measureText bridge with a C# contract for font spec, max width, line clamp, and truncation mode.

### Target advanced version

Cached text layout engine with font readiness tracking, fallback heuristics, RTL awareness, and test harnesses.

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
- Very long single words.
- Emoji or mixed-script labels.
- Font not yet ready when first measurement is requested.

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

- src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099
- src/CanDoItAll.ComponentKit/wwwroot/js/zy-canvas-calendar.js#L1-L3720
- src/CanDoItAll.ComponentKit/wwwroot/canvas-workbench.css#L1-L2309
- docs/canvases-improvements/01-reference-and-gap-analysis.md#L1-L203
- src/CanDoItAll.ComponentKit/Canvas/Graph/TextMeasureService.cs
- src/CanDoItAll.ComponentKit/wwwroot/js/text-measure-service.js
- tests/CanDoItAll.Tests.Components/TextMeasureServiceTests.cs

## Dependencies

- JsInteropBridge
- CanvasThemeTokenPack

## Performance requirements

Cache by text/font/width tuple and avoid re-measuring during every pointer move or hover change.

## UX/UI notes

Truncation rules must be visually consistent across node cards, menus, chips, and calendar tiles.

## Accessibility notes

Expose full text for tooltips and semantic mirrors when truncation occurs.

## Recommended implementation in Blazor + C#

Define typed props/state in C# and keep business semantics there. JS should only own the pieces that are directly tied to browser APIs or hot-path rendering.

## Recommended JS layer

Implement a dedicated JS module with a narrow public API. Own pointer-heavy math, rendering loops, hit testing, and host integration there; keep business and persistence decisions in C#.

## Animation guidance

Allow subtle opacity or transform transitions only where they improve perceived smoothness without adding lag.

## Render strategy

Renderer-agnostic surface in C# with JS implementation details hidden behind the host/runtime boundary.

## Test recommendations

Cover single-line, multi-line, emoji, long-word, and line-clamp edge cases.

## Validation recommendations

- Validate all typed input contracts and reject invalid IDs or impossible bounds early.
- Add at least one component test and one targeted logic test for the primary behavior.
- Confirm no duplicate shared abstraction is created next to the existing framework entry points.
- Verify read-only/disabled behavior and error fallback handling.
- Check performance under large or rapidly changing scenes when the component participates in hot paths.

## Future extensions

- Cached text layout engine with font readiness tracking, fallback heuristics, RTL awareness, and test harnesses.
- Allow plugins or domain adapters to extend this component without forking the shared framework.
- Add diagnostics and performance counters before increasing runtime complexity.

## API proposal

```csharp
public sealed class TextMeasureServiceProps
{
    public string Id { get; init; } = string.Empty;
    public bool IsSelected { get; init; }
    public bool IsDisabled { get; init; }
}
```

## Internal structure

- Props model
- TextMeasureService integration
- Line-break/truncation strategy
- Tooltip/full-text fallback

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
- Interop contract is stable, coarse-grained, and does not spam cross-boundary calls during hot interactions.

## Key repository references

- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099`
- `src/CanDoItAll.ComponentKit/wwwroot/js/zy-canvas-calendar.js#L1-L3720`
- `src/CanDoItAll.ComponentKit/wwwroot/canvas-workbench.css#L1-L2309`
- `docs/canvases-improvements/01-reference-and-gap-analysis.md#L1-L203`
