# CreateActionPalette Specification

## Purpose

Present quick-create actions, grouped create menus, and contextual create composers using shared action metadata.

## User scenarios

- Open quick-create from the workbench toolbar.
- Open contextual create flows from node menus or inspector groups.
- Support future command palette-like insert experiences.

## Functional scope

### Minimum viable version

Shared action palette that renders CanvasWorkbenchAction trees and forwards create requests consistently.

### Target advanced version

Searchable grouped palette, pinned favorites, keyboard command integration, and rich templates with previews.

## Visual behavior

The component should appear above the canvas content with consistent elevation, radius, spacing, and theme tokens. It must avoid occluding critical content whenever an anchored placement can solve that.

## Interaction behavior

This component owns interaction semantics within its boundary and must coordinate cleanly with SelectionModel, HoverFocusRouter, KeyboardShortcutRouter, and the surrounding shell.

## Component states

- Closed
- Opening
- Editing
- Validating
- Saving
- ValidationError
- Cancelled

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

- src/CanDoItAll.ComponentKit/Canvas/CanvasWorkbenchContracts.cs#L5-L340
- src/CanDoItAll.ComponentKit/Components/CanvasWorkbench.razor#L1-L572
- src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099
- src/CanDoItAll.Modules.Workbench/ProjectStructureCanvasCatalog.cs#L1-L326
- src/CanDoItAll.Modules.Factory/PromptFactoryCanvasCatalog.cs#L1-L645
- src/CanDoItAll.ComponentKit/Canvas/Graph/CreateActionPalette.cs
- src/CanDoItAll.ComponentKit/Components/CreateActionPalette.razor
- src/CanDoItAll.ComponentKit/wwwroot/js/create-action-palette.js
- tests/CanDoItAll.Tests.Components/CreateActionPaletteTests.cs

## Dependencies

- ContextMenuHost
- InlineEditorComposer
- TextBlockPrimitive
- IconGlyphPrimitive

## Performance requirements

Lazy-render deep menu groups and reuse cached action trees from domain adapters.

## UX/UI notes

Creation should be fast, searchable, and context-aware without hiding available options.

## Accessibility notes

Support full keyboard navigation and screen-reader-friendly grouped action descriptions.

## Recommended implementation in Blazor + C#

Use Blazor components for structure and state orchestration. Only push to JS the overlay positioning, pointer-heavy geometry, or runtime-owned rendering details.

## Recommended JS layer

Implement a dedicated JS module with a narrow public API. Own pointer-heavy math, rendering loops, hit testing, and host integration there; keep business and persistence decisions in C#.

## Animation guidance

Allow subtle opacity or transform transitions only where they improve perceived smoothness without adding lag.

## Render strategy

Use a hybrid strategy: DOM for rich card/editing content, SVG or canvas for geometric overlays, and CSS transforms for viewport movement.

## Test recommendations

Cover grouped menus, disabled actions, and create request payload integrity.

## Validation recommendations

- Validate all typed input contracts and reject invalid IDs or impossible bounds early.
- Add at least one component test and one targeted logic test for the primary behavior.
- Confirm no duplicate shared abstraction is created next to the existing framework entry points.
- Verify read-only/disabled behavior and error fallback handling.
- Check performance under large or rapidly changing scenes when the component participates in hot paths.

## Future extensions

- Searchable grouped palette, pinned favorites, keyboard command integration, and rich templates with previews.
- Allow plugins or domain adapters to extend this component without forking the shared framework.
- Add diagnostics and performance counters before increasing runtime complexity.

## API proposal

```csharp
public sealed class CreateActionPaletteProps
{
    public string Id { get; init; } = string.Empty;
    public bool IsOpen { get; init; }
    public bool IsReadOnly { get; init; }
}
```

## Internal structure

- Draft state model
- Validation adapter
- Commit/cancel pipeline
- Focus return policy

## Responsiveness

Primary target is desktop, but the component should still avoid brittle assumptions about host size, zoom level, or maximized state.

## Theming / styling / skinning

Consume semantic theme tokens rather than hardcoded colors or spacing. Stay dark/light-ready even if the current product ships only one theme at first.

## ASCII sketch

```text
+--------------------------+
| Title                    |
| [ editable field      ]  |
| [ save ] [ cancel ]      |
+--------------------------+
```

## State diagram

```text
Closed -> Opening -> Editing -> Validating -> Saving -> Closed
                       \-> Cancelled ---------------> Closed
                       \-> ValidationError -> Editing
```

## Event list

- Opened
- ValueChanged
- Committed
- Cancelled
- ValidationFailed

## Callback list

- OnCommitAsync
- OnCancelAsync
- OnValidationRequestedAsync

## Validation scenarios

- Happy-path render or interaction flow works from start to finish.
- Read-only and disabled states suppress actions without breaking layout.
- Error fallback preserves a stable UI and exposes diagnostics.
- Component integrates cleanly with its declared dependencies and does not bypass shared ownership boundaries.
- Old ownership point is genuinely reduced or removed after extraction.
- Interop contract is stable, coarse-grained, and does not spam cross-boundary calls during hot interactions.

## Key repository references

- `src/CanDoItAll.ComponentKit/Canvas/CanvasWorkbenchContracts.cs#L5-L340`
- `src/CanDoItAll.ComponentKit/Components/CanvasWorkbench.razor#L1-L572`
- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099`
- `src/CanDoItAll.Modules.Workbench/ProjectStructureCanvasCatalog.cs#L1-L326`
- `src/CanDoItAll.Modules.Factory/PromptFactoryCanvasCatalog.cs#L1-L645`
