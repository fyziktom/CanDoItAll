# TooltipPopoverHost Specification

## Purpose

Provide unified tooltip and popover behavior for truncated text, validation messages, inline helpers, and contextual rich previews.

## User scenarios

- Show full text for truncated labels.
- Display validation guidance or recommendation details near nodes.
- Provide small popovers for badges, anchor points, or minimap items.

## Functional scope

### Minimum viable version

Tooltip host with hover/focus trigger modes and viewport-aware positioning.

### Target advanced version

Rich popovers with action slots, pinning, and cross-overlay stacking coordination.

## Visual behavior

The component should appear above the canvas content with consistent elevation, radius, spacing, and theme tokens. It must avoid occluding critical content whenever an anchored placement can solve that.

## Interaction behavior

This component owns interaction semantics within its boundary and must coordinate cleanly with SelectionModel, HoverFocusRouter, KeyboardShortcutRouter, and the surrounding shell.

## Component states

- Hidden
- Visible
- Focused
- Dismissed
- Error

## Edge cases

- Null or incomplete input model arriving during a mid-refresh state.
- Rapid successive updates caused by selection changes, drag loops, or remote persistence echoes.
- Read-only or disabled mode where visuals still need to render but actions must not execute.

## Error behavior

Show a localized inline error state when user-facing actions fail, keep the previous stable state visible, and preserve focus for retry or cancel.

## Responsibilities

- Present a focused high-level interaction surface.
- Own visibility/focus/commit lifecycle within its boundary.
- Coordinate cleanly with the shell and selection state.

## Responsibility boundaries

- Does not become a catch-all for unrelated page actions.
- Does not duplicate shared shell responsibilities.
- Does not bypass selection/focus rules.

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
- IsOpen
- AnchorRect
- PlacementMode
- ZIndex

## Data model

Visibility state + anchor/placement state + local draft/action state + callback payloads.

## Integration points

- src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099
- src/CanDoItAll.ComponentKit/wwwroot/canvas-workbench.css#L1-L2309
- docs/ui-shared-components/recommendations/missing-components.md#L1-L241
- src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor#L1-L2866
- src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor#L1-L1399
- src/CanDoItAll.ComponentKit/Canvas/Graph/TooltipPopoverHost.cs
- src/CanDoItAll.ComponentKit/Components/TooltipPopoverHost.razor
- src/CanDoItAll.ComponentKit/wwwroot/js/tooltip-popover-host.js
- tests/CanDoItAll.Tests.Components/TooltipPopoverHostTests.cs

## Dependencies

- HoverFocusRouter
- CanvasThemeTokenPack
- TextBlockPrimitive

## Performance requirements

Defer popover content creation until trigger intent is clear.

## UX/UI notes

Tooltips should help, not create hover flicker or occlude important canvas content.

## Accessibility notes

Support focus-triggered descriptions and dismissible popovers with correct ARIA patterns.

## Recommended implementation in Blazor + C#

Use Blazor components for structure and state orchestration. Only push to JS the overlay positioning, pointer-heavy geometry, or runtime-owned rendering details.

## Recommended JS layer

Implement a dedicated JS module with a narrow public API. Own pointer-heavy math, rendering loops, hit testing, and host integration there; keep business and persistence decisions in C#.

## Animation guidance

Use very short fade/scale transitions and respect reduced-motion preferences. State clarity is more important than decoration.

## Render strategy

Use a hybrid strategy: DOM for rich card/editing content, SVG or canvas for geometric overlays, and CSS transforms for viewport movement.

## Test recommendations

Cover hover delay, focus trigger, and edge-aware positioning.

## Validation recommendations

- Validate all typed input contracts and reject invalid IDs or impossible bounds early.
- Add at least one component test and one targeted logic test for the primary behavior.
- Confirm no duplicate shared abstraction is created next to the existing framework entry points.
- Verify read-only/disabled behavior and error fallback handling.
- Check performance under large or rapidly changing scenes when the component participates in hot paths.

## Future extensions

- Rich popovers with action slots, pinning, and cross-overlay stacking coordination.
- Allow plugins or domain adapters to extend this component without forking the shared framework.
- Add diagnostics and performance counters before increasing runtime complexity.

## API proposal

```csharp
public sealed class TooltipPopoverHostProps
{
    public string Id { get; init; } = string.Empty;
    public bool IsOpen { get; init; }
    public bool IsReadOnly { get; init; }
}
```

## Internal structure

- Visibility controller
- Anchor/placement resolver
- Presentation component
- Dismissal/focus policy

## Responsiveness

The component should define how it collapses, docks, or repositions on narrower stage widths. Avoid assuming a permanent ultra-wide desktop canvas.

## Theming / styling / skinning

Consume semantic theme tokens rather than hardcoded colors or spacing. Stay dark/light-ready even if the current product ships only one theme at first.

## ASCII sketch

```text
      +----------------------+
      | overlay / popover    |
+-----+----------------------+-----+
|         selected target          |
+----------------------------------+
```

## State diagram

```text
Hidden -> Visible -> Focused -> Hidden
            \-> Dismissed ----^
```

## Event list

- Opened
- Closed
- ActionInvoked
- AnchorChanged

## Callback list

- OnOpened
- OnClosed
- OnActionInvoked

## Validation scenarios

- Happy-path render or interaction flow works from start to finish.
- Read-only and disabled states suppress actions without breaking layout.
- Error fallback preserves a stable UI and exposes diagnostics.
- Component integrates cleanly with its declared dependencies and does not bypass shared ownership boundaries.
- Interop contract is stable, coarse-grained, and does not spam cross-boundary calls during hot interactions.

## Key repository references

- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099`
- `src/CanDoItAll.ComponentKit/wwwroot/canvas-workbench.css#L1-L2309`
- `docs/ui-shared-components/recommendations/missing-components.md#L1-L241`
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor#L1-L2866`
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor#L1-L1399`
