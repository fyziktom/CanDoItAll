# FloatingInspectorHost Specification

## Purpose

Support detachable or floating inspector presentation anchored to the canvas stage.

## User scenarios

- Prompt Factory floating inspector docking and persistence.
- Future compact inspector mode for smaller screens or multi-canvas workflows.
- Contextual mini inspectors near selected nodes.

## Functional scope

### Minimum viable version

Extract mountFloatingInspector and floating inspector docking lifecycle into its own helper module.

### Target advanced version

Pinned/detached states, saved positions, responsive fallback docking, and multiple inspector panels.

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

- src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.CanvasInspector.cs#L1-L37
- src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099
- src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor#L1-L2866
- src/CanDoItAll.ComponentKit/Components/CanvasWorkbenchStage.razor#L1-L82
- src/CanDoItAll.ComponentKit/Canvas/Graph/FloatingInspectorHost.cs
- src/CanDoItAll.ComponentKit/Components/FloatingInspectorHost.razor
- src/CanDoItAll.ComponentKit/wwwroot/js/floating-inspector-host.js
- tests/CanDoItAll.Tests.Components/FloatingInspectorHostTests.cs

## Dependencies

- CanvasWorkbenchStageShell
- HoverFocusRouter
- CanvasThemeTokenPack

## Performance requirements

Sync only geometry and state needed for placement; keep content rendering in Blazor.

## UX/UI notes

Floating inspectors must never obscure critical editing surfaces without a clear reposition/dock affordance.

## Accessibility notes

Provide dock/undock controls and clear focus order between floating panel and canvas.

## Recommended implementation in Blazor + C#

Use Blazor components for structure and state orchestration. Only push to JS the overlay positioning, pointer-heavy geometry, or runtime-owned rendering details.

## Recommended JS layer

Implement a dedicated JS module with a narrow public API. Own pointer-heavy math, rendering loops, hit testing, and host integration there; keep business and persistence decisions in C#.

## Animation guidance

Allow subtle opacity or transform transitions only where they improve perceived smoothness without adding lag.

## Render strategy

Use a hybrid strategy: DOM for rich card/editing content, SVG or canvas for geometric overlays, and CSS transforms for viewport movement.

## Test recommendations

Cover dock state restore, resize handling, and focus cycling.

## Validation recommendations

- Validate all typed input contracts and reject invalid IDs or impossible bounds early.
- Add at least one component test and one targeted logic test for the primary behavior.
- Confirm no duplicate shared abstraction is created next to the existing framework entry points.
- Verify read-only/disabled behavior and error fallback handling.
- Check performance under large or rapidly changing scenes when the component participates in hot paths.

## Future extensions

- Pinned/detached states, saved positions, responsive fallback docking, and multiple inspector panels.
- Allow plugins or domain adapters to extend this component without forking the shared framework.
- Add diagnostics and performance counters before increasing runtime complexity.

## API proposal

```csharp
public sealed class FloatingInspectorHostProps
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

- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.CanvasInspector.cs#L1-L37`
- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099`
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor#L1-L2866`
- `src/CanDoItAll.ComponentKit/Components/CanvasWorkbenchStage.razor#L1-L82`
