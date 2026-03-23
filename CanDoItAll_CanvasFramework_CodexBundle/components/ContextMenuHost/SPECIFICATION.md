# ContextMenuHost Specification

## Purpose

Own context-menu placement, nested menus, keyboard dismissal, focus handling, and action dispatch.

## User scenarios

- Project Structure node actions, create menus, and utility commands.
- Prompt Factory node and session context actions.
- Future clipboard, validation, and diagnostics menu groups.

## Functional scope

### Minimum viable version

Extract createContextMenuLayer/showContextMenu into a standalone context-menu subsystem.

### Target advanced version

Command search, menu virtualization, touch-safe long-press mode, and analytics-friendly action instrumentation.

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
- src/CanDoItAll.Modules.Factory/PromptFactoryCanvasCatalog.cs#L1-L645
- src/CanDoItAll.Modules.Workbench/ProjectStructureCanvasCatalog.cs#L1-L326
- docs/ui-shared-components/recommendations/missing-components.md#L1-L241
- src/CanDoItAll.ComponentKit/Canvas/Graph/ContextMenuHost.cs
- src/CanDoItAll.ComponentKit/Components/ContextMenuHost.razor
- src/CanDoItAll.ComponentKit/wwwroot/js/context-menu-host.js
- tests/CanDoItAll.Tests.Components/ContextMenuHostTests.cs

## Dependencies

- HoverFocusRouter
- KeyboardShortcutRouter
- TextBlockPrimitive
- IconGlyphPrimitive

## Performance requirements

Use event delegation and lazy subtree rendering for large nested menus.

## UX/UI notes

Menu placement and dismissal behavior strongly affects perceived polish.

## Accessibility notes

Support roving focus, escape dismissal, arrow-key navigation, and semantic menu roles.

## Recommended implementation in Blazor + C#

Use Blazor components for structure and state orchestration. Only push to JS the overlay positioning, pointer-heavy geometry, or runtime-owned rendering details.

## Recommended JS layer

Implement a dedicated JS module with a narrow public API. Own pointer-heavy math, rendering loops, hit testing, and host integration there; keep business and persistence decisions in C#.

## Animation guidance

Allow subtle opacity or transform transitions only where they improve perceived smoothness without adding lag.

## Render strategy

Use a hybrid strategy: DOM for rich card/editing content, SVG or canvas for geometric overlays, and CSS transforms for viewport movement.

## Test recommendations

Cover nested menus, focus return, disabled items, and viewport-edge placement.

## Validation recommendations

- Validate all typed input contracts and reject invalid IDs or impossible bounds early.
- Add at least one component test and one targeted logic test for the primary behavior.
- Confirm no duplicate shared abstraction is created next to the existing framework entry points.
- Verify read-only/disabled behavior and error fallback handling.
- Check performance under large or rapidly changing scenes when the component participates in hot paths.

## Future extensions

- Command search, menu virtualization, touch-safe long-press mode, and analytics-friendly action instrumentation.
- Allow plugins or domain adapters to extend this component without forking the shared framework.
- Add diagnostics and performance counters before increasing runtime complexity.

## API proposal

```csharp
public sealed class ContextMenuHostProps
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
- Old ownership point is genuinely reduced or removed after extraction.
- Interop contract is stable, coarse-grained, and does not spam cross-boundary calls during hot interactions.

## Key repository references

- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099`
- `src/CanDoItAll.ComponentKit/wwwroot/canvas-workbench.css#L1-L2309`
- `src/CanDoItAll.Modules.Factory/PromptFactoryCanvasCatalog.cs#L1-L645`
- `src/CanDoItAll.Modules.Workbench/ProjectStructureCanvasCatalog.cs#L1-L326`
- `docs/ui-shared-components/recommendations/missing-components.md#L1-L241`
