# RecommendationOverlay Specification

## Purpose

Display AI-driven or rules-driven recommendations directly on the Prompt Factory canvas as contextual badges, callouts, or action popovers.

## User scenarios

- Show suggested next blocks or missing inputs near a selected prompt node.
- Present accept/reject actions for recommendations directly on the canvas.
- Drive future onboarding or best-practice guidance in the editor.

## Functional scope

### Minimum viable version

Contextual recommendation badges with accept/open-details actions.

### Target advanced version

Ranked recommendation panels, inline previews, and one-click graph insertion.

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

- src/CanDoItAll.Modules.Factory/PromptFactoryService.cs#L238-L715
- src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor#L1-L2866
- src/CanDoItAll.Modules.Factory/PromptFactoryCanvasCatalog.cs#L1-L645
- src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099
- src/CanDoItAll.Modules.Factory/CanvasAdapters/RecommendationOverlay.cs
- src/CanDoItAll.Modules.Factory/wwwroot/js/recommendation-overlay.js
- tests/CanDoItAll.Tests.Components/RecommendationOverlayTests.cs

## Dependencies

- TooltipPopoverHost
- ChipBadgePrimitive
- HoverFocusRouter
- SelectionModel

## Performance requirements

Only compute and render recommendations for active context, not the full graph.

## UX/UI notes

Recommendations must remain supportive and dismissible, never disruptive.

## Accessibility notes

Make recommendation rationale and accept/dismiss controls semantically clear.

## Recommended implementation in Blazor + C#

Use Blazor components for structure and state orchestration. Only push to JS the overlay positioning, pointer-heavy geometry, or runtime-owned rendering details.

## Recommended JS layer

Use a thin JS helper only for geometry, measurement, DOM positioning, or runtime integration. Keep the public contract coarse-grained and typed.

## Animation guidance

Use very short fade/scale transitions and respect reduced-motion preferences. State clarity is more important than decoration.

## Render strategy

Use a hybrid strategy: DOM for rich card/editing content, SVG or canvas for geometric overlays, and CSS transforms for viewport movement.

## Test recommendations

Cover recommendation display, dismissal, and accept-action callback integrity.

## Validation recommendations

- Validate all typed input contracts and reject invalid IDs or impossible bounds early.
- Add at least one component test and one targeted logic test for the primary behavior.
- Confirm no duplicate shared abstraction is created next to the existing framework entry points.
- Verify read-only/disabled behavior and error fallback handling.
- Check performance under large or rapidly changing scenes when the component participates in hot paths.

## Future extensions

- Ranked recommendation panels, inline previews, and one-click graph insertion.
- Keep the domain-specific layer thin enough that future product modules can mirror the same pattern.
- Add diagnostics and performance counters before increasing runtime complexity.

## API proposal

```csharp
public sealed class RecommendationOverlayProps
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

Primary target is desktop, but the component should still avoid brittle assumptions about host size, zoom level, or maximized state.

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
- Domain-specific rules remain in the adapter and do not leak into shared framework components.

## Key repository references

- `src/CanDoItAll.Modules.Factory/PromptFactoryService.cs#L238-L715`
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor#L1-L2866`
- `src/CanDoItAll.Modules.Factory/PromptFactoryCanvasCatalog.cs#L1-L645`
- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099`
