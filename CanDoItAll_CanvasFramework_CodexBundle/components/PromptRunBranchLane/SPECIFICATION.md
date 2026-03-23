# PromptRunBranchLane Specification

## Purpose

Represent and render branch lanes or grouped prompt-run paths within the shared graph workbench.

## User scenarios

- Visualize alternate prompt branches or outcomes.
- Keep branch-specific nodes aligned and grouped.
- Support future reorder or branch template flows.

## Functional scope

### Minimum viable version

Lane/group abstraction that can render branch labels and hold related nodes together.

### Target advanced version

Collapsible lanes, reorder handles, branch metrics, and validation badges.

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

- src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor#L1-L2866
- src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.Catalog.cs#L1-L1090
- src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099
- src/CanDoItAll.Modules.Factory/CanvasAdapters/PromptRunBranchLane.cs
- src/CanDoItAll.Modules.Factory/wwwroot/js/prompt-run-branch-lane.js
- tests/CanDoItAll.Tests.Components/PromptRunBranchLaneTests.cs

## Dependencies

- LayoutEngine
- GroupFrameOverlay
- TextBlockPrimitive

## Performance requirements

Treat lanes as lightweight grouping metadata rather than heavy nested DOM trees.

## UX/UI notes

Lanes should clarify branching without making the canvas feel rigid or over-segmented.

## Accessibility notes

Expose branch names and order in semantic summaries and inspector lists.

## Recommended implementation in Blazor + C#

Define typed props/state in C# and keep business semantics there. JS should only own the pieces that are directly tied to browser APIs or hot-path rendering.

## Recommended JS layer

Use a thin JS helper only for geometry, measurement, DOM positioning, or runtime integration. Keep the public contract coarse-grained and typed.

## Animation guidance

Allow subtle opacity or transform transitions only where they improve perceived smoothness without adding lag.

## Render strategy

Renderer-agnostic surface in C# with JS implementation details hidden behind the host/runtime boundary.

## Test recommendations

Cover lane bounds, ordering, and branch-label rendering.

## Validation recommendations

- Validate all typed input contracts and reject invalid IDs or impossible bounds early.
- Add at least one component test and one targeted logic test for the primary behavior.
- Confirm no duplicate shared abstraction is created next to the existing framework entry points.
- Verify read-only/disabled behavior and error fallback handling.
- Check performance under large or rapidly changing scenes when the component participates in hot paths.

## Future extensions

- Collapsible lanes, reorder handles, branch metrics, and validation badges.
- Keep the domain-specific layer thin enough that future product modules can mirror the same pattern.
- Add diagnostics and performance counters before increasing runtime complexity.

## API proposal

```csharp
public sealed class PromptRunBranchLaneProps
{
    public string Id { get; init; } = string.Empty;
    public bool IsSelected { get; init; }
    public bool IsDisabled { get; init; }
}
```

## Internal structure

- Layout policy
- Measurement cache
- Bounds normalizer
- Scene update trigger

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
- Domain-specific rules remain in the adapter and do not leak into shared framework components.

## Key repository references

- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor#L1-L2866`
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.Catalog.cs#L1-L1090`
- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099`
